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
			
			// EntityFramework hates me
			if (!Config.IsInit) {
				Config.InitCreds();
			}
			
			services.AddDbContext<ChatContext>((DbContextOptionsBuilder opt) => {
				opt.UseSqlServer("Server=" + Config.sqlServer + ";Database=" + Config.sqlDatabase + ";User Id=" + Config.sqlUser + ";Password=" + Config.sqlPassword);
			});
			
			services.AddMvc((MvcOptions opt) => {
				opt.InputFormatters.Insert(0, new Subtext.Formatters.BinaryInputFormatter());
			});
			
			services.AddControllers();
		}
		
		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			}
			
			// app.UseHttpsRedirection();
			
			app.UseRouting();
			
			app.UseAuthorization();
			
			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
			});
		}
	}
}
