/* Subtext/Startup.cs

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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Subtext.Models;

namespace Subtext {
	public class Startup {
		public Startup(IConfiguration configuration) {
			Configuration = configuration;
		}
		
		public IConfiguration Configuration { get; }
		
		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services) {
			
			// Entity Framework hates me
			if (!Config.IsInit) {
				Config.InitCreds();
			}
			
			services.AddDbContext<ChatContext>((DbContextOptionsBuilder opt) => {
				opt.UseSqlServer("Server=" + Config.sqlServer + ";Database=" + Config.sqlDatabase + ";User Id=" + Config.sqlUser + ";Password=" + Config.sqlPassword);
			});
			
			services.AddMvc((MvcOptions opt) => {
				opt.InputFormatters.Insert(0, new Subtext.Formatters.BinaryInputFormatter());
				opt.OutputFormatters.Insert(0, new Subtext.Formatters.BinaryOutputFormatter());
			});
			
			services.AddControllers();
		}
		
		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			}
			
			app.UseHttpsRedirection();
			
			app.UseRouting();
			
			app.UseAuthorization();
			
			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
			});
		}
	}
}
