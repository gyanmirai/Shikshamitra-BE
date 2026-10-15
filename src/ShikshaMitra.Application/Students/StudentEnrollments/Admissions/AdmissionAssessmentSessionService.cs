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
		var standardIds = offerings.Select(item => item.StandardId).Distinct().ToArray();
		var classNames = (await LazyServiceProvider.LazyGetRequiredService<IRepository<ShikshaMitra.TenantSettings.Standards.Standard, Guid>>()
			.GetListAsync(item => Enumerable.Contains(standardIds, item.Id))).ToDictionary(item => item.Id, item => item.Name);
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
				ClassName = classNames.GetValueOrDefault(offering.StandardId) ?? string.Empty,
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
		var rows = await AsyncExecuter.ToListAsync((await sessions.WithDetailsAsync(item => item.Scopes)).Where(item =>
			item.Scopes.Any(scope => scope.AdmissionOfferingId == offeringId && scope.WorkflowStageDefinitionId == definitionId) ||
			!item.Scopes.Any() && item.AdmissionOfferingId == offeringId && item.WorkflowStageDefinitionId == definitionId));
		await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentSchedulingService>().EnrichReservationsAsync(rows);
		var allowed = await GetScopesAsync();
		return rows.OrderBy(item => item.StartsAt).Select(item =>
		{
			var dto = Map(item); dto.ScopeDetails = allowed.Where(scope => item.Supports(scope.AdmissionOfferingId, scope.WorkflowStageDefinitionId)).ToList();
			dto.CanManage = item.Scopes.Count == 0 || dto.ScopeDetails.Count == item.Scopes.Count;
			dto.Scopes = dto.ScopeDetails.Select(scope => new AdmissionAssessmentSessionScopeInput { AdmissionOfferingId = scope.AdmissionOfferingId, WorkflowStageDefinitionId = scope.WorkflowStageDefinitionId }).ToList();
			dto.AdmissionOfferingId = offeringId; dto.WorkflowStageDefinitionId = definitionId; return dto;
		}).ToList();
	}

	public async Task<AdmissionAssessmentSessionDto> CreateAsync(AdmissionAssessmentSessionInput input)
	{
		if (input.RoomId.HasValue || input.Scopes.Count > 0)
			return await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().CreateAsync(input);
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
		if (session.RoomId.HasValue || input.RoomId.HasValue) return await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentPlanningService>().UpdateAsync(id, input);
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
		await LazyServiceProvider.LazyGetRequiredService<IAdmissionAssessmentResourceLock>().AcquireAsync(CurrentTenant.Id, [id]);
		var session = await GetAsync(id);
		AdmissionAssessmentAccessService.EnsureConcurrency(session, input.ConcurrencyStamp);
		if (input.CancelPendingBookings)
		{
			var attemptRows = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionAssessmentAttempt, Guid>>()
				.GetListAsync(item => item.SessionId == id && item.Status != "cancelled" && item.Outcome != "waived");
			if (session.StartsAt <= DateTime.UtcNow || attemptRows.Any(item => item.Status != "pending" || item.Attendance != "pending"))
				throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Started or completed assessment evidence cannot be cancelled in bulk.");
			foreach (var attempt in attemptRows)
				await LazyServiceProvider.LazyGetRequiredService<AdmissionAssessmentCommandService>().CancelAsync(attempt.Id,
					new AdmissionAssessmentActionInput { ConcurrencyStamp = attempt.ConcurrencyStamp, Notes = input.Notes ?? "Assessment session cancelled." });
		}
		session.Cancel();
		if (session.RoomReservationId.HasValue)
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Academic.Timetable.RoomAvailability.RoomAvailabilityAppService>()
				.CancelAssessmentReservationAsync(session.RoomReservationId.Value, id, input.Notes ?? "Assessment session cancelled.");
		await sessions.UpdateAsync(session, true);
		return Map(session);
	}

	public async Task EnsureAccessibleAsync(Guid id) => await GetAsync(id, false);
	internal async Task<AdmissionAssessmentSession> GetAsync(Guid id, bool requireAll = true)
	{
		var session = await AsyncExecuter.FirstOrDefaultAsync((await sessions.WithDetailsAsync(item => item.Scopes)).Where(item => item.Id == id)) ?? throw new EntityNotFoundException(typeof(AdmissionAssessmentSession), id);
		var ids = session.Scopes.Select(item => item.AdmissionOfferingId).Append(session.AdmissionOfferingId).Distinct().ToArray();
		var allowed = await AsyncExecuter.CountAsync((await access.GetScopedOfferingsAsync()).Where(item => Enumerable.Contains(ids, item.Id)));
		if (allowed == 0 || requireAll && allowed != ids.Length) throw new EntityNotFoundException(typeof(AdmissionAssessmentSession), id);
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
		Scopes = item.Scopes.Select(scope => new AdmissionAssessmentSessionScopeInput { AdmissionOfferingId = scope.AdmissionOfferingId, WorkflowStageDefinitionId = scope.WorkflowStageDefinitionId }).ToList(),
		SessionType = item.SessionType, RoomId = item.RoomId, RoomReservationId = item.RoomReservationId, RoomReservationStatus = item.RoomReservationStatus, InterviewerId = item.InterviewerId,
		SlotDurationMinutes = item.SlotDurationMinutes, BreakMinutes = item.BreakMinutes,
		Name = item.Name, StartsAt = ToUtcOffset(item.StartsAt), EndsAt = ToUtcOffset(item.EndsAt), Location = item.Location, Capacity = item.Capacity,
		BookedCount = item.BookedCount, IsCancelled = item.IsCancelled, ConcurrencyStamp = item.ConcurrencyStamp
	};
}
