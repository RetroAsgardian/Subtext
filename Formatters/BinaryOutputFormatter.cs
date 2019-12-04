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