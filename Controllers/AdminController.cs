/* Subtext/Controllers/AdminController.cs

This file is part of the Subtext server.

Subtext is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Subtext is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with Subtext. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Subtext.Models;
using System.Security.Cryptography;

namespace Subtext.Controllers {
	[Produces("application/json")]
	[Route("/Subtext/admin")]
	[ApiController]
	public class AdminController : ControllerBase {
		private readonly ChatContext context;
		
		static RandomNumberGenerator rng = RandomNumberGenerator.Create();
		
		public AdminController(ChatContext context) {
			this.context = context;
		}
		
		[HttpGet("login/challenge")]
		public async Task<ActionResult> LoginChallenge(
			Guid adminId
		) {
			Admin admin = await context.Admins.FindAsync(adminId);
			if (admin == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (admin.IsLoggedIn) {
				return StatusCode(403, new APIError("AdminLoggedIn"));
			}
			
			byte[] challenge = new byte[Subtext.Config.secretSize];
			rng.GetBytes(challenge);
			admin.Challenge = challenge;
			
			await context.SaveChangesAsync();
			
			return StatusCode(200, challenge);
		}
		
		[HttpPost("login/response")]
		public async Task<ActionResult> LoginResponse(
			Guid adminId,
			[FromQuery] byte[] response
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			Admin admin = await context.Admins.FindAsync(adminId);
			if (admin == null) {
				result.Add("error", "NoObjectWithId");
				return StatusCode(404, result);
			}
			
			if (admin.IsLoggedIn) {
				result.Add("error", "AdminLoggedIn");
				return StatusCode(403, result);
			}
			
			Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(admin.Secret, admin.Challenge, Subtext.Config.pbkdf2Iterations);
			byte[] expectedResponse = pbkdf2.GetBytes(Subtext.Config.secretSize);
			
			if (response.SequenceEqual(expectedResponse)) {
				AdminSession session = new AdminSession();
				session.Admin = admin;
				session.Timestamp = DateTime.UtcNow;
				await context.AdminSessions.AddAsync(session);
				
				admin.IsLoggedIn = true;
				
				byte[] challenge = new byte[Subtext.Config.secretSize];
				rng.GetBytes(challenge);
				admin.Challenge = challenge;
				
				await LogAdminAction(admin, "Login.Success", "");
				
				await context.SaveChangesAsync();
				
				result.Add("sessionId", session.Id);
				
				return StatusCode(200, result);
			} else {
				result.Add("error", "IncorrectResponse");
				await LogAdminAction(admin, "Login.Failure", "");
				
				byte[] challenge = new byte[Subtext.Config.secretSize];
				rng.GetBytes(challenge);
				admin.Challenge = challenge;
				
				await context.SaveChangesAsync();
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-Admin");
				
				return StatusCode(401, result);
			}
		}
		
		[Serializable]
		enum SessionVerificationResult {
			Success,
			SessionNotFound,
			UserNotFound,
			SessionExpired,
			UserLoggedOut,
			OtherError
		}
		
		async Task<(SessionVerificationResult, AdminSession)> VerifyAdminSession(Guid sessionId) {
			AdminSession session = await context.AdminSessions.FindAsync(sessionId);
			if (session == null) {
				return (SessionVerificationResult.SessionNotFound, null);
			}
			
			if (session.Timestamp + Subtext.Config.adminSessionDuration < DateTime.UtcNow) {
				return (SessionVerificationResult.SessionExpired, session);
			}
			
			await context.Entry(session).Reference(s => s.Admin).LoadAsync();
			Admin admin = session.Admin;
			if (admin == null) {
				// This should never happen
				return (SessionVerificationResult.UserNotFound, session);
			}
			
			if (!admin.IsLoggedIn) {
				return (SessionVerificationResult.UserLoggedOut, session);
			}
			
			return (SessionVerificationResult.Success, session);
		}
		
		async Task<bool> VerifyAdminPermissions(Admin admin, string action) {
			if (admin == null) {
				return false;
			}
			await context.Entry(admin).Collection(a => a.Permissions).LoadAsync();
			foreach (PermissionRecord permission in admin.Permissions) {
				if (permission.Action.EndsWith("*")) {
					string match = permission.Action.Substring(0, permission.Action.Length - 1);
					if (action.Substring(0, match.Length) == match) {
						return true;
					}
				}
				if (action == permission.Action) {
					return true;
				}
			}
			return false;
		}
		
		async Task LogAdminAction(Admin admin, string action, string details) {
			if (admin == null) {
				return;
			}
			
			AuditLogEntry logEntry = new AuditLogEntry();
			logEntry.Admin = admin;
			logEntry.Action = action;
			logEntry.Details = details;
			logEntry.Timestamp = DateTime.UtcNow;
			
			await context.AuditLog.AddAsync(logEntry);
		}
		
		[HttpPost("renew")]
		public async Task<ActionResult> Renew(
			Guid sessionId
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			(SessionVerificationResult verificationResult, AdminSession session) = await VerifyAdminSession(sessionId);
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					result.Add("error", "NoObjectWithId");
					return StatusCode(404, result);
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					result.Add("error", "NoObjectWithId");
					return StatusCode(500, result);
				}
				if (verificationResult == SessionVerificationResult.UserLoggedOut) {
					result.Add("error", "AdminLoggedOut");
					return StatusCode(403, result);
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					result.Add("error", "SessionExpired");
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-Admin");
					return StatusCode(401, result);
				}
				
				result.Add("error", "AuthError");
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-Admin");
				return StatusCode(401, result);
			}
			
			session.Timestamp = DateTime.UtcNow;
			await context.SaveChangesAsync();
			
			result.Add("success", true);
			
			return StatusCode(200, result);
		}
		
		[HttpPost("logout")]
		public async Task<ActionResult> Logout(
			Guid sessionId
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			AdminSession session = await context.AdminSessions.FindAsync(sessionId);
			if (session == null) {
				result.Add("error", "NoObjectWithId");
				return StatusCode(404, result);
			}
			
			await context.Entry(session).Reference(s => s.Admin).LoadAsync();
			Admin admin = session.Admin;
			if (admin == null) {
				result.Add("error", "NoObjectWithId");
				return StatusCode(500, result);
			}
			
			admin.IsLoggedIn = false;
			
			await LogAdminAction(admin, "Logout", "");
			
			context.AdminSessions.Remove(session);
			await context.SaveChangesAsync();
			
			result.Add("success", true);
			
			return StatusCode(200, result);
		}
		
		[HttpGet("auditlog")]
		public async Task<ActionResult> AuditLog(
			Guid sessionId,
			int? start = null,
			int? count = null,
			string action = null,
			Guid? adminId = null,
			DateTime? startTime = null,
			DateTime? endTime = null
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			(SessionVerificationResult verificationResult, AdminSession session) = await VerifyAdminSession(sessionId);
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					result.Add("error", "NoObjectWithId");
					return StatusCode(404, result);
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					result.Add("error", "NoObjectWithId");
					return StatusCode(500, result);
				}
				if (verificationResult == SessionVerificationResult.UserLoggedOut) {
					result.Add("error", "AdminLoggedOut");
					return StatusCode(403, result);
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					result.Add("error", "SessionExpired");
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-Admin");
					return StatusCode(401, result);
				}
				
				result.Add("error", "AuthError");
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-Admin");
				return StatusCode(401, result);
			}
			
			bool authorized = await VerifyAdminPermissions(session.Admin, "AuditLog.View");
			if (!authorized) {
				result.Add("error", "NotAuthorized");
				return StatusCode(403, result);
			}
			
			if (start.HasValue && start < 0) {
				start = 0;
			}
			
			if (count.HasValue && count <= 0) {
				count = Subtext.Config.pageSize;
			}
			
			result.Add("log", await context.AuditLog
				.OrderByDescending(ale => ale.Timestamp)
				.Where(ale => action != null ? ale.Action == action : true)
				.Where(ale => adminId.HasValue ? ale.AdminId == adminId : true)
				.Where(ale => startTime.HasValue ? ale.Timestamp >= startTime : true)
				.Where(ale => endTime.HasValue ? ale.Timestamp <= endTime : true)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Subtext.Config.pageSize, count.GetValueOrDefault(Subtext.Config.pageSize)))
				.Select(ale => new {ale.Id, ale.AdminId, ale.Action, ale.Details, ale.Timestamp})
				.ToListAsync());
			
			return StatusCode(200, result);
		}
	}
}
