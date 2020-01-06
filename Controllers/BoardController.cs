/* Subtext/Controllers/BoardController.cs

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
using System.Text.Json;

namespace Subtext.Controllers {
	[Produces("application/json")]
	[Route("/Subtext/board")]
	[ApiController]
	public class BoardController : ControllerBase {
		private readonly ChatContext context;
		
		static Regex reName = new Regex(@"^[a-z_][a-z0-9_]{4,}$", RegexOptions.Compiled);
		
		static RandomNumberGenerator rng = RandomNumberGenerator.Create();
		
		public BoardController(ChatContext context) {
			this.context = context;
		}
		
		[HttpPost("create")]
		public async Task<ActionResult> Create(
			Guid sessionId,
			string name,
			BoardEncryption encryption = BoardEncryption.GnuPG
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			/*
			if (await context.Boards.AnyAsync(b => b.Name == name)) {
				return StatusCode(409, new APIError("NameTaken"));
			}
			*/
			if (!reName.IsMatch(name)) {
				return StatusCode(400, new APIError("NameInvalid"));
			}
			
			Board board = new Board();
			board.Owner = session.User;
			board.Name = name;
			board.Encryption = encryption;
			
			await context.Boards.AddAsync(board);
			
			MemberRecord mr = new MemberRecord();
			mr.Board = board;
			mr.User = session.User;
			
			await context.MemberRecords.AddAsync(mr);
			
			await context.SaveChangesAsync();
			
			return StatusCode(201, board.Id);
		}
		
		[HttpPost("createdirect")]
		public async Task<ActionResult> CreateDirect(
			Guid sessionId,
			Guid recipientId
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			if (!await context.FriendRecords.AnyAsync(fr => fr.OwnerId == session.UserId && fr.FriendId == recipientId)) {
				return StatusCode(403, new APIError("NotFriends"));
			}
			
			// TODO better way to find DM boards, not reliant on the name
			string name1 = "!direct_" + recipientId + "_" + session.UserId;
			string name2 = "!direct_" + session.UserId + "_" + recipientId;
			IQueryable<Board> dmBoards = context.Boards.Where(b => b.IsDirect == true && (
				(b.OwnerId == session.UserId && b.Name == name1) ||
				(b.OwnerId == recipientId && b.Name == name2)
			));
			if (await dmBoards.AnyAsync()) {
				return StatusCode(200, (await dmBoards.FirstAsync()).Id);
			}
			
			Board board = new Board();
			board.IsDirect = true;
			board.Owner = session.User;
			board.Name = "!direct_" + recipientId + "_" + session.UserId;
			board.Encryption = BoardEncryption.GnuPG;
			
			await context.Boards.AddAsync(board);
			
			MemberRecord mr1 = new MemberRecord();
			mr1.Board = board;
			mr1.User = session.User;
			MemberRecord mr2 = new MemberRecord();
			mr2.Board = board;
			mr2.UserId = recipientId;
			
			await context.MemberRecords.AddAsync(mr1);
			await context.MemberRecords.AddAsync(mr2);
			
			await context.SaveChangesAsync();
			
			return StatusCode(201, board.Id);
		}
		
		[HttpGet("")]
		public async Task<ActionResult> GetBoards(
			Guid sessionId,
			int? start = null,
			int? count = null,
			bool? onlyOwned = null
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			if (onlyOwned.HasValue && onlyOwned.Value) {
				return StatusCode(200, await context.Boards
					.Where(b => b.OwnerId == session.UserId)
					.OrderBy(b => b.Id)
					.Skip(start.GetValueOrDefault(0))
					.Take(Math.Min(Config.pageSize, count.GetValueOrDefault(Config.pageSize)))
					.Select(b => new {b.Id, b.Name, b.OwnerId, b.Encryption, b.LastUpdate, b.LastSignificantUpdate})
					.ToListAsync());
			} else {
				return StatusCode(200, await context.MemberRecords
					.Where(mr => mr.UserId == session.UserId)
					.Join(context.Boards, mr => mr.BoardId, b => b.Id, (mr, b) => b)
					.OrderBy(b => b.Id)
					.Skip(start.GetValueOrDefault(0))
					.Take(Math.Min(Config.pageSize, count.GetValueOrDefault(Config.pageSize)))
					.Select(b => new {b.Id, b.Name, b.OwnerId, b.Encryption, b.LastUpdate, b.LastSignificantUpdate})
					.ToListAsync());
			}
		}
		
		[HttpGet("{boardId}")]
		public async Task<ActionResult> Get(
			Guid sessionId,
			Guid boardId
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			Board board = await context.Boards.FindAsync(boardId);
			if (board == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (!await context.MemberRecords.AnyAsync(mr => mr.UserId == session.UserId && mr.BoardId == board.Id)) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			return StatusCode(200, new {board.Id, board.Name, board.OwnerId, board.Encryption, board.LastUpdate, board.LastSignificantUpdate});
		}
		
		[HttpGet("{boardId}/members")]
		public async Task<ActionResult> GetMembers(
			Guid sessionId,
			Guid boardId,
			int? start = null,
			int? count = null
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			Board board = await context.Boards.FindAsync(boardId);
			if (board == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (!await context.MemberRecords.AnyAsync(mr => mr.UserId == session.UserId && mr.BoardId == boardId)) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			return StatusCode(200, await context.MemberRecords
				.Where(mr => mr.BoardId == boardId)
				.OrderBy(mr => mr.UserId)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Config.pageSize, count.GetValueOrDefault(Config.pageSize)))
				.Select(mr => mr.UserId)
				.ToListAsync());
		}
		
		[HttpPost("{boardId}/members")]
		public async Task<ActionResult> AddMember(
			Guid sessionId,
			Guid boardId,
			Guid userId
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			Board board = await context.Boards.FindAsync(boardId);
			if (board == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (board.Owner != session.User) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			User user = await context.Users.FindAsync(userId);
			if (user == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (await context.MemberRecords.AnyAsync(mr => mr.User == user && mr.Board == board)) {
				return StatusCode(409, new APIError("AlreadyAdded"));
			}
			
			MemberRecord mr = new MemberRecord();
			mr.Board = board;
			mr.User = user;
			
			await context.MemberRecords.AddAsync(mr);
			
			Message msg = new Message();
			msg.Board = board;
			msg.Author = null;
			msg.Timestamp = DateTime.UtcNow;
			msg.IsSystem = true;
			msg.Type = "AddMember";
			msg.Content = System.Text.Encoding.UTF8.GetBytes(userId.ToString());
			
			await context.Messages.AddAsync(msg);
			
			board.LastUpdate = DateTime.UtcNow;
			
			await context.SaveChangesAsync();
			return StatusCode(200, "success");
		}
		
		[HttpDelete("{boardId}/members/{userId}")]
		public async Task<ActionResult> RemoveMember(
			Guid sessionId,
			Guid boardId,
			Guid userId
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			Board board = await context.Boards.FindAsync(boardId);
			if (board == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (board.Owner != session.User) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			if (!await context.MemberRecords.AnyAsync(mr => mr.UserId == userId && mr.Board == board)) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			context.MemberRecords.Remove(await context.MemberRecords.FirstAsync(mr => mr.UserId == userId && mr.Board == board));
			
			Message msg = new Message();
			msg.Board = board;
			msg.Author = null;
			msg.Timestamp = DateTime.UtcNow;
			msg.IsSystem = true;
			msg.Type = "RemoveMember";
			msg.Content = System.Text.Encoding.UTF8.GetBytes(userId.ToString());
			
			await context.Messages.AddAsync(msg);
			
			board.LastUpdate = DateTime.UtcNow;
			
			await context.SaveChangesAsync();
			return StatusCode(200, "success");
		}
		
		[HttpGet("{boardId}/messages")]
		public async Task<ActionResult> GetMessages(
			Guid sessionId,
			Guid boardId,
			int? start = null,
			int? count = null,
			string type = null,
			bool onlySystem = false
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			Board board = await context.Boards.FindAsync(boardId);
			if (board == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (!await context.MemberRecords.AnyAsync(mr => mr.UserId == session.UserId && mr.BoardId == board.Id)) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			return StatusCode(200, await context.Messages
				.Where(m => m.BoardId == boardId)
				.OrderByDescending(m => m.Timestamp)
				.Where(m => type != null ? m.Type == type : true)
				.Where(m => onlySystem ? m.IsSystem : true)
				.Skip(start.GetValueOrDefault(0))
				.Take(Math.Min(Config.pageSize, count.GetValueOrDefault(Config.pageSize)))
				.Select(m => new {
					m.Id,
					m.Timestamp,
					m.AuthorId,
					m.IsSystem,
					m.Type,
					Content = m.Content.Length > Config.maxInlineMessageSize ? (byte[]) null : m.Content
				})
				.ToListAsync());
		}
		
		[HttpGet("{boardId}/messages/{messageId}")]
		[Produces("application/octet-stream", "application/json")]
		public async Task<ActionResult> GetMessage(
			Guid sessionId,
			Guid boardId,
			Guid messageId
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			Board board = await context.Boards.FindAsync(boardId);
			if (board == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (!await context.MemberRecords.AnyAsync(mr => mr.UserId == session.UserId && mr.BoardId == board.Id)) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			Message msg = await context.Messages.FindAsync(messageId);
			if (msg == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			if (msg.Board != board) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			var metadata = new {msg.Id, msg.Timestamp, msg.AuthorId, msg.IsSystem, msg.Type};
			Response.Headers.Add("X-Metadata", JsonSerializer.Serialize(metadata, metadata.GetType()));
			
			return StatusCode(200, msg.Content);
		}
		
		[HttpPost("{boardId}/messages")]
		public async Task<ActionResult> PostMessage(
			Guid sessionId,
			Guid boardId,
			[FromBody] byte[] content,
			bool isSystem = false,
			string type = "Message"
		) {
			(SessionVerificationResult verificationResult, Session session) = await new UserController(context).VerifyAndRenewSession(sessionId);
			
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
			
			Board board = await context.Boards.FindAsync(boardId);
			if (board == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			if (!await context.MemberRecords.AnyAsync(mr => mr.UserId == session.UserId && mr.BoardId == board.Id)) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			Message msg = new Message();
			msg.Board = board;
			msg.Author = session.User;
			msg.Timestamp = DateTime.UtcNow;
			msg.IsSystem = isSystem;
			msg.Type = type;
			msg.Content = content;
			
			await context.Messages.AddAsync(msg);
			
			board.LastUpdate = msg.Timestamp;
			if (!isSystem) {
				board.LastSignificantUpdate = msg.Timestamp;
				session.User.LastActive = msg.Timestamp;
			}
			
			await context.SaveChangesAsync();
			return StatusCode(201, msg.Id);
		}
	}
}
