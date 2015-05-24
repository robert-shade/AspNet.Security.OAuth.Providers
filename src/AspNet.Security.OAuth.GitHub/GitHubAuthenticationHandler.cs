﻿using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Authentication;
using Microsoft.AspNet.Authentication.OAuth;
using Microsoft.AspNet.Http.Authentication;
using Microsoft.Framework.Internal;
using Newtonsoft.Json.Linq;

namespace AspNet.Security.OAuth.GitHub {
    public class GitHubAuthenticationHandler : OAuthAuthenticationHandler<GitHubAuthenticationOptions, GitHubAuthenticationNotifications> {
        public GitHubAuthenticationHandler([NotNull] HttpClient client)
            : base(client) {
        }

        protected override async Task<AuthenticationTicket> GetUserInformationAsync(
            [NotNull] AuthenticationProperties properties, [NotNull] TokenResponse tokens) {
            var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInformationEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await Backchannel.SendAsync(request, Context.RequestAborted);
            response.EnsureSuccessStatusCode();

            var payload = JObject.Parse(await response.Content.ReadAsStringAsync());

            var identity = new ClaimsIdentity(Options.AuthenticationScheme);
            var notification = new GitHubAuthenticatedNotification(Context, Options, payload, tokens);
                    
            if (!string.IsNullOrEmpty(notification.Identifier)) {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, notification.Identifier,
                                            ClaimValueTypes.String, Options.ClaimsIssuer));
            }
                    
            if (!string.IsNullOrEmpty(notification.Login)) {
                identity.AddClaim(new Claim(ClaimTypes.Name, notification.Login,
                                            ClaimValueTypes.String, Options.ClaimsIssuer));
            }
                    
            if (!string.IsNullOrEmpty(notification.Name)) {
                identity.AddClaim(new Claim("urn:github:name", notification.Name,
                                            ClaimValueTypes.String, Options.ClaimsIssuer));
            }
                    
            if (!string.IsNullOrEmpty(notification.Link)) {
                identity.AddClaim(new Claim("urn:github:url", notification.Link,
                                            ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            if (Options.SaveTokens) {
                identity.AddClaim(new Claim("access_token", notification.AccessToken,
                                            ClaimValueTypes.String, Options.ClaimsIssuer));

                if (!string.IsNullOrEmpty(notification.RefreshToken)) {
                    identity.AddClaim(new Claim("refresh_token", notification.RefreshToken,
                                                ClaimValueTypes.String, Options.ClaimsIssuer));
                }

                if (!string.IsNullOrEmpty(notification.TokenType)) {
                    identity.AddClaim(new Claim("token_type", notification.TokenType,
                                                ClaimValueTypes.String, Options.ClaimsIssuer));
                }

                if (notification.ExpiresIn.HasValue) {
                    identity.AddClaim(new Claim("expires_in", notification.ExpiresIn.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture),
                                                ClaimValueTypes.String, Options.ClaimsIssuer));
                }
            }

            notification.Properties = properties;
            notification.Principal = new ClaimsPrincipal(identity);

            await Options.Notifications.Authenticated(notification);
                    
            return new AuthenticationTicket(
                notification.Principal, notification.Properties,
                notification.Options.AuthenticationScheme);
        }
    }
}