using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Subtext.Models;

namespace Subtext.Controllers {
	[Produces("application/json")]
	[Route("/Subtext/key")]
	[ApiController]
	public class KeyController : ControllerBase {
		private readonly ChatContext context;
		
		public KeyController(ChatContext context) {
			this.context = context;
		}
		
		[HttpGet("{keyId}")]
		[Produces("application/octet-stream")]
		public async Task<ActionResult> GetKeyData(
			Guid keyId
		) {
			PublicKey key = await context.PublicKeys.FindAsync(keyId);
			if (key == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			return StatusCode(200, key.KeyData);
		}
		
	}
}
