using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Common.Middleware;

internal static class ApiPermissionDeniedDetails
{
	private const string Recovery =
		"Ask your administrator to review your permissions and assigned data access, then try again.";
	internal const string DefaultDetail = "Access denied for this action. " + Recovery;
	private static readonly Regex Identifier = new("^[A-Za-z][A-Za-z0-9_]{0,99}$");
	private static readonly Regex WordBoundary = new("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");

	internal static void Apply(HttpContext context, ProblemDetails problem, ActionDescriptor? action = null)
	{
		if (problem.Status != StatusCodes.Status403Forbidden ||
			HasBusinessErrorCode(problem, "errorCode") || HasBusinessErrorCode(problem, "code") ||
			!IsGeneric(problem.Detail))
		{
			return;
		}

		// Controller metadata describes the request, not which permission or scope check failed.
		var descriptor = action as ControllerActionDescriptor ??
			context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
		var resource = Humanize(descriptor?.ControllerName);
		var operation = HumanizeAction(descriptor?.ActionName);
		problem.Detail = resource == null || operation == null
			? DefaultDetail
			: $"Access denied for {resource}: {operation}. {Recovery}";
	}

	private static bool HasBusinessErrorCode(ProblemDetails problem, string key) =>
		problem.Extensions.TryGetValue(key, out var code) && !string.IsNullOrWhiteSpace(code?.ToString()) &&
		!IsFrameworkAuthorizationCode(code?.ToString());

	internal static bool IsFrameworkAuthorizationCode(string? code) => code is
		AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted or
		AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName or
		AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedForGivenResource or
		AbpAuthorizationErrorCodes.GivenRequirementHasNotGrantedForGivenResource or
		AbpAuthorizationErrorCodes.GivenRequirementsHasNotGrantedForGivenResource;

	internal static bool IsGeneric(string? detail)
	{
		var message = detail?.Trim().Replace('\u2019', '\'');
		var sentence = message?.TrimEnd('.', '!');
		return string.IsNullOrWhiteSpace(message) ||
			string.Equals(message, DefaultDetail, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(sentence, "You don't have permission to access this resource", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(sentence, "You do not have permission to access this resource", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(sentence, "You are not authorized to access this resource", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(sentence, "Forbidden", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(sentence, "Access denied", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(sentence, "Authorization failed", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(message, "Exception of type 'Volo.Abp.Authorization.AbpAuthorizationException' was thrown.", StringComparison.Ordinal) ||
			string.Equals(sentence, "Authorization failed! Given policy has not granted", StringComparison.Ordinal) ||
			(message?.StartsWith("Authorization failed! Given policy has not granted:", StringComparison.Ordinal) ?? false) ||
			(message?.StartsWith("Authorization failed! Given policy has not granted for given resource:", StringComparison.Ordinal) ?? false) ||
			(message?.StartsWith("Authorization failed! Given requirement has not granted for given resource:", StringComparison.Ordinal) ?? false) ||
			(message?.StartsWith("Authorization failed! Given requirements has not granted for given resource:", StringComparison.Ordinal) ?? false);
	}

	private static string? HumanizeAction(string? name)
	{
		if (name?.EndsWith("Async", StringComparison.Ordinal) == true)
		{
			name = name[..^5];
		}

		return name switch
		{
			"Get" or "GetList" or "GetAll" or "List" => "View",
			"Create" or "Add" => "Add",
			"Update" or "Edit" or "Save" => "Update",
			"Delete" or "Remove" => "Delete",
			_ => Humanize(name)
		};
	}

	private static string? Humanize(string? name) =>
		name != null && Identifier.IsMatch(name) ? WordBoundary.Replace(name, " ").Replace('_', ' ') : null;
}
