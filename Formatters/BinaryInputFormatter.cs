using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Subtext.Formatters {
	public class BinaryInputFormatter : InputFormatter {
		
		public BinaryInputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/octet-stream"));
		}
		
		public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context) {
			using (MemoryStream stream = new MemoryStream(4096)) {
				await context.HttpContext.Request.Body.CopyToAsync(stream);
				return await InputFormatterResult.SuccessAsync(stream.ToArray());
			}
		}
		
		protected override bool CanReadType(Type type) {
			return type == typeof(byte[]);
		}
		
	}
}