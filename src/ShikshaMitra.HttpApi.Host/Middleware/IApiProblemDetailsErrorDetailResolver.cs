namespace ShikshaMitra.Common.Middleware;

public interface IApiProblemDetailsErrorDetailResolver
{
	string ResolveDetail(string? detail, string? errorCode, string defaultDetail);
}
