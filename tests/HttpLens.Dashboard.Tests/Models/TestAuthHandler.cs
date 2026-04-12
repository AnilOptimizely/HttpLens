using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace HttpLens.Dashboard.Tests.Models
{
    sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var userHeader) ||
                string.IsNullOrEmpty(userHeader.FirstOrDefault()))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.Name, userHeader.First()!) };

            if (Request.Headers.TryGetValue("X-Test-Role", out var roleHeader) &&
                !string.IsNullOrEmpty(roleHeader.FirstOrDefault()))
            {
                claims.Add(new Claim(ClaimTypes.Role, roleHeader.First()!));
            }

            var identity = new ClaimsIdentity(claims, "Test");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
