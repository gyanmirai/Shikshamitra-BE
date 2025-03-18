using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System;

namespace ShikshaMitra.Extensions;

public static class HttpContextExtensions
{
	public static bool TryGetCustomHeaderGuid(this HttpContext context, string key, out Guid value)
	{
		value = Guid.Empty;

		if (context.Items.TryGetValue("CustomHeaderGuids", out var headerObj) &&
			headerObj is Dictionary<string, Guid> headerGuids)
		{
			return headerGuids.TryGetValue(key, out value);
		}

		return false;
	}

	public static Guid GetCustomHeaderGuid(this HttpContext context, string key, Guid defaultValue = default)
	{
		if (context.TryGetCustomHeaderGuid(key, out var value))
		{
			return value;
		}

		return defaultValue;
	}
}
