# Middleware configuration

The API pipeline is configured in `Program.cs` in this order:

1. `ErrorHandlingMiddleware` catches unhandled exceptions and returns
   `{"error":"Internal server error."}` with status `500`.
2. `TokenAuthenticationMiddleware` requires an `Authorization: Bearer <token>`
   header before an endpoint can run. Invalid or missing tokens return
   `{"error":"Unauthorized."}` with status `401`.
3. `RequestLoggingMiddleware` records the HTTP method, path, final status code,
   and elapsed time through the application logger.
4. HTTPS redirection and endpoint execution run after the custom middleware.

The expected token is configured at `Authentication:ApiToken`. For deployment,
override it without editing source files:

```bash
export Authentication__ApiToken='a-long-random-production-token'
dotnet run
```

The development profile uses `techhive-development-token`, which is suitable
only for local testing. Do not use that value in production.

The development-only `/api/debug/throw` endpoint is included to verify the
exception middleware and returns the standardized `500` response when called
with a valid token.
