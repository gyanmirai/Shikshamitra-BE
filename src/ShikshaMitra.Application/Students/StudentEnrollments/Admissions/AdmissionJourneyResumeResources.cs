using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Only the seat owner can reacquire a released resource. No booking, reminder or Finance replay.</summary>
public class AdmissionJourneyResumeResources(
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	AdmissionSeatCommandService seats) : ITransientDependency
{
	public async Task RestoreAsync(AdmissionApplication application, AdmissionJourneyResumePlan plan, Guid closureId, string reason)
	{
		foreach (var stage in plan.Stages.Where(x => x.StageType == AdmissionWorkflowStageType.Seat && x.EnteredAt.HasValue &&
			x.Status is AdmissionRuntimeStageStatus.Active or AdmissionRuntimeStageStatus.Completed))
		{
			var policy = AdmissionSeatAccessService.ReadPolicy(await definitions.GetAsync(stage.DefinitionStageId));
			if (policy == null || (stage.Status == AdmissionRuntimeStageStatus.Active && policy.ReservationMode != "onEntry")) continue;
			plan.Snapshot!.SeatStates.TryGetValue(stage.Id, out var previous);
			if (stage.Status == AdmissionRuntimeStageStatus.Completed && !plan.Snapshot.SeatStates.ContainsKey(stage.Id))
				throw new BusinessException(AdmissionJourneyResumeErrorCodes.Blocked, "The previous seat outcome needs an administrator's review before resuming.");
			await seats.ReacquireForResumeAsync(application.Id, stage.Id, closureId, reason, previous);
		}
	}
}
