/* Subtext/Controllers/UserController.cs

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
using System.Text.RegularExpressions;

namespace Subtext.Controllers {
	[Serializable]
	public enum SessionVerificationResult {
		Success,
		SessionNotFound,
		UserNotFound,
		SessionExpired,
		UserLoggedOut,
		OtherError
	}
	
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
			
			if (password.Length < Config.passwordMinLength) {
				return StatusCode(400, new APIError("PasswordInsecure"));
			}
			
			User user = new User();
			user.Name = name;
			
			byte[] salt = new byte[Config.secretSize];
			rng.GetBytes(salt);
			
			byte[] pepper = new byte[1];
			rng.GetBytes(pepper);
			
			byte[] combinedSalt = salt.Concat(pepper).ToArray();
			
			Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, combinedSalt, Config.pbkdf2Iterations);
			byte[] secret = pbkdf2.GetBytes(Config.secretSize);
			
			user.Secret = secret;
			user.Salt = salt;
			
			if (Config.instanceIsPrivate) {
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
			if (!await context.Users.AnyAsync(u => u.Name == name)) {
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
			
			byte[] combinedSalt = new byte[Config.secretSize + 1];
			user.Salt.CopyTo(combinedSalt, 0);
			
			for (int pepper = 0; pepper < 256; pepper++) {
				combinedSalt[combinedSalt.Length - 1] = (byte) pepper;
				Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, combinedSalt, Config.pbkdf2Iterations);
				pbkdf2.Salt = combinedSalt;
				byte[] secret = pbkdf2.GetBytes(Config.secretSize);
				if (secret.SequenceEqual(user.Secret)) {
					passwordMatch = true;
					break;
				}
			}
			
			if (!passwordMatch) {
				user.IncorrectGuesses += 1;
				if (user.IncorrectGuesses >= Config.passwordMaxAttempts) {
					user.IncorrectGuesses = 0;
					user.IsLocked = true;
					// Log the IP address
					user.LockReason = "TooManyPasswordAttempts " + Request.HttpContext.Connection.RemoteIpAddress.ToString();
					user.LockExpiry = DateTime.UtcNow.Add(Config.passwordLockTime);
				}
				await context.SaveChangesAsync();
				
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			Session session = new Session();
			session.User = user;
			session.Timestamp = DateTime.UtcNow;
			
			user.LastActive = DateTime.UtcNow;
			user.Presence = UserPresence.Online;
			user.IncorrectGuesses = 0;
			
			await context.Sessions.AddAsync(session);
			await context.SaveChangesAsync();
			
			return StatusCode(200, session.Id);
		}
		
		public async Task<(SessionVerificationResult, Session)> VerifySession(Guid sessionId) {
			Session session = await context.Sessions.FindAsync(sessionId);
			if (session == null) {
				return (SessionVerificationResult.SessionNotFound, null);
			}
			
			if (session.Timestamp + Config.sessionDuration < DateTime.UtcNow) {
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
		
		public async Task<(SessionVerificationResult, Session)> VerifyAndRenewSession(Guid sessionId) {
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
			
			DateTime expiryCutoff = DateTime.UtcNow.Subtract(Config.sessionDuration);
			if (await context.Sessions.Where(s => s.UserId == user.Id && s.Timestamp >= expiryCutoff).CountAsync() == 0) {
				user.Presence = UserPresence.Offline;
			}
			
			await context.SaveChangesAsync();
			
			return StatusCode(200, "success");
		}
		
		[HttpGet("{userId}")]
		public async Task<ActionResult> Get(
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
			
			if (user == session.User || await context.FriendRecords.AnyAsync(fr => fr.Owner == session.User && fr.Friend == user)) {
				return StatusCode(200, new {user.Id, user.Name, user.Presence, user.LastActive, user.Status, user.IsDeleted});
			} else {
				return StatusCode(200, new {user.Id, user.Name, user.IsDeleted});
			}
		}
		
		[HttpGet("{userId}/friends")]
		public async Task<ActionResult> GetFriends(
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
				count = Config.pageSize;
			}
			
			return StatusCode(200, await context.FriendRecords
				.Where(fr => fr.OwnerId == userId)
				.OrderBy(fr => fr.FriendId)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Config.pageSize, count.GetValueOrDefault(Config.pageSize)))
				.Select(fr => fr.FriendId)
				.ToListAsync());
		}
		
		[HttpDelete("{userId}/friends/{friendId}")]
		public async Task<ActionResult> RemoveFriend(
			Guid sessionId,
			Guid userId,
			Guid friendId
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
			
			if (!await context.FriendRecords.AnyAsync(fr => fr.OwnerId == userId && fr.FriendId == friendId)) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			FriendRecord fr1 = await context.FriendRecords.FirstAsync(fr => fr.OwnerId == userId && fr.FriendId == friendId);
			context.FriendRecords.Remove(fr1);
			
			if (await context.FriendRecords.AnyAsync(fr => fr.OwnerId == friendId && fr.FriendId == userId)) {
				FriendRecord fr2 = await context.FriendRecords.FirstAsync(fr => fr.OwnerId == friendId && fr.FriendId == userId);
				context.FriendRecords.Remove(fr2);
			}
			
			await context.SaveChangesAsync();
			return StatusCode(200, "success");
		}
		
		[HttpGet("{userId}/blocked")]
		public async Task<ActionResult> GetBlockedUsers(
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
				count = Config.pageSize;
			}
			
			return StatusCode(200, await context.BlockRecords
				.Where(br => br.OwnerId == userId)
				.OrderBy(br => br.BlockedId)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Config.pageSize, count.GetValueOrDefault(Config.pageSize)))
				.Select(br => br.BlockedId)
				.ToListAsync());
		}
		
		[HttpPost("{userId}/blocked")]
		public async Task<ActionResult> AddBlockedUser(
			Guid sessionId,
			Guid userId,
			Guid blockedId
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
			
			User blocked = await context.Users.FindAsync(blockedId);
			if (blocked == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			if (blocked == session.User) {
				return StatusCode(400, new APIError("InvalidRequest"));
			}
			
			if (await context.BlockRecords.AnyAsync(br => br.OwnerId == userId && br.BlockedId == blockedId)) {
				return StatusCode(409, new APIError("AlreadyBlocked"));
			}
			
			BlockRecord br = new BlockRecord();
			br.Owner = session.User;
			br.Blocked = blocked;
			
			await context.BlockRecords.AddAsync(br);
			await context.SaveChangesAsync();
			return StatusCode(200, "success");
		}
		
		[HttpDelete("{userId}/blocked/{blockedId}")]
		public async Task<ActionResult> RemoveBlockedUser(
			Guid sessionId,
			Guid userId,
			Guid blockedId
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
			
			if (!await context.BlockRecords.AnyAsync(br => br.OwnerId == userId && br.BlockedId == blockedId)) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			BlockRecord br = await context.BlockRecords.FirstAsync(br => br.OwnerId == userId && br.BlockedId == blockedId);
			context.BlockRecords.Remove(br);
			await context.SaveChangesAsync();
			return StatusCode(200, "success");
		}
		
		[HttpGet("{userId}/friendrequests")]
		public async Task<ActionResult> GetFriendRequests(
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
				count = Config.pageSize;
			}
			
			return StatusCode(200, await context.FriendRequests
				.Where(fr => fr.RecipientId == userId)
				.OrderBy(fr => fr.SenderId)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Config.pageSize, count.GetValueOrDefault(Config.pageSize)))
				.Select(fr => fr.SenderId)
				.ToListAsync());
		}
		
		[HttpPost("{userId}/friendrequests")]
		public async Task<ActionResult> SendFriendRequest(
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
			if (user == session.User) {
				return StatusCode(400, new APIError("InvalidRequest"));
			}
			
			if (await context.FriendRecords.AnyAsync(fr => fr.OwnerId == session.UserId && fr.FriendId == userId)) {
				return StatusCode(409, new APIError("AlreadyFriends"));
			}
			if (await context.FriendRequests.AnyAsync(fr => fr.SenderId == session.UserId && fr.RecipientId == userId)) {
				return StatusCode(409, new APIError("AlreadySent"));
			}
			
			FriendRequest request = new FriendRequest();
			request.Sender = session.User;
			request.Recipient = user;
			
			await context.FriendRequests.AddAsync(request);
			await context.SaveChangesAsync();
			
			return StatusCode(201, "success");
		}
		
		[HttpPost("{userId}/friendrequests/{senderId}")]
		public async Task<ActionResult> AcceptFriendRequest(
			Guid sessionId,
			Guid userId,
			Guid senderId
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
			
			if (!await context.FriendRequests.AnyAsync(fr => fr.SenderId == senderId && fr.RecipientId == userId)) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			FriendRequest request = await context.FriendRequests.FirstAsync(fr => fr.SenderId == senderId && fr.RecipientId == userId);
			await context.Entry(request).Reference(fr => fr.Sender).LoadAsync();
			
			FriendRecord fr1 = new FriendRecord();
			fr1.Owner = session.User;
			fr1.Friend = request.Sender;
			
			FriendRecord fr2 = new FriendRecord();
			fr2.Owner = request.Sender;
			fr2.Friend = session.User;
			
			await context.FriendRecords.AddAsync(fr1);
			await context.FriendRecords.AddAsync(fr2);
			context.FriendRequests.Remove(request);
			await context.SaveChangesAsync();
			
			return StatusCode(200, "success");
		}
		
		[HttpDelete("{userId}/friendrequests/{senderId}")]
		public async Task<ActionResult> RejectFriendRequest(
			Guid sessionId,
			Guid userId,
			Guid senderId
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
			
			if (!await context.FriendRequests.AnyAsync(fr => fr.SenderId == senderId && fr.RecipientId == userId)) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			FriendRequest request = await context.FriendRequests.FirstAsync(fr => fr.SenderId == senderId && fr.RecipientId == userId);
			context.FriendRequests.Remove(request);
			await context.SaveChangesAsync();
			
			return StatusCode(200, "success");
		}
		
		[HttpGet("{userId}/keys")]
		public async Task<ActionResult> GetKeys(
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
				count = Config.pageSize;
			}
			
			return StatusCode(200, await context.PublicKeys
				.Where(pk => pk.OwnerId == userId)
				.OrderByDescending(pk => pk.PublishTime)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Config.pageSize, count.GetValueOrDefault(Config.pageSize)))
				.Select(pk => new {pk.Id, pk.PublishTime})
				.ToListAsync());
		}
		
		[HttpPost("{userId}/keys")]
		public async Task<ActionResult> AddKey(
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
		
		[HttpPut("{userId}/presence")]
		public async Task<ActionResult> SetPresence(
			Guid sessionId,
			Guid userId,
			UserPresence presence,
			DateTime? untilTime = null,
			string otherData = ""
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
			
			if (presence == UserPresence.Online) {
				// "Away due to inactivity" crap is handled on the client because yes
				session.User.Presence = UserPresence.Online;
				session.User.Status = otherData;
				
				await context.SaveChangesAsync();
				return StatusCode(200, "success");
			} else if (presence == UserPresence.Away) {
				if (!untilTime.HasValue) {
					return StatusCode(400, new APIError("InvalidRequest"));
				}
				
				session.User.Presence = UserPresence.Away;
				session.User.Status = untilTime.Value.ToString("O") + ";" + otherData;
				
				await context.SaveChangesAsync();
				return StatusCode(200, "success");
			} else if (presence == UserPresence.Busy) {
				if (!untilTime.HasValue) {
					return StatusCode(400, new APIError("InvalidRequest"));
				}
				
				session.User.Presence = UserPresence.Busy;
				session.User.Status = untilTime.Value.ToString("O") + ";" + otherData;
				
				await context.SaveChangesAsync();
				return StatusCode(200, "success");
			} else {
				return StatusCode(400, new APIError("InvalidRequest"));
			}
		}
		
		[HttpDelete("{userId}")]
		public async Task<ActionResult> Delete(
			Guid sessionId,
			Guid userId,
			string password
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
			
			User user = session.User;
			
			bool passwordMatch = false;
			
			byte[] combinedSalt = new byte[Config.secretSize + 1];
			user.Salt.CopyTo(combinedSalt, 0);
			
			for (int pepper = 0; pepper < 256; pepper++) {
				combinedSalt[combinedSalt.Length - 1] = (byte) pepper;
				Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, combinedSalt, Config.pbkdf2Iterations);
				pbkdf2.Salt = combinedSalt;
				byte[] secret = pbkdf2.GetBytes(Config.secretSize);
				if (secret.SequenceEqual(user.Secret)) {
					passwordMatch = true;
					break;
				}
			}
			
			if (!passwordMatch) {
				Response.Headers.Add("WWW-Authenticate", "X-Subtext-User");
				return StatusCode(401, new APIError("AuthError"));
			}
			
			// Mark as deleted
			user.IsDeleted = true;
			
			// Delete secret and presence data
			user.Salt = null;
			user.Secret = null;
			user.Name = user.Name + "!deleted_" + DateTime.UtcNow.ToString("yyyyMMdd");
			user.LastActive = DateTime.MinValue;
			user.Presence = UserPresence.Offline;
			user.Status = "";
			
			// Close sessions
			await context.Sessions.Where(s => s.UserId == user.Id).ForEachAsync(s => {
				context.Sessions.Remove(s);
			});
			
			await context.SaveChangesAsync();
			
			return StatusCode(200, "success");
		}
		
	}
}
