using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationManualAppService(AcademicNotificationAccessService sourceAccess,
	AcademicNotificationManualPlanner planner, AcademicNotificationReviewService reviews,
	AcademicNotificationManualOperations operations, AcademicNotificationStaffAudit audit, IClock clock,
	Student360NotificationAudience student360)
	: ApplicationService, IAcademicNotificationManualAppService
{
	public virtual async Task<Student360NotificationAudienceDto> GetStudent360AudienceAsync(Guid studentId, string eventKey,
		CancellationToken cancellationToken = default)
	{
		if (!Student360NotificationAudience.Supports(eventKey)) throw new AbpAuthorizationException();
		var input = new PreviewAcademicNotificationManualDto(eventKey, Student360NotificationSourceAdapter.Type,
			studentId, [], [CommunicationChannelType.InApp]);
		var context = await sourceAccess.RequireManualPreviewAsync(input, cancellationToken);
		await planner.EnsureScopeAsync(input, context, cancellationToken);
		if (!context.Source.IsEligible) return new(null, [], []);
		return new(context.Source.AudienceSlices!.Single().Scope.MediumId,
			await student360.ReadFamilyChoicesAsync(eventKey, context, cancellationToken),
			context.Source.EligibleRecipients.Where(x => x.Kind == "ClassTeacher").ToArray());
	}

	public virtual async Task<IReadOnlyList<NotificationRecipientRefDto>> GetDiaryFollowUpRecipientsAsync(Guid diaryId,
		CancellationToken cancellationToken = default)
	{
		var input = new PreviewAcademicNotificationManualDto(TeacherDiaryNotificationSource.EventKey,
			TeacherDiaryNotificationSource.Type, diaryId, [], [CommunicationChannelType.InApp]);
		var context = await sourceAccess.RequireManualPreviewAsync(input, cancellationToken);
		return context.Source.IsEligible ? context.Source.EligibleRecipients : [];
	}

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

	[UnitOfWork(true)]
	public virtual async Task<AcademicNotificationOperationDto> SendAsync(ConfirmAcademicNotificationManualDto input,
		CancellationToken cancellationToken = default)
	{
		var identity = await sourceAccess.RequireManualSenderAsync(cancellationToken);
		var (review, idempotencyHash) = await reviews.LockConfirmationAsync(identity.TenantId, identity.ActorId, input, Now(), cancellationToken);
		var request = reviews.ReadRequest(review);
		var context = await LoadReviewedSourceAsync(request, identity, cancellationToken);
		if (review.OperationId is Guid existing)
			return Map(await operations.GetAsync(identity.TenantId, existing, request, cancellationToken));
		var plan = await RequireSameReviewAsync(review, request, context, cancellationToken);
		var operation = await operations.AcceptAsync(review, request, plan, idempotencyHash, Now(), cancellationToken);
		await reviews.SaveAsync(review, cancellationToken);
		await audit.WriteAsync(identity.TenantId, identity.ActorId, "ConfirmManualSend", operation.Id, null,
			request.EventKey, operation.Status.ToString(), null, null, cancellationToken);
		return Map(operation);
	}

	[UnitOfWork(true)]
	public virtual Task<AcademicNotificationOperationDto> ApproveAsync(Guid operationId, CancellationToken cancellationToken = default) =>
		DecideAsync(operationId, approve: true, cancellationToken);

	[UnitOfWork(true)]
	public virtual Task<AcademicNotificationOperationDto> RejectAsync(Guid operationId, CancellationToken cancellationToken = default) =>
		DecideAsync(operationId, approve: false, cancellationToken);

	private async Task<AcademicNotificationOperationDto> DecideAsync(Guid operationId, bool approve, CancellationToken cancellationToken)
	{
		var identity = await sourceAccess.RequireStaffActionAsync(cancellationToken);
		operations.RequireAssignedApprover(identity.TenantId, identity.ActorId);
		var review = await reviews.LockDecisionAsync(identity.TenantId, operationId, identity.ActorId, cancellationToken);
		var request = reviews.ReadRequest(review);
		var context = await LoadReviewedSourceAsync(request, identity, cancellationToken);
		var operation = await operations.GetAsync(identity.TenantId, operationId, request, cancellationToken);
		if (approve && operation.ApprovedByUserId == identity.ActorId ||
			!approve && operation.Status == CommunicationCampaignStatus.Cancelled) return Map(operation);
		if (!operation.ApprovalRequired || operation.Status != CommunicationCampaignStatus.PendingApproval)
			throw new BusinessException(NotificationErrorCodes.ReviewConsumed);
		if (approve)
		{
			var plan = await RequireSameReviewAsync(review, request, context, cancellationToken);
			var now = Now();
			operation.ApprovedByUserId = identity.ActorId;
			operation.ApprovedAt = now;
			await operations.QueueAsync(operation, review, request, plan, now, cancellationToken);
		}
		else
		{
			await planner.EnsureScopeAsync(request, context, cancellationToken);
			operation.Status = CommunicationCampaignStatus.Cancelled;
			operation.RejectionReason = "Rejected by the assigned approver.";
		}
		await operations.SaveAsync(operation, cancellationToken);
		await audit.WriteAsync(identity.TenantId, identity.ActorId, approve ? "ApproveManualSend" : "RejectManualSend", operation.Id,
			null, request.EventKey, approve ? "Approved" : "Rejected", null, null, cancellationToken);
		return Map(operation);
	}

	private async Task<AcademicNotificationManualPlan> RequireSameReviewAsync(NotificationSendReview review,
		PreviewAcademicNotificationManualDto request, AcademicNotificationAccessContext context, CancellationToken cancellationToken)
	{
		AcademicNotificationManualPlan plan;
		try { plan = await planner.BuildAsync(request, context, Now(), cancellationToken); }
		catch (AbpAuthorizationException) { throw new BusinessException(NotificationErrorCodes.ReviewChanged); }
		catch (BusinessException error) when (error.Code is NotificationErrorCodes.SourceUnavailable or NotificationErrorCodes.RecipientIneligible or
			NotificationErrorCodes.RuleConfigurationInvalid or NotificationErrorCodes.TemplateInvalid)
		{
			throw new BusinessException(NotificationErrorCodes.ReviewChanged);
		}
		var current = AcademicNotificationReviewService.Digest(request, context with { UserId = review.ActorUserId }, plan);
		if (!string.Equals(current, review.Digest, StringComparison.Ordinal))
			throw new BusinessException(NotificationErrorCodes.ReviewChanged);
		return plan;
	}

	private async Task<AcademicNotificationAccessContext> LoadReviewedSourceAsync(PreviewAcademicNotificationManualDto request,
		(Guid TenantId, Guid ActorId, UserScopeContext Scope) identity, CancellationToken cancellationToken)
	{
		try { return await sourceAccess.LoadManualSourceAsync(request, identity, cancellationToken); }
		catch (BusinessException error) when (error.Code is NotificationErrorCodes.SourceUnavailable or NotificationErrorCodes.EventUnknown)
		{
			throw new BusinessException(NotificationErrorCodes.ReviewChanged);
		}
	}

	private static AcademicNotificationOperationDto Map(CommunicationCampaign operation) => new(operation.Id, operation.Status);
	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
