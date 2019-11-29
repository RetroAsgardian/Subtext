using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Subtext {
	public class Program {
		public static readonly string version = "0.1.0";
		public static readonly string variant = "RetroAsgardian/Subtext";
		
		public static void Main(string[] args) {
			Config.Init();
			CreateHostBuilder(args).Build().Run();
		}
		
		public static IHostBuilder CreateHostBuilder(string[] args) {
			return Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder => {
					webBuilder.UseStartup<Startup>();
				});
		}
	}
}
