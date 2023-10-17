using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;
using System;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace WindowsAuthAppToAADDemo
{
    public partial class Startup
    {
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static string aadInstance = EnsureTrailingSlash(ConfigurationManager.AppSettings["ida:AADInstance"]);
        private static string tenantId = ConfigurationManager.AppSettings["ida:TenantId"];
        private static string postLogoutRedirectUri = ConfigurationManager.AppSettings["ida:PostLogoutRedirectUri"];
        private static string redirectUri = ConfigurationManager.AppSettings["ida:redirectUri"];

        string authority = aadInstance + tenantId;

        public void ConfigureAuth(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions()
            {
                ExpireTimeSpan = TimeSpan.FromMinutes(2),
                //SlidingExpiration = true,
                Provider = new CookieAuthenticationProvider
                {
                    OnResponseSignIn = OnCustomResponseSignIn,
                    OnValidateIdentity = OnMyCustomValidateIdentity
                }
            });

            app.UseOpenIdConnectAuthentication(
            new OpenIdConnectAuthenticationOptions
            {
                ClientId = clientId,
                Authority = authority,
                RedirectUri = redirectUri,
                PostLogoutRedirectUri = postLogoutRedirectUri,
                Scope = OpenIdConnectScope.OpenIdProfile,
                ResponseType = OpenIdConnectResponseType.CodeIdToken,
                UseTokenLifetime = false,

                Notifications = new OpenIdConnectAuthenticationNotifications()
                {
                    AuthenticationFailed = (context) =>
                    {
                        return Task.FromResult(0);
                    },
                    SecurityTokenValidated = (context) =>
                    {
                        var claims = context.AuthenticationTicket.Identity.Claims;
                        var groups = from c in claims
                                        where c.Type == "groups"
                                        select c;
                        foreach (var group in groups)
                        {
                            context.AuthenticationTicket.Identity.AddClaim(new Claim(ClaimTypes.Role, group.Value));
                        }
                        return Task.FromResult(0);
                    }
                }

            }
            );


            // This makes any middleware defined above this line run before the Authorization rule is applied in web.config
            app.UseStageMarker(PipelineStage.Authenticate);
        }

        private void OnCustomResponseSignIn(CookieResponseSignInContext context)
        {
            //context.Properties.AllowRefresh = true;
            //context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(2);

            var ticks = context.Options.SystemClock.UtcNow.AddHours(10).UtcTicks;
            context.Properties.Dictionary.Add("absolute", ticks.ToString());
        }

        private Task OnMyCustomValidateIdentity(CookieValidateIdentityContext context)
        {
            bool reject = true;
            string value;
            if (context.Properties.Dictionary.TryGetValue("absolute", out value))
            {
                long ticks;
                if (Int64.TryParse(value, out ticks))
                {
                    reject = context.Options.SystemClock.UtcNow.UtcTicks > ticks;
                }
            }
            if (reject)
            {
                context.RejectIdentity();
                // optionally clear cookie
                //ctx.OwinContext.Authentication.SignOut(ctx.Options.AuthenticationType);
            }

            return Task.FromResult(0);
        }


        private static string EnsureTrailingSlash(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            if (!value.EndsWith("/", StringComparison.Ordinal))
            {
                return value + "/";
            }

            return value;
        }
    }
}
