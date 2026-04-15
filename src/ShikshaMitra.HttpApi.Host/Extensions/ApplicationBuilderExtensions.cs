using Microsoft.AspNetCore.Builder;
using ShikshaMitra.Common.Middleware;

namespace ShikshaMitra.Extensions;

public static class ApplicationBuilderExtensions
{
	public static IApplicationBuilder UseApiProblemDetails(this IApplicationBuilder app)
	{
		return app.UseMiddleware<ApiProblemDetailsMiddleware>();
	}
}
