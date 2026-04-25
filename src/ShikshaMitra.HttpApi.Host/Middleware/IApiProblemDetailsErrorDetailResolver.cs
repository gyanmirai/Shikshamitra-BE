using System.Collections.Generic;

namespace ShikshaMitra.Common.Middleware;

public interface IApiProblemDetailsErrorDetailResolver
{
	string ResolveDetail(
		string? detail,
		string? errorCode,
		string defaultDetail,
		IReadOnlyDictionary<string, object?>? errorData = null);
}
