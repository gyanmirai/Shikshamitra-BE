using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleRecipientValidator(
	HrReportRecipientLookupService recipientLookupService,
	HrReportExportScopeAuthorizationService scopeAuthorizationService,
	ICurrentTenant currentTenant)
	: ITransientDependency
{
	public async Task<List<Guid>> NormalizeRecipientsAsync(IEnumerable<Guid>? userIds, Guid ownerUserId)
	{
		var recipients = (userIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
		if (!recipients.Contains(ownerUserId))
		{
			recipients.Insert(0, ownerUserId);
		}

		var externalRecipients = recipients.Where(x => x != ownerUserId).ToList();
		var ownerScope = await scopeAuthorizationService.CaptureAsync(ownerUserId, currentTenant.Id);
		foreach (var recipientId in externalRecipients)
		{
			if (!await recipientLookupService.HasReportViewPermissionAsync(recipientId))
			{
				throw new BusinessException(HRErrorCodes.ReportAccessDenied)
					.WithData("recipientUserId", recipientId);
			}
		}
		await scopeAuthorizationService.EnsureRecipientsCoverSnapshotAsync(ownerScope, externalRecipients);

		return recipients;
	}
}
