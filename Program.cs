/* Subtext/Program.cs

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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Subtext {
	public class Program {
		public static readonly string version = "0.1.0";
		public static readonly string variant = "RetroAsgardian/Subtext";
		
		// HACK prevent migration when EF CLI tool hijacks ChatModel
		public static bool MainCalled { get; private set; }
		
		public static void Main(string[] args) {
			MainCalled = true;
			
			Console.WriteLine("Subtext - Copyright (C) 2019-2020 RetroAsgardian");
			Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY.");
			Console.WriteLine("This is free software, and you are welcome to redistribute it");
			Console.WriteLine("under certain conditions.");
			Console.WriteLine("For more details, see 'LICENSE.md'.");
			Console.WriteLine();
			
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
