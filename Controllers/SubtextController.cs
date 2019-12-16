/* Subtext/Controllers/SubtextController.cs

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
using Microsoft.AspNetCore.Mvc;
using Subtext.Models;
using System.Security.Cryptography;

namespace Subtext.Controllers {
	[Produces("application/json")]
	[Route("/Subtext")]
	[ApiController]
	public class SubtextController : ControllerBase {
		private readonly ChatContext context;
		
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
		public ActionResult About() {
			Dictionary<string, object> result = new Dictionary<string, object>();
			
			result.Add("version", Subtext.Program.version);
			result.Add("variant", Subtext.Program.variant);
			
			result.Add("serverName", Subtext.Config.serverName);
			result.Add("serverIsPrivate", Subtext.Config.serverIsPrivate);
			
			result.Add("sessionDuration", Subtext.Config.sessionDuration.TotalSeconds);
			
			return StatusCode(200, result);
		}
	}
}
