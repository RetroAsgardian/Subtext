using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Subtext.Models;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Subtext.Controllers {
	[Produces("application/json")]
	[Route("/Subtext/user")]
	[ApiController]
	public class UserController : ControllerBase {
		private readonly ChatContext context;
		
		static Regex reName = new Regex(@"^[a-z_][a-z0-9_]{4,}$", RegexOptions.Compiled);
		
		static RandomNumberGenerator rng = RandomNumberGenerator.Create();
		
		public UserController(ChatContext context) {
			this.context = context;
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
		
		[HttpPost("create")]
		public async Task<ActionResult<Dictionary<string, object>>> Create(
			string name,
			string password
			// byte[] publicKey = null
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
			
			/* if (publicKey != null) {
				PublicKey key = new PublicKey();
				key.Owner = user;
				key.PublishTime = DateTime.UtcNow;
				key.KeyData = publicKey;
				
				await context.PublicKeys.AddAsync(key);
			} */
			
			await context.SaveChangesAsync();
			
			result.Add("userId", user.Id);
			
			return StatusCode(201, result);
		}
		
		[HttpGet("queryIdByName")]
		public async Task<ActionResult<Dictionary<string, object>>> QueryIdByName(
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
		
		[HttpPost("login")]
		public async Task<ActionResult<Dictionary<string, object>>> Login(
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
		
		async Task<(SessionVerificationResult, Session)> VerifyAndRenewSession(Guid sessionId) {
			(SessionVerificationResult verificationResult, Session session) = await VerifySession(sessionId);
			if (verificationResult == SessionVerificationResult.Success) {
				session.Timestamp = DateTime.UtcNow;
				await context.SaveChangesAsync();
			}
			return (verificationResult, session);
		}
		
		[HttpPost("heartbeat")]
		public async Task<ActionResult<Dictionary<string, object>>> Heartbeat(
			Guid sessionId
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
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
			
			result.Add("success", true);
			return StatusCode(200, result);
		}
		
		[HttpPost("logout")]
		public async Task<ActionResult<Dictionary<string, object>>> Logout(
			Guid sessionId
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			Session session = await context.Sessions.FindAsync(sessionId);
			if (session == null) {
				result.Add("error", "NoObjectWithId");
				return StatusCode(404, result);
			}
			
			await context.Entry(session).Reference(s => s.User).LoadAsync();
			
			User user = session.User;
			if (user == null) {
				result.Add("error", "NoObjectWithId");
				return StatusCode(500, result);
			}
			
			context.Sessions.Remove(session);
			
			DateTime expiryCutoff = DateTime.UtcNow.Subtract(Subtext.Config.sessionDuration);
			if (await context.Sessions.Where(s => s.UserId == user.Id && s.Timestamp >= expiryCutoff).CountAsync() == 0) {
				user.Presence = UserPresence.Offline;
			}
			
			await context.SaveChangesAsync();
			
			result.Add("success", true);
			return StatusCode(200, result);
		}
		
		[HttpGet("{userId}")]
		public async Task<ActionResult<Dictionary<string, object>>> GetUser(
			Guid sessionId,
			Guid userId
		) {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
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
			
			User user = await context.Users.FindAsync(userId);
			if (user == null) {
				result.Add("error", "NoObjectWithId");
				return StatusCode(404, result);
			}
			
			result.Add("user", new {user.Id, user.Name, user.Presence, user.LastActive, user.Status, user.IsLocked, user.LockReason, user.LockExpiry, user.IsDeleted});
			
			return StatusCode(200, result);
		}
		
	}
}
