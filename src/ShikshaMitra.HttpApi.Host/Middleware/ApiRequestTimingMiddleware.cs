using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.Diagnostics;

namespace ShikshaMitra.Common.Middleware;

public sealed class ApiRequestTimingMiddleware(RequestDelegate next,
	ILogger<ApiRequestTimingMiddleware> logger, IConfiguration configuration, IWebHostEnvironment? environment = null)
{
	private readonly bool _includePhases = ApiRequestTimingCheckpoints.IsEnabled(configuration, environment);
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
			var header = string.Format(CultureInfo.InvariantCulture,
				"app;dur={0:F2}, db;dur={1:F2}, db-open;dur={2:F2}",
				timing.ElapsedMilliseconds, timing.CommandMilliseconds, timing.ConnectionMilliseconds);
			if (_includePhases)
			{
				var phase = timing.ReadPhases();
				header += Metric("pre-auth", phase.BeforeAuth) + Metric("auth", phase.Auth) +
					Metric("post-auth", phase.AfterAuth) + Metric("endpoint", phase.Endpoint);
			}
			context.Response.Headers.Append("Server-Timing", header);
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

	private static string Metric(string name, double? milliseconds) => milliseconds.HasValue
		? string.Create(CultureInfo.InvariantCulture, $", {name};dur={milliseconds.Value:F2}")
		: "";

	private static double ReadThreshold(string? value) =>
		double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) &&
		double.IsFinite(threshold) && threshold >= 0 ? threshold : 2000;
}

public static class ApiRequestTimingCheckpoints
{
	public static bool IsEnabled(IConfiguration configuration, IWebHostEnvironment? environment) =>
		configuration.GetValue<bool?>("PerformanceDiagnostics:IncludeRequestPhases") ?? environment?.IsStaging() == true;

	public static IApplicationBuilder BeforeAuth(IApplicationBuilder app) =>
		Use(app, scope => scope.MarkBeforeAuth());

	public static IApplicationBuilder AfterAuth(IApplicationBuilder app) =>
		Use(app, scope => scope.MarkAfterAuth());

	public static IApplicationBuilder BeforeEndpoint(IApplicationBuilder app) =>
		Use(app, scope => scope.MarkBeforeEndpoint());

	private static IApplicationBuilder Use(IApplicationBuilder app, Action<ApiRequestTimingScope> record) =>
		app.Use(next => context =>
		{
			if (ApiRequestTimingScope.Current is { } scope) record(scope);
			return next(context);
		});
}
