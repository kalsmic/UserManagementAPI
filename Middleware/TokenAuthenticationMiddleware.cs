using System.Security.Cryptography;
using System.Text;

namespace UserManagementAPI.Middleware;

public sealed class TokenAuthenticationMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    ILogger<TokenAuthenticationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        var expectedToken = configuration["Authentication:ApiToken"];

        if (string.IsNullOrWhiteSpace(expectedToken) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            !TokensMatch(authorization["Bearer ".Length..].Trim(), expectedToken))
        {
            logger.LogWarning(
                "HTTP {Method} {Path} responded {StatusCode} because authentication failed",
                context.Request.Method,
                context.Request.Path,
                StatusCodes.Status401Unauthorized);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized." });
            return;
        }

        await next(context);
    }

    private static bool TokensMatch(string suppliedToken, string expectedToken)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
