using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionEligibilityCommandService(AdmissionEligibilityAccessService access, AdmissionEligibilityStateService states,
	AdmissionEligibilityReadService reader, IRepository<AdmissionEligibilityEvaluation, Guid> evaluations,
	IRepository<AdmissionEligibilityException, Guid> exceptions, IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionActivity, Guid> activities) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionEligibilityDto> EvaluateAsync(Guid applicationId, Guid stageId, AdmissionEligibilityEvaluateDto input)
	{
		var state = await PrepareAsync(applicationId, stageId, input);
		var check = state.Check!;
		var row = new AdmissionEligibilityEvaluation(GuidGenerator.Create(), CurrentTenant.Id, applicationId, stageId,
			(state.Latest?.SequenceNumber ?? 0) + 1, check.Outcome, check.PolicyJson, check.FactsJson,
			JsonSerializer.Serialize(check.Criteria, new JsonSerializerOptions(JsonSerializerDefaults.Web)), check.Fingerprint, Actor(), DateTime.UtcNow);
		await evaluations.InsertAsync(row, false);
		return await SaveAsync(state, "AdmissionEligibilityEvaluated", row.Id, row.Outcome);
	}
	public async Task<AdmissionEligibilityDto> RequestExceptionAsync(Guid applicationId, Guid stageId, Guid evaluationId, AdmissionEligibilityExceptionRequestDto input)
	{
		var state = await PrepareAsync(applicationId, stageId, input); state.EnsureFresh();
		if (state.Latest?.Id != evaluationId || state.Status != "ineligible" || !state.Context.Policy!.AllowExceptions || state.Pending.Count != 0)
			throw new BusinessException(AdmissionEligibilityErrorCodes.ExceptionNotAllowed);
		var request = new AdmissionEligibilityException(GuidGenerator.Create(), CurrentTenant.Id, state.Latest, input.Reason, Actor(), DateTime.UtcNow);
		await exceptions.InsertAsync(request, false);
		return await SaveAsync(state, "AdmissionEligibilityExceptionRequested", request.Id, input.Reason);
	}
	public async Task<AdmissionEligibilityDto> ReviewExceptionAsync(Guid applicationId, Guid stageId, Guid exceptionId, AdmissionEligibilityExceptionReviewDto input)
	{
		var state = await PrepareAsync(applicationId, stageId, input); state.EnsureFresh();
		var request = state.Pending.SingleOrDefault(item => item.Id == exceptionId);
		if (request == null || !state.Context.Policy!.AllowExceptions || !reader.ReviewerRoleAllowed(state.Context.Policy) || input.Decision is not ("approved" or "rejected"))
			throw new BusinessException(AdmissionEligibilityErrorCodes.ExceptionNotAllowed);
		var decision = new AdmissionEligibilityException(GuidGenerator.Create(), CurrentTenant.Id, state.Latest!, input.Reason, Actor(), DateTime.UtcNow,
			request, input.Decision, state.Context.Policy.RequireIndependentReviewer);
		await exceptions.InsertAsync(decision, false);
		return await SaveAsync(state, "AdmissionEligibilityExceptionReviewed", decision.Id, input.Decision);
	}
	private async Task<AdmissionEligibilityState> PrepareAsync(Guid applicationId, Guid stageId, AdmissionEligibilityEvaluateDto input)
	{
		var context = await access.GetContextAsync(applicationId, stageId, true);
		AdmissionEligibilityAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		return await states.ReadAsync(context);
	}
	private async Task<AdmissionEligibilityDto> SaveAsync(AdmissionEligibilityState state, string action, Guid recordId, string description)
	{
		await applications.UpdateAsync(state.Context.Application, false);
		await activities.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, action, description,
			state.Context.Application.ApplicantId, state.Context.Application.Id,
			metadataJson: JsonSerializer.Serialize(new { RecordId = recordId, StageId = state.Context.Stage.Id, ActorId = Actor() })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await reader.GetAsync(state.Context.Application.Id, state.Context.Stage.Id);
	}
	private Guid Actor() => CurrentUser.Id ?? throw new BusinessException(AdmissionEligibilityErrorCodes.InvalidState);
}
