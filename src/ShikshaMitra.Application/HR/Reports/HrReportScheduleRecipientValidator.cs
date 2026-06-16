using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleRecipientValidator(
	HrReportRecipientLookupService recipientLookupService)
	: ITransientDependency
{
	public async Task<List<Guid>> NormalizeRecipientsAsync(IEnumerable<Guid>? userIds, Guid ownerUserId)
	{
		var recipients = (userIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
		if (!recipients.Contains(ownerUserId))
		{
			recipients.Insert(0, ownerUserId);
		}

		foreach (var recipientId in recipients.Where(x => x != ownerUserId))
		{
			if (!await recipientLookupService.HasReportViewPermissionAsync(recipientId))
			{
				throw new BusinessException(HRErrorCodes.ReportAccessDenied)
					.WithData("recipientUserId", recipientId);
			}
		}

		return recipients;
	}
}
