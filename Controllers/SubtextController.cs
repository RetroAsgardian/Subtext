using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Subtext.Models;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Subtext.Controllers {
	[Produces("application/json")]
	[Route("/[controller]")]
	[ApiController]
	public class SubtextController : ControllerBase {
		private readonly ChatContext context;
		
		static Regex reName = new Regex(@"^[a-z_][a-z0-9_]{4,}$", RegexOptions.Compiled);
		
		static RandomNumberGenerator rng = RandomNumberGenerator.Create();
		
		static bool adminCheck = false;
		
		public SubtextController(ChatContext context) {
			this.context = context;
			if (!adminCheck) {
				if (context.Admins.Count() == 0) {
					Console.WriteLine("No admins exist, creating one...");
					Admin admin = new Admin();
					PermissionRecord permission = new PermissionRecord();
					permission.Admin = admin;
					permission.Action = "*";
					admin.Permissions = new List<PermissionRecord>();
					admin.Permissions.Add(permission);
					byte[] challenge = new byte[Subtext.Config.secretSize];
					rng.GetBytes(challenge);
					admin.Challenge = challenge;
					byte[] secret = new byte[Subtext.Config.secretSize];
					rng.GetBytes(secret);
					admin.Secret = secret;
					context.Admins.Add(admin);
					context.SaveChanges();
					Console.WriteLine(String.Format("Admin Credentials:\n\tID: {0}\n\tSecret: {1}", admin.Id, BitConverter.ToString(admin.Secret)));
				}
				adminCheck = true;
			}
		}
		
		[HttpGet("/")]
		public ContentResult Root() {
			return Content("Subtext");
		}
		
		[HttpGet("")]
		public ActionResult<Dictionary<string, object>> About() {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			result.Add("version", Subtext.Program.version);
			
			result.Add("serverName", Subtext.Config.serverName);
			result.Add("serverIsPrivate", Subtext.Config.serverIsPrivate);
			
			result.Add("sessionDuration", Subtext.Config.sessionDuration.TotalSeconds);
			
			return StatusCode(200, result);
		}
		
		[HttpGet("admin/login/challenge")]
		public async Task<ActionResult<Dictionary<string, object>>> AdminLoginChallenge(
			Guid adminId
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
			
			byte[] challenge = new byte[Subtext.Config.secretSize];
			rng.GetBytes(challenge);
			admin.Challenge = challenge;
			
			result.Add("challenge", challenge);
			
			await context.SaveChangesAsync();
			
			return StatusCode(200, result);
		}
		
		[HttpPost("admin/login/response")]
		public async Task<ActionResult<Dictionary<string, object>>> AdminLoginResponse(
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
		
		[HttpPost("admin/renew")]
		public async Task<ActionResult<Dictionary<string, object>>> AdminRenew(
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
		
		[HttpPost("admin/logout")]
		public async Task<ActionResult<Dictionary<string, object>>> AdminLogout(
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
		
		[HttpGet("admin/auditlog")]
		public async Task<ActionResult<Dictionary<string, object>>> AdminAuditLog(
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
		
		[HttpPost("user/create")]
		public async Task<ActionResult<Dictionary<string, object>>> UserCreate(
			string name,
			string password,
			byte[] publicKey
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			if (await context.Users.AnyAsync(u => u.Name == name)) {
				result.Add("error", "NameTaken");
				return StatusCode(409, result);
			}
			
			if (!reName.IsMatch(name)) {
				result.Add("error", "NameInvalid");
				return StatusCode(400, result);
			}
			
			if (password.Length < Subtext.Config.passwordMinLength) {
				result.Add("error", "PasswordInsecure");
				return StatusCode(400, result);
			}
			
			User user = new User();
			user.Name = name;
			
			byte[] salt = new byte[Subtext.Config.secretSize];
			rng.GetBytes(salt);
			
			byte[] pepper = new byte[1];
			rng.GetBytes(pepper);
			
			byte[] combinedSalt = salt.Concat(pepper).ToArray();
			
			Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, combinedSalt, Subtext.Config.pbkdf2Iterations);
			byte[] secret = pbkdf2.GetBytes(Subtext.Config.secretSize);
			
			user.Secret = secret;
			user.Salt = salt;
			
			if (Subtext.Config.serverIsPrivate) {
				user.IsLocked = true;
				user.LockReason = "AccountNotValidated";
				user.LockExpiry = DateTime.MaxValue;
			}
			
			await context.Users.AddAsync(user);
			
			PublicKey key = new PublicKey();
			key.Owner = user;
			key.PublishTime = DateTime.UtcNow;
			key.KeyData = publicKey;
			
			await context.PublicKeys.AddAsync(key);
			
			await context.SaveChangesAsync();
			
			result.Add("userId", user.Id);
			
			return StatusCode(200, result);
		}
		
		[HttpPost("user/queryIdByName")]
		public async Task<ActionResult<Dictionary<string, object>>> UserQueryIdByName(
			string name
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			if (!(await context.Users.AnyAsync(u => u.Name == name))) {
				result.Add("error", "NoObjectWithId");
				return StatusCode(404, result);
			}
			
			User user = await context.Users.FirstOrDefaultAsync(u => u.Name == name);
			
			result.Add("id", user.Id);
			
			return StatusCode(200, result);
		}
		
		[HttpPost("user/login")]
		public async Task<ActionResult<Dictionary<string, object>>> UserLogin(
			Guid userId,
			string password
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			User user = await context.Users.FindAsync(userId);
			if (user == null) {
				result.Add("error", "NoObjectWithId");
				return StatusCode(404, result);
			}
			
			if (user.IsDeleted) {
				result.Add("error", "ObjectDeleted");
				return StatusCode(410, result);
			}
			
			if (user.IsLocked) {
				if (user.LockExpiry < DateTime.MaxValue && DateTime.UtcNow >= user.LockExpiry) {
					user.IsLocked = false;
					await context.SaveChangesAsync();
				}
				result.Add("error", "UserLocked");
				result.Add("lockReason", user.LockReason);
				result.Add("lockExpiry", user.LockExpiry);
				return StatusCode(403, result);
			}
			
			bool passwordMatch = false;
			
			byte[] combinedSalt = new byte[Subtext.Config.secretSize + 1];
			user.Salt.CopyTo(combinedSalt, 0);
			
			for (int pepper = 0; pepper < 256; pepper++) {
				combinedSalt[combinedSalt.Length - 1] = (byte) pepper;
				Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, combinedSalt, Subtext.Config.pbkdf2Iterations);
				pbkdf2.Salt = combinedSalt;
				byte[] secret = pbkdf2.GetBytes(Subtext.Config.secretSize);
				if (secret.SequenceEqual(user.Secret)) {
					passwordMatch = true;
					break;
				}
			}
			
			if (!passwordMatch) {
				result.Add("error", "AuthError");
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, result);
			}
			
			Session session = new Session();
			session.User = user;
			session.Timestamp = DateTime.UtcNow;
			
			user.LastActive = DateTime.UtcNow;
			user.Presence = UserPresence.Online;
			
			await context.Sessions.AddAsync(session);
			await context.SaveChangesAsync();
			
			result.Add("sessionId", session.Id);
			return StatusCode(200, result);
		}
		
		async Task<(SessionVerificationResult, Session)> VerifySession(Guid sessionId) {
			Session session = await context.Sessions.FindAsync(sessionId);
			if (session == null) {
				return (SessionVerificationResult.SessionNotFound, null);
			}
			
			if (session.Timestamp + Subtext.Config.sessionDuration < DateTime.UtcNow) {
				context.Sessions.Remove(session);
				await context.SaveChangesAsync();
				return (SessionVerificationResult.SessionExpired, session);
			}
			
			await context.Entry(session).Reference(s => s.User).LoadAsync();
			User user = session.User;
			if (user == null) {
				// This should never happen
				return (SessionVerificationResult.UserNotFound, session);
			}
			
			return (SessionVerificationResult.Success, session);
		}
		
		[HttpPost("user/heartbeat")]
		public async Task<ActionResult<Dictionary<string, object>>> UserHeartbeat(
			Guid sessionId
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			(SessionVerificationResult verificationResult, Session session) = await VerifySession(sessionId);
			
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					result.Add("error", "NoObjectWithId");
					return StatusCode(404, result);
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					result.Add("error", "NoObjectWithId");
					return StatusCode(500, result);
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					result.Add("error", "SessionExpired");
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
					return StatusCode(401, result);
				}
				
				result.Add("error", "AuthError");
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, result);
			}
			
			session.Timestamp = DateTime.UtcNow;
			await context.SaveChangesAsync();
			
			result.Add("success", true);
			return StatusCode(200, result);
		}
		
	}
}
