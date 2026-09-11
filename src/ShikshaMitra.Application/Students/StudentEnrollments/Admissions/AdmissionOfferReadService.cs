using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionOfferReadService(
	IRepository<AdmissionApplicationDecisionHistory, Guid> history,
	AdmissionOfferAccessService access) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionOfferStageDto> GetAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var events = (await history.GetListAsync(item => item.ApplicationId == applicationId &&
			item.StageInstanceId == stageId && item.Action != null)).OrderBy(item => item.OccurredAt).ToList();
		var policy = context.Policy;
		var status = GetStatus(context.Application, events, Clock.Now);
		var issueEvents = events.Where(item => item.Action is "issue" or "reissue").ToList();
		var extensionCount = events.Count(item => item.Action == "extend");
		var reissueCount = events.Count(item => item.Action == "reissue");
		var canComplete = policy?.AllowedCompletionOutcomes.Contains(status, StringComparer.Ordinal) == true;
		return new AdmissionOfferStageDto
		{
			ApplicationId = applicationId, StageId = stageId, OfferStageEnabled = policy != null,
			Policy = policy, IsActive = context.IsActive, Status = status, VersionNumber = issueEvents.Count,
			ExtensionCount = extensionCount, ReissueCount = reissueCount,
			IssuedAt = issueEvents.LastOrDefault()?.OccurredAt, ExpiresAt = context.Application.OfferExpiresAt,
			RespondedAt = events.LastOrDefault(item => item.Action is "accept" or "decline")?.OccurredAt,
			ConcurrencyStamp = context.Application.ConcurrencyStamp,
			Events = events.Select(Map).ToList(), Blockers = Blockers(policy, status),
			CanIssue = context.IsActive && policy != null && status == "ready" && context.Application.Status == AdmissionApplicationStatus.Approved,
			CanAccept = context.IsActive && policy != null && status == "issued",
			CanDecline = context.IsActive && policy != null && status == "issued",
			CanExtend = context.IsActive && policy?.AllowExtension == true && status == "issued" && extensionCount < policy.MaximumExtensions,
			CanWithdraw = context.IsActive && policy != null && status == "issued",
			CanReissue = context.IsActive && policy?.AllowReissue == true && status is "declined" or "expired" or "withdrawn" && reissueCount < policy.MaximumReissues,
			CanComplete = canComplete
		};
	}

	public static string GetStatus(AdmissionApplication application, IReadOnlyCollection<AdmissionApplicationDecisionHistory> events, DateTime now)
	{
		if (application.Status == AdmissionApplicationStatus.OfferAccepted) return "accepted";
		if (application.Status == AdmissionApplicationStatus.OfferDeclined) return "declined";
		if (application.Status == AdmissionApplicationStatus.Offered)
			return application.OfferExpiresAt.HasValue && application.OfferExpiresAt.Value <= now ? "expired" : "issued";
		if (events.LastOrDefault()?.Action == "withdraw") return "withdrawn";
		return "ready";
	}

	private static List<string> Blockers(AdmissionOfferStagePolicy? policy, string status)
	{
		if (policy == null) return [];
		if (policy.AllowedCompletionOutcomes.Contains(status, StringComparer.Ordinal)) return [];
		return status switch
		{
			"ready" => ["Issue the admission offer."],
			"issued" => ["Record the applicant or guardian response."],
			"expired" => ["Reissue the expired offer or allow expiry as a completion outcome."],
			"withdrawn" => ["Reissue the withdrawn offer or allow withdrawal as a completion outcome."],
			_ => [$"The {status} outcome is not configured to complete this stage."]
		};
	}

	private static AdmissionOfferEventDto Map(AdmissionApplicationDecisionHistory item) => new()
	{
		Id = item.Id, Action = item.Action!, ApplicationStatus = item.Status, Reason = item.Reason,
		ActorId = item.ActorId, OccurredAt = item.OccurredAt, MetadataJson = item.MetadataJson
	};
}
