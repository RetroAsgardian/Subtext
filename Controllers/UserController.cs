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
		public async Task<ActionResult> Create(
			string name,
			string password,
			[FromBody] byte[] publicKey
		) {
			if (await context.Users.AnyAsync(u => u.Name == name)) {
				return StatusCode(409, new APIError("NameTaken"));
			}
			
			if (!reName.IsMatch(name)) {
				return StatusCode(400, new APIError("NameInvalid"));
			}
			
			if (password.Length < Subtext.Config.passwordMinLength) {
				return StatusCode(400, new APIError("PasswordInsecure"));
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
			
			if (publicKey.Length > 0) {
				PublicKey key = new PublicKey();
				key.Owner = user;
				key.PublishTime = DateTime.UtcNow;
				key.KeyData = publicKey;
				
				await context.PublicKeys.AddAsync(key);
			}
			
			await context.SaveChangesAsync();
			
			return StatusCode(201, user.Id);
		}
		
		[HttpGet("queryidbyname")]
		public async Task<ActionResult> QueryIdByName(
			string name
		) {
			if (!(await context.Users.AnyAsync(u => u.Name == name))) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			User user = await context.Users.FirstOrDefaultAsync(u => u.Name == name);
			
			return StatusCode(200, user.Id);
		}
		
		[HttpPost("login")]
		public async Task<ActionResult> Login(
			Guid userId,
			string password
		) {
			User user = await context.Users.FindAsync(userId);
			if (user == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (user.IsDeleted) {
				return StatusCode(410, new APIError("ObjectDeleted"));
			}
			
			if (user.IsLocked) {
				if (user.LockExpiry < DateTime.MaxValue && DateTime.UtcNow >= user.LockExpiry) {
					user.IsLocked = false;
					await context.SaveChangesAsync();
				}
				return StatusCode(403, new {error = "UserLocked", lockReason = user.LockReason, lockExpiry = user.LockExpiry});
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
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			Session session = new Session();
			session.User = user;
			session.Timestamp = DateTime.UtcNow;
			
			user.LastActive = DateTime.UtcNow;
			user.Presence = UserPresence.Online;
			
			await context.Sessions.AddAsync(session);
			await context.SaveChangesAsync();
			
			return StatusCode(200, session.Id);
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
		public async Task<ActionResult> Heartbeat(
			Guid sessionId
		) {
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					return StatusCode(404, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					return StatusCode(500, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
					return StatusCode(401, new APIError("SessionExpired"));
				}
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			return StatusCode(200, "success");
		}
		
		[HttpPost("logout")]
		public async Task<ActionResult> Logout(
			Guid sessionId
		) {
			Session session = await context.Sessions.FindAsync(sessionId);
			if (session == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			await context.Entry(session).Reference(s => s.User).LoadAsync();
			
			User user = session.User;
			if (user == null) {
				return StatusCode(500, new APIError("NoObjectWithId"));
			}
			
			context.Sessions.Remove(session);
			
			DateTime expiryCutoff = DateTime.UtcNow.Subtract(Subtext.Config.sessionDuration);
			if (await context.Sessions.Where(s => s.UserId == user.Id && s.Timestamp >= expiryCutoff).CountAsync() == 0) {
				user.Presence = UserPresence.Offline;
			}
			
			await context.SaveChangesAsync();
			
			return StatusCode(200, "success");
		}
		
		[HttpGet("{userId}")]
		public async Task<ActionResult> GetUser(
			Guid sessionId,
			Guid userId
		) {
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					return StatusCode(404, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					return StatusCode(500, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
					return StatusCode(401, new APIError("SessionExpired"));
				}
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			User user = await context.Users.FindAsync(userId);
			if (user == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			return StatusCode(200, new {user.Id, user.Name, user.Presence, user.LastActive, user.Status, user.IsLocked, user.LockReason, user.LockExpiry, user.IsDeleted});
		}
		
		[HttpGet("{userId}/friends")]
		public async Task<ActionResult> GetUserFriends(
			Guid sessionId,
			Guid userId,
			int? start = null,
			int? count = null
		) {
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					return StatusCode(404, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					return StatusCode(500, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
					return StatusCode(401, new APIError("SessionExpired"));
				}
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			if (userId != session.UserId) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			if (start.HasValue && start < 0) {
				start = 0;
			}
			
			if (count.HasValue && count <= 0) {
				count = Subtext.Config.pageSize;
			}
			
			return StatusCode(200, await context.FriendRecords
				.Where(fr => fr.OwnerId == userId)
				.OrderBy(fr => fr.FriendId)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Subtext.Config.pageSize, count.GetValueOrDefault(Subtext.Config.pageSize)))
				.Select(fr => fr.FriendId)
				.ToListAsync());
		}
		
		[HttpGet("{userId}/blocked")]
		public async Task<ActionResult> GetUserBlocked(
			Guid sessionId,
			Guid userId,
			int? start = null,
			int? count = null
		) {
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					return StatusCode(404, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					return StatusCode(500, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
					return StatusCode(401, new APIError("SessionExpired"));
				}
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			if (userId != session.UserId) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			if (start.HasValue && start < 0) {
				start = 0;
			}
			
			if (count.HasValue && count <= 0) {
				count = Subtext.Config.pageSize;
			}
			
			return StatusCode(200, await context.BlockRecords
				.Where(br => br.OwnerId == userId)
				.OrderBy(br => br.BlockedId)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Subtext.Config.pageSize, count.GetValueOrDefault(Subtext.Config.pageSize)))
				.Select(br => br.BlockedId)
				.ToListAsync());
		}
		
		[HttpGet("{userId}/friendrequests")]
		public async Task<ActionResult> GetUserFriendRequests(
			Guid sessionId,
			Guid userId,
			int? start = null,
			int? count = null
		) {
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					return StatusCode(404, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					return StatusCode(500, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
					return StatusCode(401, new APIError("SessionExpired"));
				}
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			if (userId != session.UserId) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			if (start.HasValue && start < 0) {
				start = 0;
			}
			
			if (count.HasValue && count <= 0) {
				count = Subtext.Config.pageSize;
			}
			
			return StatusCode(200, await context.FriendRequests
				.Where(fr => fr.RecipientId == userId)
				.OrderBy(fr => fr.SenderId)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Subtext.Config.pageSize, count.GetValueOrDefault(Subtext.Config.pageSize)))
				.Select(fr => fr.SenderId)
				.ToListAsync());
		}
		
		[HttpGet("{userId}/keys")]
		public async Task<ActionResult> GetUserKeys(
			Guid sessionId,
			Guid userId,
			int? start = null,
			int? count = null
		) {
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					return StatusCode(404, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					return StatusCode(500, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
					return StatusCode(401, new APIError("SessionExpired"));
				}
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			User user = await context.Users.FindAsync(userId);
			if (user == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (start.HasValue && start < 0) {
				start = 0;
			}
			
			if (count.HasValue && count <= 0) {
				count = Subtext.Config.pageSize;
			}
			
			return StatusCode(200, await context.PublicKeys
				.Where(pk => pk.OwnerId == userId)
				.OrderByDescending(pk => pk.PublishTime)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Subtext.Config.pageSize, count.GetValueOrDefault(Subtext.Config.pageSize)))
				.Select(pk => new {pk.Id, pk.PublishTime})
				.ToListAsync());
		}
		
		[HttpPost("{userId}/keys")]
		public async Task<ActionResult> PostUserKey(
			Guid sessionId,
			Guid userId,
			[FromBody] byte[] publicKey
		) {
			(SessionVerificationResult verificationResult, Session session) = await VerifyAndRenewSession(sessionId);
			
			if (verificationResult != SessionVerificationResult.Success) {
				if (verificationResult == SessionVerificationResult.SessionNotFound) {
					return StatusCode(404, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.UserNotFound) {
					return StatusCode(500, new APIError("NoObjectWithId"));
				}
				if (verificationResult == SessionVerificationResult.SessionExpired) {
					Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
					return StatusCode(401, new APIError("SessionExpired"));
				}
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			if (userId != session.UserId) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			PublicKey key = new PublicKey();
			key.Owner = session.User;
			key.PublishTime = DateTime.UtcNow;
			key.KeyData = publicKey;
			
			await context.PublicKeys.AddAsync(key);
			await context.SaveChangesAsync();
			
			return StatusCode(201, key.Id);
		}
		
	}
}
