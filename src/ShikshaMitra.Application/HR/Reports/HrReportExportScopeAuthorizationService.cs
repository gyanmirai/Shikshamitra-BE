using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public sealed class HrReportExportScopeSnapshot
{
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;
	public Guid? TenantId { get; set; }
	public bool HasTenantWideAccess { get; set; }
	public List<Guid> MediumStageIds { get; set; } = [];
}

public class HrReportExportScopeAuthorizationService(
	HrReportGenerator reportGenerator,
	ICurrentTenant currentTenant) : ITransientDependency
{
	public async Task<HrReportExportScopeSnapshot> CaptureAsync(Guid userId, Guid? tenantId)
	{
		EnsureCurrentTenant(tenantId);
		var snapshot = await ResolveCurrentScopeAsync(userId);
		if (snapshot == null || snapshot.TenantId != tenantId)
		{
			throw AccessDenied(userId, "The user's HR UserScope belongs to another tenant.");
		}

		if (!snapshot.HasTenantWideAccess && snapshot.MediumStageIds.Count == 0)
		{
			throw AccessDenied(userId, "The report owner has no effective HR stage scope.");
		}

		return snapshot;
	}

	public async Task<HrReportExportScopeSnapshot?> ResolveCurrentScopeAsync(Guid userId)
	{
		return await reportGenerator.ResolveExportScopeAsync(userId, currentTenant.Id);
	}

	public async Task EnsureRecipientCoversSnapshotAsync(
		HrReportExportScopeSnapshot snapshot,
		Guid recipientUserId)
	{
		if (!await CanRecipientCoverSnapshotAsync(snapshot, recipientUserId))
		{
			throw AccessDenied(
				recipientUserId,
				"The recipient's HR UserScope does not cover the report export scope.");
		}
	}

	public async Task EnsureRecipientsCoverSnapshotAsync(
		HrReportExportScopeSnapshot snapshot,
		IEnumerable<Guid> recipientUserIds)
	{
		foreach (var recipientUserId in recipientUserIds.Where(x => x != Guid.Empty).Distinct())
		{
			await EnsureRecipientCoversSnapshotAsync(snapshot, recipientUserId);
		}
	}

	public async Task<bool> CanRecipientAccessExportAsync(
		HrReportExportJob job,
		Guid recipientUserId)
	{
		var recipientScope = await ResolveCurrentScopeAsync(recipientUserId);
		return recipientScope != null &&
			CanResolvedScopeAccessExport(job, recipientScope);
	}

	public bool CanResolvedScopeAccessExport(
		HrReportExportJob job,
		HrReportExportScopeSnapshot recipientScope)
	{
		if (job.TenantId != currentTenant.Id ||
			recipientScope.TenantId != job.TenantId)
		{
			return false;
		}

		var requiredScope = HrReportStoredExportRequestSerializer.DeserializeScope(job.RequestJson);
		if (requiredScope != null)
		{
			return DoesScopeCover(recipientScope, requiredScope);
		}

		// Older exports have no immutable generation-scope snapshot.
		// Only a tenant-wide recipient can safely receive an arbitrary older artifact.
		return recipientScope.HasTenantWideAccess;
	}

	public async Task EnsureRecipientCanAccessExportAsync(
		HrReportExportJob job,
		Guid recipientUserId)
	{
		if (!await CanRecipientAccessExportAsync(job, recipientUserId))
		{
			throw AccessDenied(
				recipientUserId,
				"The recipient's current HR UserScope does not cover this report export.");
		}
	}

	private async Task<bool> CanRecipientCoverSnapshotAsync(
		HrReportExportScopeSnapshot snapshot,
		Guid recipientUserId)
	{
		var recipientScope = await ResolveCurrentScopeAsync(recipientUserId);
		return recipientScope != null &&
			DoesScopeCover(recipientScope, snapshot);
	}

	private static bool DoesScopeCover(
		HrReportExportScopeSnapshot recipientScope,
		HrReportExportScopeSnapshot requiredScope)
	{
		if (requiredScope.Version != HrReportExportScopeSnapshot.CurrentVersion ||
			recipientScope.Version != HrReportExportScopeSnapshot.CurrentVersion ||
			requiredScope.MediumStageIds == null ||
			recipientScope.MediumStageIds == null ||
			requiredScope.TenantId != recipientScope.TenantId)
		{
			return false;
		}

		if (recipientScope.HasTenantWideAccess)
		{
			return true;
		}

		if (requiredScope.HasTenantWideAccess)
		{
			return false;
		}

		var recipientStageIds = recipientScope.MediumStageIds.ToHashSet();
		return requiredScope.MediumStageIds.Count > 0 &&
			requiredScope.MediumStageIds.All(recipientStageIds.Contains);
	}

	private void EnsureCurrentTenant(Guid? tenantId)
	{
		if (currentTenant.Id != tenantId)
		{
			throw AccessDenied(null, "The report export tenant does not match the active tenant.");
		}
	}

	private static BusinessException AccessDenied(Guid? recipientUserId, string reason)
	{
		var exception = new BusinessException(HRErrorCodes.ReportAccessDenied)
			.WithData("reason", reason);
		if (recipientUserId.HasValue)
		{
			exception.WithData("recipientUserId", recipientUserId.Value);
		}

		return exception;
	}
}
