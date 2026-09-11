using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionReviews;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionReviewReadService(AdmissionReviewAccessService access, AdmissionReviewStateService states,
	AdmissionReviewPeopleService people, AdmissionReviewDecisionGuard decisions, AdmissionReviewRuntimeService runtime,
	IRepository<AdmissionReviewRecord, Guid> records, IRepository<AdmissionReviewDecision, Guid> reviews,
	IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionReviewDto> GetAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId); var state = await states.ReadAsync(context);
		var result = new AdmissionReviewDto { ApplicationId = applicationId, StageId = stageId, ReviewEnabled = context.Policy != null,
			Policy = context.Policy, IsActive = context.IsActive, ConcurrencyStamp = context.Application.ConcurrencyStamp,
			Status = state.Status, IsStale = state.IsStale, AssignedReviewerId = state.AssignedReviewerId,
			AssignedReviewerName = await people.GetNameAsync(state.AssignedReviewerId), DueAt = state.DueAt, IsOverdue = state.IsOverdue,
			Prerequisites = state.Evidence?.Items ?? [], Decision = await decisions.GetCapabilitiesAsync(state) };
		if (state.Latest != null) result.LatestRecord = await MapAsync(state.Latest, state, state.Decision);
		if (context.Policy == null) return result;
		result.CanSaveDraft = await people.CanReviewAsync(state, Permissions.Edit);
		result.CanSubmit = await people.CanReviewAsync(state, Permissions.Submit) && state.Evidence?.IsKnown == true;
		result.CanAssign = (context.CanMutate || context.IsActive && context.Application.Status == AdmissionApplicationStatus.Draft) && await permissions.IsGrantedAsync(Permissions.Assign);
		result.CanRequestCorrection = state.Latest?.Status == "submitted" && !state.IsStale && state.Decision == null &&
			state.Evidence?.IsKnown == true && context.IsActive && await people.CanDecideAsync(state) && await permissions.IsGrantedAsync(ApplicationPermissions.Reopen) &&
			context.Application.Status is AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Waitlisted or AdmissionApplicationStatus.Approved;
		result.CanComplete = await runtime.CanCompleteAsync(state);
		if (context.Application.Status == AdmissionApplicationStatus.Draft) result.Blockers.Add("Submit the corrected application form before continuing the review.");
		if (state.IsStale) result.Blockers.Add(AdmissionReviewDecisionGuard.Message(AdmissionReviewErrorCodes.StaleReview));
		if (state.Evidence?.IsKnown != true) result.Blockers.Add(AdmissionReviewDecisionGuard.Message(AdmissionReviewErrorCodes.EvidenceRequired));
		if (state.AssignedReviewerId.HasValue && state.AssignedReviewerId != CurrentUser.Id) result.Blockers.Add("The assigned reviewer must complete and submit this review.");
		if (context.Policy.ReviewerRole is { Length: > 0 } role && !CurrentUser.IsInRole(role)) result.Blockers.Add("A reviewer in the configured role must complete this review.");
		result.Blockers.AddRange(result.Decision.Blockers);
		if (state.Decision != null && context.IsActive && !result.CanComplete && state.Decision.Decision is "reject" or "waitlist")
			result.Blockers.Add("Add a matching route for this admission decision before completing Review.");
		result.Blockers = result.Blockers.Distinct().ToList(); return result;
	}
	public async Task<List<AdmissionReviewRecordDto>> GetHistoryAsync(Guid applicationId, Guid stageId)
	{
		var state = await states.ReadAsync(await access.GetContextAsync(applicationId, stageId));
		var rows = await records.GetListAsync(item => item.ApplicationId == applicationId && item.StageInstanceId == stageId);
		var decisions = await reviews.GetListAsync(item => item.ApplicationId == applicationId && item.StageInstanceId == stageId);
		var result = new List<AdmissionReviewRecordDto>();
		foreach (var row in rows.OrderByDescending(item => item.SequenceNumber)) result.Add(await MapAsync(row, state, decisions.SingleOrDefault(item => item.RecordId == row.Id)));
		return result;
	}
	private async Task<AdmissionReviewRecordDto> MapAsync(AdmissionReviewRecord row, AdmissionReviewState state, AdmissionReviewDecision? decision) => new()
	{
		Id = row.Id, SequenceNumber = row.SequenceNumber, Status = row.Status, Recommendation = row.Recommendation,
		Reason = row.Reason, Notes = row.Notes, RecordedBy = row.RecordedBy, RecordedAt = row.RecordedAt,
		AssignedReviewerId = row.AssignedReviewerId, AssignedReviewerName = await people.GetNameAsync(row.AssignedReviewerId), DueAt = row.DueAt,
		IsStale = state.Evidence != null && row.InputFingerprint != state.Evidence.Fingerprint,
		Checklist = JsonSerializer.Deserialize<List<AdmissionReviewChecklistDto>>(row.ChecklistSnapshotJson, AdmissionReviewEvidenceService.JsonOptions) ?? [],
		Prerequisites = JsonSerializer.Deserialize<List<AdmissionReviewEvidenceDto>>(row.EvidenceSnapshotJson, AdmissionReviewEvidenceService.JsonOptions) ?? [],
		Decision = decision == null ? null : new() { Id = decision.Id, RecordId = decision.RecordId, Decision = decision.Decision,
			Reason = decision.Reason, DecidedBy = decision.DecidedBy, DecidedAt = decision.DecidedAt }
	};
}
