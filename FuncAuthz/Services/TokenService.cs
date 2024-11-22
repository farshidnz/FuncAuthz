using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FuncAuthz.Services;

interface ITokenService
{
    Task<ClaimsPrincipal?> ValidateTokenAsync(string? token);
}

internal class TokenService(AuthorizationOptions authenticationOptions) : ITokenService
{
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string? token)
    {
        try
        {
            var tokenHandler = new JsonWebTokenHandler();
            var tokenValidationResult =
                await tokenHandler.ValidateTokenAsync(token, authenticationOptions.TokenValidationParameters);

            if (tokenValidationResult.IsValid == false)
            {
                return null;
            }

            return new ClaimsPrincipal(tokenValidationResult.ClaimsIdentity);
        }
        catch (Exception)
        {
            return null;
        }
    }
}