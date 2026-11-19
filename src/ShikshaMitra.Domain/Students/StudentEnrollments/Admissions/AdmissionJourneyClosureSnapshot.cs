using System;
using System.Linq;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Versioned, immutable evidence of the state cancelled by End Journey.</summary>
public sealed record AdmissionJourneyClosureSnapshot
{
	public int Version { get; init; } = 1;
	public Guid OfferingId { get; init; }
	public Guid CycleId { get; init; }
	public Guid WorkflowId { get; init; }
	public Guid WorkflowDefinitionId { get; init; }
	public AdmissionApplicationStatus PreviousStatus { get; init; }
	public Guid? CurrentStageId { get; init; }
	public Guid[] PendingStageIds { get; init; } = [];
	public AdmissionWorkflowInstanceStatus WorkflowStatus { get; init; }
	public DateTime? WorkflowCompletedAt { get; init; }
	public string? DecisionReason { get; init; }
	public Guid? DecisionBy { get; init; }
	public DateTime? DecisionAt { get; init; }
	public DateTime? OfferExpiresAt { get; init; }
	public DateTime? OfferAcceptedAt { get; init; }
	public Dictionary<Guid, AdmissionSeatReservationStatus> SeatStates { get; init; } = [];

	public static AdmissionJourneyClosureSnapshot Capture(AdmissionApplication application,
		AdmissionApplicationWorkflowInstance workflow, IEnumerable<AdmissionApplicationStageInstance> stages) => new()
	{
		OfferingId = application.AdmissionOfferingId, CycleId = application.AdmissionCycleId,
		WorkflowId = workflow.Id, WorkflowDefinitionId = application.WorkflowDefinitionId,
		PreviousStatus = application.Status, CurrentStageId = application.CurrentStageInstanceId,
		PendingStageIds = stages.Where(x => x.Status == AdmissionRuntimeStageStatus.Pending).Select(x => x.Id).ToArray(),
		WorkflowStatus = workflow.Status, WorkflowCompletedAt = workflow.CompletedAt,
		DecisionReason = application.DecisionReason, DecisionBy = application.DecisionBy, DecisionAt = application.DecisionAt,
		OfferExpiresAt = application.OfferExpiresAt, OfferAcceptedAt = application.OfferAcceptedAt
	};
}
