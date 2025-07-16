using Microsoft.AspNetCore.Builder;
using ShikshaMitra.Common.Middleware;

namespace ShikshaMitra.Extensions;

public static class ApplicationBuilderExtensions
{
	public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
	{
		return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
	}
}