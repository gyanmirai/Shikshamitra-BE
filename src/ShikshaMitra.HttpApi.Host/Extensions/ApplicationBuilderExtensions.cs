using Microsoft.AspNetCore.Builder;
using ShikshaMitra.Common.Middleware;

namespace ShikshaMitra.Extensions;

public static class ApplicationBuilderExtensions
{
	public static IApplicationBuilder UseApiProblemDetails(this IApplicationBuilder app)
	{
		return app.UseMiddleware<ApiProblemDetailsMiddleware>();
	}

	/// <summary>
	/// Registers <see cref="DevAutoAuthMiddleware"/> for the current pipeline.
	/// Only call this inside an <c>IsDevelopment()</c> guard.
	/// </summary>
	public static IApplicationBuilder UseDevAutoAuth(this IApplicationBuilder app)
	{
		return app.UseMiddleware<DevAutoAuthMiddleware>();
	}
}
