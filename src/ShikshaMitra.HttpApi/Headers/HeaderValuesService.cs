using Microsoft.AspNetCore.Http;
using ShikshaMitra.Models;
using System;
using System.Collections.Generic;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Common.Headers;

public class HeaderValuesService(IHttpContextAccessor httpContextAccessor) : IHeaderValuesService, ITransientDependency
{
	private HeaderValues _cachedValues;

	public HeaderValues Current => _cachedValues ??= LoadHeaderValues();

	public Guid GetAcademicYearId() => Current.AcademicYearId;
	public Guid GetEducationBoardId() => Current.EducationBoardId;
	public Guid GetEducationMediumId() => Current.EducationMediumId;
	public string GetCurrentUser() => Current.CurrentUser;
	public DateTime GetRequestTime() => Current.RequestTime;

	private HeaderValues LoadHeaderValues()
	{
		var context = httpContextAccessor.HttpContext;
		if (context == null)
		{
			return new HeaderValues
			{
				RequestTime = DateTime.UtcNow
			};
		}

		var values = new HeaderValues
		{
			RequestTime = DateTime.UtcNow,
			CurrentUser = context.User?.Identity?.Name ?? "anonymous"
		};

		if (context.Items.TryGetValue("CustomHeaderGuids", out var guidObj) &&
			guidObj is Dictionary<string, Guid> guidValues)
		{
			values.AcademicYearId = guidValues.GetValueOrDefault("AcademicYearId", Guid.Empty);
			values.EducationBoardId = guidValues.GetValueOrDefault("EducationBoardId", Guid.Empty);
			values.EducationMediumId = guidValues.GetValueOrDefault("EducationMediumId", Guid.Empty);
		}

		return values;
	}
}
