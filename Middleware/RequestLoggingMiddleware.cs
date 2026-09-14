using System.Diagnostics;

namespace UserManagementAPI.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var unhandledException = false;

        try
        {
            await next(context);
        }
        catch
        {
            unhandledException = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                unhandledException ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
