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
			await context.SaveChangesAsync();
			
			return StatusCode(201, board.Id);
		}
		
	}
}
