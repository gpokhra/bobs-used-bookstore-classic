using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bookstore.Web
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
            ConfigurationManager.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            // Register all bookstore services, repositories, DbContext
            var authMode = Configuration["Services:Authentication"]
                           ?? Configuration["Services/Authentication"]
                           ?? "local";

            var loggingMode = Configuration["Services:LoggingService"]
                              ?? Configuration["Services/LoggingService"]
                              ?? "local";

            LoggingSetup.ConfigureNLog(loggingMode);

            services.AddBookstoreAuthentication(authMode);

            services.AddBookstoreServices(_env);

            // Session support (used by shopping cart correlation ID)
            services.AddSession();
            services.AddHttpContextAccessor();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseSession();

            app.UseBookstoreAuthentication();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "admin",
                    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }

    /// <summary>
    /// Static accessor so legacy-style helpers that can't take IConfiguration
    /// via DI (e.g. AuthenticationSetup, DependencyInjectionSetup) can still
    /// read configuration. Set once at startup before any service registration.
    /// </summary>
    public static class ConfigurationManager
    {
        public static IConfiguration Configuration { get; set; }
    }
}
