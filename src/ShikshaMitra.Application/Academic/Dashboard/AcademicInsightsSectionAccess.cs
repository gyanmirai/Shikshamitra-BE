using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Validation;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Authorization.Permissions;

namespace ShikshaMitra.Academic.Dashboard;

public static class AcademicInsightsSectionAccess
{
	public static async Task<AcademicInsightSectionDto?> ReadAsync(IPermissionChecker permissions, string permission, Func<Task<AcademicInsightSectionDto>> read) =>
		await permissions.IsGrantedAsync(permission) ? await SafeReadAsync("", async () => await read()) : null;

	public static async Task<AcademicInsightSectionDto?> ReadFeatureAsync(IPermissionChecker permissions, string key, string primaryPermission,
		IReadOnlyCollection<string> siblingPermissions, Func<Task<AcademicInsightSectionDto>> read, ILogger? logger)
	{
		if (await permissions.IsGrantedAsync(primaryPermission)) return await SafeReadAsync(key, async () => await read(), logger);
		foreach (var sibling in siblingPermissions)
			if (await permissions.IsGrantedAsync(sibling)) return AcademicInsightsAppService.Unavailable(key,
				"The aggregate requires its feature View access. Open a permitted workflow in this section.");
		return null;
	}

	public static async Task<AcademicInsightSectionDto?> SafeReadAsync(string key, Func<Task<AcademicInsightSectionDto?>> read, ILogger? logger = null)
	{
		try { return await read(); }
		catch (Exception error) when (error is not AbpException and not BusinessException and not AbpAuthorizationException and not AbpValidationException and not EntityNotFoundException and not UnauthorizedAccessException and not OperationCanceledException and not ValidationException and not ArgumentException)
		{
			logger?.LogWarning(error, "Academic Insights section {SectionKey} could not be loaded.", key);
			return new() { Key = key, Status = "error", Summary = "This section could not be loaded. Try again." };
		}
	}
}
