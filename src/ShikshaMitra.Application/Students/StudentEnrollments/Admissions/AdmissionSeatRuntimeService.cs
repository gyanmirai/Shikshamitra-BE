using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionSeatRuntimeService(
	AdmissionSeatReadService reader,
	AdmissionSeatCommandService commands,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionSeatReservation, Guid> reservations) : ITransientDependency
{
	public async Task<AdmissionSeatStageDto> EnsureCompleteAsync(Guid applicationId, Guid stageId)
	{
		var state = await reader.GetAsync(applicationId, stageId);
		if (!state.CanComplete)
			throw new BusinessException(AdmissionSeatStageErrorCodes.CompletionRequired,
				state.Blockers.Count > 0 ? state.Blockers[0] : "Complete the seat allocation.");
		return state;
	}

	public async Task EnterAsync(AdmissionApplication application, AdmissionApplicationStageInstance stage)
	{
		if (stage.StageType != AdmissionWorkflowStageType.Seat) return;
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		var policy = AdmissionSeatAccessService.ReadPolicy(definition);
		if (policy?.ReservationMode != "onEntry") return;
		await commands.ReserveAsync(application.Id, stage.Id, new ReserveAdmissionSeatDto
		{
			JoinWaitlistWhenFull = policy.AllowWaitlist,
			IdempotencyKey = $"system:seat-entry:{stage.Id:N}",
			ConcurrencyStamp = application.ConcurrencyStamp
		});
	}

	public async Task EnsureApplicationCompleteAsync(AdmissionApplication application)
	{
		if (application.Status == AdmissionApplicationStatus.AdmissionFinalized) return;
		var workflow = await workflows.FindAsync(item => item.ApplicationId == application.Id);
		if (workflow == null) return;
		var visited = await stages.GetListAsync(item => item.WorkflowInstanceId == workflow.Id &&
			item.StageType == AdmissionWorkflowStageType.Seat && item.Status != AdmissionRuntimeStageStatus.Pending);
		foreach (var stage in visited)
		{
			var policy = AdmissionSeatAccessService.ReadPolicy(await definitions.GetAsync(stage.DefinitionStageId));
			if (policy == null) continue;
			var reservation = await reservations.FindAsync(item => item.ApplicationId == application.Id &&
				item.StageInstanceId == stage.Id);
			if (reservation?.Status != AdmissionSeatReservationStatus.Confirmed)
				throw new BusinessException(AdmissionSeatStageErrorCodes.CompletionRequired,
					"A confirmed admission seat is required before finalization.");
		}
	}

	public static IReadOnlyDictionary<string, string> AddFacts(
		IReadOnlyDictionary<string, string> values, AdmissionSeatStageDto? seat)
	{
		if (seat == null) return values;
		var result = new Dictionary<string, string>(values, StringComparer.Ordinal)
		{
			["seat.status"] = seat.Status,
			["seat.remainingCapacity"] = seat.RemainingCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
			["seat.isExpired"] = (seat.Status == "expired").ToString().ToLowerInvariant(),
			["seat.waitlistPosition"] = seat.WaitlistPosition?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
		};
		return result;
	}
}
