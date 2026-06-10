using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyImpactConfirmationService(
	IDistributedCache<string> cache,
	IHttpContextAccessor httpContextAccessor,
	IClock clock) : ITransientDependency
{
	public const string HeaderName = "X-HR-Policy-Impact-Token";
	private const int ExpiryMinutes = 15;
	private const string CachePrefix = "hr-policy-impact-token:";

	public async Task AttachTokenAsync(HrPolicyImpactPreviewDto preview)
	{
		preview.ConfirmationToken = null;
		preview.ExpiresAt = null;
		preview.BlockingWarnings = HrPolicyImpactWarningBuilder.GetBlockingWarnings(preview);
		if (preview.BlockingWarnings.Count > 0)
		{
			preview.RequiresConfirmation = false;
			return;
		}

		preview.RequiresConfirmation = preview.Warnings.Any(HrPolicyImpactWarningBuilder.RequiresConfirmation);

		if (!preview.RequiresConfirmation)
		{
			return;
		}

		var token = Guid.NewGuid().ToString("N");
		var expiresAt = clock.Now.AddMinutes(ExpiryMinutes);
		var payload = new ConfirmationPayload(
			preview.PolicyType,
			preview.PolicyId,
			preview.AcademicYearId,
			NormalizeAction(preview.Action),
			HasBlockingWarnings: false,
			HrPolicyImpactFingerprintBuilder.Build(preview),
			expiresAt);

		await cache.SetAsync(
			CachePrefix + token,
			JsonSerializer.Serialize(payload),
			new DistributedCacheEntryOptions
			{
				AbsoluteExpiration = expiresAt
			});

		preview.ConfirmationToken = token;
		preview.ExpiresAt = expiresAt;
	}

	public async Task EnsureConfirmedAsync(HrPolicyImpactPreviewDto preview, string? confirmationToken = null)
	{
		preview.BlockingWarnings = HrPolicyImpactWarningBuilder.GetBlockingWarnings(preview);
		if (preview.BlockingWarnings.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyImpactBlocking)
				.WithData("warnings", string.Join("; ", preview.BlockingWarnings.Select(x => x.Message)));
		}

		var requiresConfirmation = preview.Warnings.Any(HrPolicyImpactWarningBuilder.RequiresConfirmation);
		if (!requiresConfirmation)
		{
			return;
		}

		var token = !string.IsNullOrWhiteSpace(confirmationToken)
			? confirmationToken
			: httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
		if (string.IsNullOrWhiteSpace(token))
		{
			throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationRequired);
		}

		var cached = await cache.GetAsync(CachePrefix + token);
		if (string.IsNullOrWhiteSpace(cached))
		{
			throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		}

		var payload = JsonSerializer.Deserialize<ConfirmationPayload>(cached)
			?? throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid);

		if (payload.ExpiresAt < clock.Now ||
			payload.HasBlockingWarnings ||
			!string.Equals(payload.RiskFingerprint, HrPolicyImpactFingerprintBuilder.Build(preview), StringComparison.Ordinal) ||
			!string.Equals(payload.PolicyType, preview.PolicyType, StringComparison.OrdinalIgnoreCase) ||
			payload.PolicyId != preview.PolicyId ||
			payload.AcademicYearId != preview.AcademicYearId ||
			!string.Equals(payload.Action, NormalizeAction(preview.Action), StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		}
	}

	private static string NormalizeAction(string? action) =>
		string.IsNullOrWhiteSpace(action) ? "update" : action.Trim().ToLowerInvariant();

	private sealed record ConfirmationPayload(
		string PolicyType,
		Guid? PolicyId,
		Guid AcademicYearId,
		string Action,
		bool HasBlockingWarnings,
		string RiskFingerprint,
		DateTime ExpiresAt);
}
