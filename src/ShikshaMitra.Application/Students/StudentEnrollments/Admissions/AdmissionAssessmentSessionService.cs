using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionAssessmentSessionService(
	IRepository<AdmissionAssessmentSession, Guid> sessions,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionWorkflowDefinition, Guid> workflows,
	IRepository<AdmissionCycle, Guid> cycles,
	AdmissionAssessmentAccessService access) : ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionAssessmentScopeDto>> GetScopesAsync()
	{
		var offerings = await AsyncExecuter.ToListAsync(await access.GetScopedOfferingsAsync());
		var cycleIds = offerings.Select(item => item.AdmissionCycleId).Distinct().ToList();
		var academicYears = (await cycles.GetListAsync(item => cycleIds.Contains(item.Id)))
			.ToDictionary(item => item.Id, item => item.AcademicYearId);
		var offeringIds = offerings.Select(item => item.Id).ToList();
		var applicationQuery = await applications.GetQueryableAsync();
		var pinned = await AsyncExecuter.ToListAsync(applicationQuery.Where(item => offeringIds.Contains(item.AdmissionOfferingId))
			.Select(item => new { item.AdmissionOfferingId, item.WorkflowDefinitionId }).Distinct());
		var workflowIds = offerings.Where(item => item.WorkflowDefinitionId.HasValue).Select(item => item.WorkflowDefinitionId!.Value)
			.Concat(pinned.Select(item => item.WorkflowDefinitionId)).Distinct().ToList();
		var workflowRows = (await workflows.GetListAsync(item => workflowIds.Contains(item.Id) &&
			(item.Status == AdmissionWorkflowStatus.Published || item.Status == AdmissionWorkflowStatus.Superseded ||
			 item.Status == AdmissionWorkflowStatus.Archived))).ToDictionary(item => item.Id);
		var stageRows = await definitions.GetListAsync(item => workflowIds.Contains(item.WorkflowDefinitionId) && item.StageType == AdmissionWorkflowStageType.Assessment);
		var result = new List<AdmissionAssessmentScopeDto>();
		foreach (var offering in offerings)
		foreach (var stage in stageRows.Where(stage => stage.WorkflowDefinitionId == offering.WorkflowDefinitionId ||
			pinned.Any(application => application.AdmissionOfferingId == offering.Id && application.WorkflowDefinitionId == stage.WorkflowDefinitionId)))
		{
			var policy = AdmissionAssessmentAccessService.ReadPolicy(stage);
			if (policy == null || !workflowRows.TryGetValue(stage.WorkflowDefinitionId, out var workflow)) continue;
			result.Add(new AdmissionAssessmentScopeDto
			{
				AdmissionOfferingId = offering.Id, OfferingName = offering.Name, OfferingCode = offering.Code, WorkflowStageDefinitionId = stage.Id,
				AcademicYearId = academicYears.GetValueOrDefault(offering.AdmissionCycleId),
				BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
				StageName = stage.Name, WorkflowDefinitionId = workflow.Id, WorkflowName = workflow.Name, WorkflowVersion = workflow.Version,
				SchedulingEnabled = policy.SchedulingEnabled
			});
		}
		return result.OrderBy(item => item.OfferingName).ThenBy(item => item.StageName).ToList();
	}

	public async Task<List<AdmissionAssessmentSessionDto>> GetListAsync(Guid offeringId, Guid definitionId)
	{
		await access.GetSessionDefinitionAsync(offeringId, definitionId);
		return (await sessions.GetListAsync(item => item.AdmissionOfferingId == offeringId && item.WorkflowStageDefinitionId == definitionId))
			.OrderBy(item => item.StartsAt).Select(Map).ToList();
	}

	public async Task<AdmissionAssessmentSessionDto> CreateAsync(AdmissionAssessmentSessionInput input)
	{
		await access.GetSessionDefinitionAsync(input.AdmissionOfferingId, input.WorkflowStageDefinitionId);
		EnsureFuture(input.StartsAt);
		var session = new AdmissionAssessmentSession(GuidGenerator.Create(), CurrentTenant.Id, input.AdmissionOfferingId,
			input.WorkflowStageDefinitionId, input.Name, input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime, input.Location, input.Capacity);
		await sessions.InsertAsync(session, true);
		return Map(session);
	}

	public async Task<AdmissionAssessmentSessionDto> UpdateAsync(Guid id, AdmissionAssessmentSessionInput input)
	{
		var session = await GetAsync(id);
		AdmissionAssessmentAccessService.EnsureConcurrency(session, input.ConcurrencyStamp);
		if (session.AdmissionOfferingId != input.AdmissionOfferingId || session.WorkflowStageDefinitionId != input.WorkflowStageDefinitionId)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "The session offering and workflow stage cannot change.");
		EnsureFuture(ToUtcOffset(session.StartsAt));
		EnsureFuture(input.StartsAt);
		session.Update(input.Name, input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime, input.Location, input.Capacity);
		await sessions.UpdateAsync(session, true);
		return Map(session);
	}

	public async Task<AdmissionAssessmentSessionDto> CancelAsync(Guid id, AdmissionAssessmentActionInput input)
	{
		var session = await GetAsync(id);
		AdmissionAssessmentAccessService.EnsureConcurrency(session, input.ConcurrencyStamp);
		session.Cancel();
		await sessions.UpdateAsync(session, true);
		return Map(session);
	}

	private async Task<AdmissionAssessmentSession> GetAsync(Guid id)
	{
		var session = await sessions.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionAssessmentSession), id);
		await access.GetOfferingAsync(session.AdmissionOfferingId);
		return session;
	}
	private static void EnsureFuture(DateTimeOffset value)
	{
		if (value.UtcDateTime <= DateTime.UtcNow)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "The assessment session must start in the future.");
	}
	internal static DateTimeOffset ToUtcOffset(DateTime value) =>
		new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
	public static AdmissionAssessmentSessionDto Map(AdmissionAssessmentSession item) => new()
	{
		Id = item.Id, AdmissionOfferingId = item.AdmissionOfferingId, WorkflowStageDefinitionId = item.WorkflowStageDefinitionId,
		Name = item.Name, StartsAt = ToUtcOffset(item.StartsAt), EndsAt = ToUtcOffset(item.EndsAt), Location = item.Location, Capacity = item.Capacity,
		BookedCount = item.BookedCount, IsCancelled = item.IsCancelled, ConcurrencyStamp = item.ConcurrencyStamp
	};
}
