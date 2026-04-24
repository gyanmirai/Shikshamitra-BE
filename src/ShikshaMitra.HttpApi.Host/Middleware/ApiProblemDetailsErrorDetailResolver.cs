using System;
using Microsoft.Extensions.Localization;
using ShikshaMitra.Localization;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Common.Middleware;

public class ApiProblemDetailsErrorDetailResolver : IApiProblemDetailsErrorDetailResolver, ITransientDependency
{
	private readonly IStringLocalizer<ShikshaMitraResource> _localizer;

	public ApiProblemDetailsErrorDetailResolver(IStringLocalizer<ShikshaMitraResource> localizer)
	{
		_localizer = localizer;
	}

	public string ResolveDetail(string? detail, string? errorCode, string defaultDetail)
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
				return localized.Value;
			}
		}

		return defaultDetail;
	}
}
