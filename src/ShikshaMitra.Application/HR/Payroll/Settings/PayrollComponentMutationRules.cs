using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;

namespace ShikshaMitra.HR.Payroll.Settings;

internal static class PayrollComponentMutationRules
{
	public static void EnsurePreviewHashProvided(string? previewHash)
	{
		if (!string.IsNullOrWhiteSpace(previewHash))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.PayrollComponentPreviewRequired)
			.WithData("message", "Preview payroll component impact before saving.");
	}

	public static void EnsureDeletePreviewHashProvided(string previewHash)
	{
		if (!string.IsNullOrWhiteSpace(previewHash))
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.PayrollComponentPreviewRequired)
			.WithData("message", "Preview payroll component delete impact before deleting.");
	}

	public static void EnsureActive(bool isActive)
	{
		if (isActive)
		{
			return;
		}

		throw new BusinessException(HRErrorCodes.PayrollComponentInactive)
			.WithData("message", "Retired payroll components cannot be updated.");
	}

	public static bool IsNameUniqueViolation(Exception exception, string indexName, string tableHint) =>
		PayrollComponentImpactProjection.IsNameUniqueViolation(exception, indexName, tableHint);

	public static string ResolveEffectiveMonth(string? effectiveMonth, DateTime now) =>
		PayrollComponentImpactProjection.ResolveEffectiveMonth(effectiveMonth, now);

	public static async Task ClearComponentListCacheAsync<TDto>(
		IDistributedCache<PagedResultDto<TDto>> cache,
		string cachePrefix,
		Guid? tenantId,
		ILogger logger,
		string componentLabel)
	{
		try
		{
			await cache.RemoveManyAsync(PayrollComponentImpactProjection.BuildCommonActiveCacheKeys(cachePrefix, tenantId));
		}
		catch (Exception ex)
		{
			logger.LogWarning(
				ex,
				"Failed to invalidate {ComponentLabel} component cache for tenant {TenantId}. List endpoints may be stale until expiration.",
				componentLabel,
				tenantId);
		}
	}
}
