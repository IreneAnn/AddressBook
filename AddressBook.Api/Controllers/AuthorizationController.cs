using AddressBook.Application.DTO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace AddressBook.Api.Controllers
{
    public class AuthorizationController : Controller
    {
        
        [HttpPost("~/connect/token")]
        [Consumes("application/x-www-form-urlencoded")]
        [Produces("application/json")]
        public async Task<IActionResult> Exchange([FromForm] TokenRequestDto dto)
        {
            var request = HttpContext.GetOpenIddictServerRequest();
            
            if (request.IsClientCredentialsGrantType())
            {
                var identity = new ClaimsIdentity(
                    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                    nameType: OpenIddictConstants.Claims.Name,
                    roleType: OpenIddictConstants.Claims.Role);                              

                identity.AddClaim(OpenIddictConstants.Claims.Subject,
                    request.ClientId ?? throw new InvalidOperationException());

                identity.AddClaim(OpenIddictConstants.Claims.ClientId,
                    request.ClientId ?? throw new InvalidOperationException());

                // 🔹 Add scope claims (this is what ApiScope policy will check)
                foreach (var scope in request.GetScopes())
                {
                    identity.AddClaim(OpenIddictConstants.Claims.Scope, scope);
                }

                 // 🔹 Add issuer
                identity.AddClaim(JwtRegisteredClaimNames.Iss, "https://localhost:7255/"); // your issuer

                var principal = new ClaimsPrincipal(identity);
                principal.SetScopes(request.GetScopes());

                // 🔹 Set audience correctly
                principal.SetAudiences("addressbook.api");

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new NotImplementedException("The specified grant type is not implemented.");
        }
    }
}