using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using ShikshaMitra.HR;
using ShikshaMitra.Localization;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Common.Middleware;

public class ApiProblemDetailsErrorDetailResolver : IApiProblemDetailsErrorDetailResolver, ITransientDependency
{
	private static readonly IReadOnlyDictionary<string, string> HrCodeToName = typeof(HRErrorCodes)
		.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
		.Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
		.Select(field => new { Name = field.Name, Value = field.GetRawConstantValue() as string })
		.Where(item => !string.IsNullOrWhiteSpace(item.Value))
		.GroupBy(item => item.Value!, StringComparer.OrdinalIgnoreCase)
		.ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

	private readonly IStringLocalizer<ShikshaMitraResource> _localizer;

	public ApiProblemDetailsErrorDetailResolver(IStringLocalizer<ShikshaMitraResource> localizer)
	{
		_localizer = localizer;
	}

	public string ResolveDetail(
		string? detail,
		string? errorCode,
		string defaultDetail,
		IReadOnlyDictionary<string, object?>? errorData = null)
	{
		var trimmedDetail = detail?.Trim();
		var isStatusDefaultDetail = !string.IsNullOrWhiteSpace(trimmedDetail) &&
		                            string.Equals(trimmedDetail, defaultDetail.Trim(), StringComparison.OrdinalIgnoreCase);

		if (!ApiProblemDetailsErrorMessages.ShouldPreferCodeMessage(detail) && !isStatusDefaultDetail)
		{
			return trimmedDetail!;
		}

		if (!string.IsNullOrWhiteSpace(errorCode))
		{
			var localized = _localizer[errorCode];
			if (!localized.ResourceNotFound && !string.IsNullOrWhiteSpace(localized.Value))
			{
				return FormatTemplate(localized.Value, errorData);
			}

			var fallback = BuildFallbackDetail(errorCode, errorData);
			if (!string.IsNullOrWhiteSpace(fallback))
			{
				return fallback;
			}

			var knownCodeFallback = BuildKnownCodeFallback(errorCode);
			if (!string.IsNullOrWhiteSpace(knownCodeFallback))
			{
				return knownCodeFallback;
			}
		}

		return defaultDetail;
	}

	private static string FormatTemplate(string template, IReadOnlyDictionary<string, object?>? data)
	{
		if (data == null || data.Count == 0)
		{
			return template;
		}

		var formatted = template;
		foreach (var pair in data)
		{
			var placeholder = "{" + pair.Key + "}";
			var value = pair.Value?.ToString() ?? string.Empty;
			formatted = formatted.Replace(placeholder, value, StringComparison.OrdinalIgnoreCase);
		}

		return formatted;
	}

	private static string? BuildFallbackDetail(string errorCode, IReadOnlyDictionary<string, object?>? data)
	{
		if (!string.Equals(errorCode, HRErrorCodes.LeaveInvalidMonthForAcademicYear, StringComparison.Ordinal))
		{
			return null;
		}

		var requestedMonth = TryReadValue(data, "requestedMonth");
		var academicYearName = TryReadValue(data, "academicYearName");
		var academicYearStart = TryReadValue(data, "academicYearStart");
		var academicYearEnd = TryReadValue(data, "academicYearEnd");

		if (!string.IsNullOrWhiteSpace(requestedMonth) &&
		    !string.IsNullOrWhiteSpace(academicYearStart) &&
		    !string.IsNullOrWhiteSpace(academicYearEnd))
		{
			if (!string.IsNullOrWhiteSpace(academicYearName))
			{
				return
					$"Requested month '{requestedMonth}' is outside academic year '{academicYearName}' ({academicYearStart} to {academicYearEnd}).";
			}

			return
				$"Requested month '{requestedMonth}' is outside the selected academic year ({academicYearStart} to {academicYearEnd}).";
		}

		return "Selected month is outside the given academic year.";
	}

	private static string? TryReadValue(IReadOnlyDictionary<string, object?>? data, string key)
	{
		if (data == null)
		{
			return null;
		}

		foreach (var pair in data)
		{
			if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
			{
				return pair.Value?.ToString();
			}
		}

		return null;
	}

	private static string? BuildKnownCodeFallback(string errorCode)
	{
		if (!HrCodeToName.TryGetValue(errorCode, out var codeName) || string.IsNullOrWhiteSpace(codeName))
		{
			return null;
		}

		var words = Regex.Matches(codeName, "[A-Z]+(?=$|[A-Z][a-z])|[A-Z]?[a-z]+|[0-9]+")
			.Select(match => match.Value)
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.ToArray();
		if (words.Length == 0)
		{
			return null;
		}

		var sentence = string.Join(" ", words);
		return $"{sentence}.";
	}
}
