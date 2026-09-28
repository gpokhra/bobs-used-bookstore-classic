using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;

namespace Bookstore.Web
{
    public static class AuthenticationConfig
    {
        public static IServiceCollection AddBookstoreAuthentication(
            this IServiceCollection services, string authMode)
        {
            if (authMode == "aws")
            {
                AddCognitoAuthentication(services);
            }
            else
            {
                AddLocalAuthentication(services);
            }

            return services;
        }

        public static IApplicationBuilder UseBookstoreAuthentication(
            this IApplicationBuilder app)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            return app;
        }

        private static void AddLocalAuthentication(IServiceCollection services)
        {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Authentication/Login";
                    options.Cookie.Name = "LocalAuthentication";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                });
        }

        private static void AddCognitoAuthentication(IServiceCollection services)
        {
            var config = ConfigurationManager.Configuration;
            var metadataAddress = config["Authentication:Cognito:MetadataAddress"]
                                  ?? config["Authentication/Cognito/MetadataAddress"];
            var clientId = config["Authentication:Cognito:LocalClientId"]
                           ?? config["Authentication/Cognito/LocalClientId"];

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = ".AspNet.Cookies";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            })
            .AddOpenIdConnect(options =>
            {
                options.ClientId = clientId;
                options.MetadataAddress = metadataAddress;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UseTokenLifetime = false;
                options.SaveTokens = true;
                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = ctx =>
                    {
                        if (ctx.ProtocolMessage.RedirectUri?.StartsWith("http://") == true)
                        {
                            ctx.ProtocolMessage.RedirectUri =
                                ctx.ProtocolMessage.RedirectUri.Replace("http://", "https://");
                        }
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    OnRemoteFailure = ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.Redirect("/Home/Error");
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                };
            });
        }
    }
}
