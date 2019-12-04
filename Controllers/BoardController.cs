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
		public async Task<ActionResult> CreateBoard(
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
			
			if (await context.Boards.AnyAsync(b => b.Name == name)) {
				return StatusCode(409, new APIError("NameTaken"));
			}
			
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
		public async Task<ActionResult> CreateBoardDirect(
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
			string name1 = "!direct_" + recipientId;
			string name2 = "!direct_" + session.UserId;
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
			board.Name = "!direct_" + recipientId;
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
		
		[HttpGet("{boardId}")]
		public async Task<ActionResult> GetBoard(
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
			
			if (!await context.MemberRecords.AnyAsync(mr => mr.UserId == session.UserId && mr.Board == board)) {
				return StatusCode(403, new APIError("NotAuthorized"));
			}
			
			return StatusCode(200, new {board.Id, board.Name, board.OwnerId, board.Encryption, board.LastUpdate, board.LastSignificantUpdate});
		}
		
	}
}
