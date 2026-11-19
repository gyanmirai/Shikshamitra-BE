using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Diagnostics;

namespace ShikshaMitra.Common.Middleware;

public sealed class ApiRequestTimingMiddleware(RequestDelegate next,
	ILogger<ApiRequestTimingMiddleware> logger, IConfiguration configuration)
{
	private readonly double _slowRequestMilliseconds = ReadThreshold(configuration["PerformanceDiagnostics:SlowRequestMilliseconds"]);

	public async Task InvokeAsync(HttpContext context)
	{
		if (!context.Request.Path.StartsWithSegments("/api"))
		{
			await next(context);
			return;
		}

		using var timing = ApiRequestTimingScope.Start();
		context.Response.OnStarting(() =>
		{
			// Timing at response start can be compared directly with the browser's TTFB.
			context.Response.Headers.Append("Server-Timing", string.Format(CultureInfo.InvariantCulture,
				"app;dur={0:F2}, db;dur={1:F2}, db-open;dur={2:F2}",
				timing.ElapsedMilliseconds, timing.CommandMilliseconds, timing.ConnectionMilliseconds));
			return Task.CompletedTask;
		});
		try
		{
			await next(context);
		}
		finally
		{
			if (timing.ElapsedMilliseconds >= _slowRequestMilliseconds)
			{
				// Route templates exclude query strings and applicant/document identifiers.
				var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "/api (unmatched)";
				logger.LogWarning("SlowApiRequest TraceId={TraceId} Method={Method} Route={Route} StatusCode={StatusCode} ElapsedMs={ElapsedMs} DbCommandCount={DbCommandCount} DbCommandMs={DbCommandMs} MaxDbCommandMs={MaxDbCommandMs} DbConnectionOpenMs={DbConnectionOpenMs}",
					context.TraceIdentifier, context.Request.Method, route, context.Response.StatusCode,
					timing.ElapsedMilliseconds, timing.CommandCount, timing.CommandMilliseconds,
					timing.MaxCommandMilliseconds, timing.ConnectionMilliseconds);
			}
		}
	}

	private static double ReadThreshold(string? value) =>
		double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) &&
		double.IsFinite(threshold) && threshold >= 0 ? threshold : 2000;
}
