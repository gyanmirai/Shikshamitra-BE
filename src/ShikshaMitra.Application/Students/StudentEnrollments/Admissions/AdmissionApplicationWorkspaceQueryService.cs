using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Derives the permanent workspace handoff from persisted workflow evidence.</summary>
[RemoteService(false)]
public class AdmissionApplicationWorkspaceQueryService(
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionWorkflowTransitionHistory, Guid> history) : ITransientDependency
{
	public async Task<IQueryable<AdmissionApplicationWorkspaceRow>> ProjectAsync(IQueryable<AdmissionApplication> applications)
	{
		var workflowQuery = await workflows.GetQueryableAsync();
		var stageQuery = await stages.GetQueryableAsync();
		var historyQuery = await history.GetQueryableAsync();
		// Retired offer/seat/enrollment stages are downstream compatibility boundaries for pinned workflows.
		// Review is deliberately excluded: entering review does not mean entering Admission decision.
		var decisionStages = from stage in stageQuery
			join workflow in workflowQuery on stage.WorkflowInstanceId equals workflow.Id
			where stage.StageType == AdmissionWorkflowStageType.Approval || stage.StageType == AdmissionWorkflowStageType.Offer ||
				stage.StageType == AdmissionWorkflowStageType.Seat || stage.StageType == AdmissionWorkflowStageType.Enrollment
			select new { Stage = stage, workflow.ApplicationId };
		return applications.Select(application => new AdmissionApplicationWorkspaceRow
		{
			Application = application,
			HasReachedDecision = decisionStages.Any(row => row.ApplicationId == application.Id &&
				(row.Stage.EnteredAt.HasValue || row.Stage.Id == application.CurrentStageInstanceId ||
					historyQuery.Any(item => item.ApplicationId == application.Id &&
						(item.ToStageInstanceId == row.Stage.Id || item.FromStageInstanceId == row.Stage.Id)))) ||
				(!decisionStages.Any(row => row.ApplicationId == application.Id && row.Stage.StageType == AdmissionWorkflowStageType.Approval) &&
					(application.Status == AdmissionApplicationStatus.Approved || application.Status == AdmissionApplicationStatus.Waitlisted ||
						application.Status == AdmissionApplicationStatus.Rejected)) || application.Status == AdmissionApplicationStatus.Offered ||
				application.Status == AdmissionApplicationStatus.OfferAccepted || application.Status == AdmissionApplicationStatus.OfferDeclined ||
				application.Status == AdmissionApplicationStatus.AdmissionFinalized || application.Status == AdmissionApplicationStatus.Enrolled,
			DecisionEnteredAt = decisionStages.Where(row => row.ApplicationId == application.Id)
				.Select(row => row.Stage.EnteredAt).Min() ??
				historyQuery.Where(item => item.ApplicationId == application.Id && decisionStages.Any(row => row.ApplicationId == application.Id &&
					(item.ToStageInstanceId == row.Stage.Id || item.FromStageInstanceId == row.Stage.Id)))
					.Select(item => (DateTime?)item.OccurredAt).Min() ?? application.DecisionAt ?? application.FinalizedAt
		});
	}
}

public class AdmissionApplicationWorkspaceRow
{
	public AdmissionApplication Application { get; set; } = null!;
	public bool HasReachedDecision { get; set; }
	public DateTime? DecisionEnteredAt { get; set; }
}
