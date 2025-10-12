using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace ShikshaMitra.Middleware;

public class CustomHeaderMiddleware : ITransientDependency
{
	private readonly RequestDelegate _next;
	private const string HEADER_NAME = "X-Custom-Header";

	public CustomHeaderMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		if (context.Request.Headers.TryGetValue(HEADER_NAME, out var headerValues))
		{
			var headerValue = headerValues.FirstOrDefault();

			if (!string.IsNullOrEmpty(headerValue))
			{
				// Parse the header
				var headerKvp = ParseCustomHeader(headerValue);

				// Store string values in context
				context.Items["CustomHeaderStrings"] = headerKvp;

				// Parse and store GUIDs
				var guidValues = new Dictionary<string, Guid>();

				foreach (var item in headerKvp)
				{
					if (Guid.TryParse(item.Value, out Guid guidValue))
					{
						guidValues[item.Key] = guidValue;
					}
				}

				context.Items["CustomHeaderGuids"] = guidValues;
			}
		}

		await _next(context);
	}

	private Dictionary<string, string> ParseCustomHeader(string headerValue)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		if (string.IsNullOrEmpty(headerValue))
			return result;

		var parts = headerValue.Split(';', StringSplitOptions.RemoveEmptyEntries)
							  .Select(part => part.Trim());

		foreach (var part in parts)
		{
			var keyValue = part.Split('=', 2);
			if (keyValue.Length == 2)
			{
				result[keyValue[0].Trim()] = keyValue[1].Trim();
			}
		}

		return result;
	}
}

// Extension method for middleware registration
public static class CustomHeaderMiddlewareExtensions
{
	public static IApplicationBuilder UseCustomHeader(this IApplicationBuilder builder)
	{
		return builder.UseMiddleware<CustomHeaderMiddleware>();
	}
}



