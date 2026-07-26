using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportRecipientService(
	HrReportRecipientLookupService recipientLookupService,
	HrReportExportScopeAuthorizationService scopeAuthorizationService) : ITransientDependency
{
	internal List<Guid> Normalize(
		IEnumerable<Guid>? requestedUserIds,
		Guid ownerUserId) =>
		(requestedUserIds ?? [])
			.Where(x => x != Guid.Empty && x != ownerUserId)
			.Distinct()
			.ToList();

	internal async Task ValidateAsync(
		IEnumerable<Guid> userIds,
		HrReportExportScopeSnapshot? scopeSnapshot,
		HrReportExportJob? storedJob = null)
	{
		foreach (var userId in userIds)
		{
			if (!await recipientLookupService.HasReportViewPermissionAsync(userId))
			{
				throw new BusinessException(HRErrorCodes.ReportAccessDenied)
					.WithData("recipientUserId", userId);
			}

			if (scopeSnapshot != null)
			{
				await scopeAuthorizationService.EnsureRecipientCoversSnapshotAsync(
					scopeSnapshot,
					userId);
			}
			else if (storedJob != null)
			{
				await scopeAuthorizationService.EnsureRecipientCanAccessExportAsync(
					storedJob,
					userId);
			}
		}
	}

	internal async Task<List<Guid>> ResolveShareRecipientsAsync(
		HrReportExportJob job,
		Guid ownerUserId,
		IEnumerable<Guid>? requestedUserIds)
	{
		await scopeAuthorizationService.EnsureRecipientCanAccessExportAsync(job, ownerUserId);
		var existingUserIds = job.GetSharedUserIds()
			.Where(x => x != Guid.Empty && x != ownerUserId)
			.Distinct()
			.ToList();
		var normalizedRequestedUserIds = Normalize(requestedUserIds, ownerUserId);
		var newUserIds = normalizedRequestedUserIds
			.Where(userId => !existingUserIds.Contains(userId))
			.ToList();
		if (existingUserIds.Count == 0 && newUserIds.Count == 0)
		{
			throw EmptySharedRecipientException();
		}

		var scopeSnapshot = HrReportStoredExportRequestSerializer.DeserializeScope(job.RequestJson);
		await ValidateAsync(newUserIds, scopeSnapshot, job);
		return existingUserIds
			.Concat(newUserIds)
			.Distinct()
			.ToList();
	}

	private static BusinessException EmptySharedRecipientException() =>
		new BusinessException(HRErrorCodes.ReportAccessDenied)
			.WithData("reason", "At least one eligible HR report recipient must be selected.");
}
