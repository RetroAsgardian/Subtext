/* Subtext/Controllers/KeyController.cs

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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Subtext.Models;
using System.Text.Json;

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
		[Produces("application/octet-stream", "application/json")]
		public async Task<ActionResult> Get(
			Guid keyId
		) {
			PublicKey key = await context.PublicKeys.FindAsync(keyId);
			if (key == null) {
				return StatusCode(404, new APIError("NoObjectWithId"));
			}
			
			var metadata = new {key.Id, key.PublishTime, key.OwnerId};
			Response.Headers.Add("X-Metadata", JsonSerializer.Serialize(metadata, metadata.GetType()));
			
			return StatusCode(200, key.KeyData);
		}
		
	}
}
