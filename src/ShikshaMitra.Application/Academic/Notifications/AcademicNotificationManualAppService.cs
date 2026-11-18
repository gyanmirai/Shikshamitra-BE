using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationManualAppService(AcademicNotificationAccessService sourceAccess,
	AcademicNotificationManualPlanner planner, AcademicNotificationReviewService reviews, IClock clock)
	: ApplicationService, IAcademicNotificationManualAppService
{
	[UnitOfWork(true)]
	public virtual async Task<AcademicNotificationManualPreviewDto> PreviewAsync(PreviewAcademicNotificationManualDto input,
		CancellationToken cancellationToken = default)
	{
		var context = await sourceAccess.RequireManualPreviewAsync(input, cancellationToken);
		var plan = await planner.BuildAsync(input, context, Now(), cancellationToken);
		var summary = plan.Summary;
		var review = await reviews.CreateAsync(input, context, plan, Now(), cancellationToken);
		return new(review?.Id, review?.ExpiresAt, summary.EligibleRecipientCount, plan.RequiresApproval, summary.Channels,
			summary.Samples, summary.BlockingIssues);
	}

	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
