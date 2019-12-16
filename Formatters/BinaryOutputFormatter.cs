/* Subtext/Formatters/BinaryOutputFormatter.cs

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
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Subtext.Formatters {
	public class BinaryOutputFormatter : OutputFormatter {
		
		public BinaryOutputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/octet-stream"));
		}

		public override Task WriteResponseBodyAsync(OutputFormatterWriteContext context) {
			byte[] data = (byte[]) context.Object;
			return context.HttpContext.Response.Body.WriteAsync(data, 0, data.Length);
		}

		protected override bool CanWriteType(Type type) {
			return type == typeof(byte[]);
		}
		
	}
}