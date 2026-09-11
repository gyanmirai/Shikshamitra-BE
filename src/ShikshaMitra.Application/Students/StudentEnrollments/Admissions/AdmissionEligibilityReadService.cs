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
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionEligibility;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionEligibilityReadService(AdmissionEligibilityAccessService access, AdmissionEligibilityStateService states,
	AdmissionEligibilityRuntimeService runtime, IRepository<AdmissionEligibilityEvaluation, Guid> evaluations,
	IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionEligibilityDto> GetAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var state = await states.ReadAsync(context);
		var canReview = context.CanMutate && !state.IsStale && ReviewerRoleAllowed(context.Policy) && await permissions.IsGrantedAsync(Permissions.ReviewException);
		var latest = state.Latest == null ? null : MapEvaluation(state.Latest, state, canReview);
		var blockers = new List<string>();
		if (context.Policy != null)
		{
			if (!context.CanMutate) blockers.Add("This eligibility stage is historical or has not been reached.");
			if (state.Latest == null) blockers.Add("Check eligibility using the saved applicant information.");
			else if (state.IsStale) blockers.Add("Source information changed. Check eligibility again before continuing.");
			else if (state.Status == "needsInformation") blockers.Add("Complete or correct the missing source information, then check eligibility again.");
			else if (state.Status == "ineligible") blockers.Add("The applicant does not meet every configured rule. Obtain an allowed exception or use an explicit review route.");
			if (state.Pending.Count > 0) blockers.Add(context.Policy.RequireIndependentReviewer && state.Pending.Any(item => item.ActorId == CurrentUser.Id)
				? "A different authorized staff member must review the pending exception." : "An authorized reviewer must decide the pending exception.");
			if (state.Pending.Count > 0 && !ReviewerRoleAllowed(context.Policy)) blockers.Add($"The exception must be reviewed by the configured {context.Policy.ExceptionApprovalRole} role.");
		}
		var canComplete = await runtime.CanCompleteAsync(state);
		if (context.IsActive && state.Latest != null && !state.IsStale && !canComplete && state.Status is "ineligible" or "needsInformation")
			blockers.Add("Configure and select an explicit route for this eligibility result before continuing.");
		return new() { ApplicationId = applicationId, StageId = stageId, EligibilityEnabled = context.Policy != null, Policy = context.Policy,
			IsActive = context.IsActive, ConcurrencyStamp = context.Application.ConcurrencyStamp, Status = state.Status, IsStale = state.IsStale,
			CanEvaluate = context.CanMutate && await permissions.IsGrantedAsync(Permissions.Evaluate),
			CanRequestException = context.CanMutate && context.Policy!.AllowExceptions && state.Status == "ineligible" && !state.IsStale && state.Pending.Count == 0 && await permissions.IsGrantedAsync(Permissions.RequestException),
			CanReviewException = latest?.Exceptions.Any(item => item.CanReview) == true, CanComplete = canComplete, LatestEvaluation = latest, Blockers = blockers };
	}
	public async Task<List<AdmissionEligibilityEvaluationDto>> GetHistoryAsync(Guid applicationId, Guid stageId)
	{
		var state = await states.ReadAsync(await access.GetContextAsync(applicationId, stageId));
		return (await evaluations.GetListAsync(item => item.ApplicationId == applicationId && item.StageInstanceId == stageId))
			.OrderByDescending(item => item.SequenceNumber).Select(item => MapEvaluation(item, state, false)).ToList();
	}
	public bool ReviewerRoleAllowed(AdmissionEligibilityPolicy? policy) => policy != null &&
		(string.IsNullOrWhiteSpace(policy.ExceptionApprovalRole) || CurrentUser.IsInRole(policy.ExceptionApprovalRole));
	private AdmissionEligibilityEvaluationDto MapEvaluation(AdmissionEligibilityEvaluation row, AdmissionEligibilityState state, bool canReview) => new()
	{
		Id = row.Id, SequenceNumber = row.SequenceNumber, Outcome = row.Outcome, EvaluatedBy = row.EvaluatedBy, EvaluatedAt = row.EvaluatedAt,
		IsStale = state.Check != null && state.Check.Fingerprint != row.InputFingerprint,
		Criteria = JsonSerializer.Deserialize<List<AdmissionEligibilityCriterionResultDto>>(row.CriteriaSnapshotJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [],
		Exceptions = state.Exceptions.Where(item => item.EvaluationId == row.Id && item.Decision == "pending").OrderBy(item => item.OccurredAt).Select(request =>
		{
			var decision = state.Exceptions.SingleOrDefault(item => item.RequestId == request.Id);
			return new AdmissionEligibilityExceptionDto { Id = request.Id, EvaluationId = row.Id, Reason = request.Reason, RequestedBy = request.ActorId, RequestedAt = request.OccurredAt,
				Status = decision?.Decision ?? "pending", ReviewReason = decision?.Reason, ReviewedBy = decision?.ActorId, ReviewedAt = decision?.OccurredAt,
				CanReview = canReview && state.Context.Policy!.AllowExceptions && row.Id == state.Latest?.Id && decision == null &&
					(!state.Context.Policy.RequireIndependentReviewer || request.ActorId != CurrentUser.Id) };
		}).ToList()
	};
}
