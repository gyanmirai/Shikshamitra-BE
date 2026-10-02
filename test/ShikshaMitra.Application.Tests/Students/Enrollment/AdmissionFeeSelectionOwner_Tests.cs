using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeSelectionOwner_Tests
{
	[Theory]
	[InlineData(AdmissionWorkflowStageType.Offer, AdmissionWorkflowStageType.Payment)]
	[InlineData(AdmissionWorkflowStageType.Payment, AdmissionWorkflowStageType.Offer)]
	public void Actual_transition_path_decides_owner_even_when_diagram_order_is_reversed(
		AdmissionWorkflowStageType first, AdmissionWorkflowStageType second)
	{
		var start = Stage(AdmissionWorkflowStageType.Custom, 0); var owner = Stage(first, 9); var later = Stage(second, 1);
		AdmissionFeeSelectionAccessService.ResolveOwner([start], []).ShouldBeNull();
		start.Complete(Guid.NewGuid(), null); owner.Enter();
		AdmissionFeeSelectionAccessService.ResolveOwner([start, owner], [Link(start, owner)]).ShouldBe(owner);
		owner.Complete(Guid.NewGuid(), null); later.Enter();
		var events = new[] { Link(owner, later), Link(start, owner) };
		AdmissionFeeSelectionAccessService.ResolveOwner([later, start, owner], events).ShouldBe(owner);
	}

	private static AdmissionApplicationStageInstance Stage(AdmissionWorkflowStageType type, int order) =>
		new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "QA", "QA", type, order, type == AdmissionWorkflowStageType.Custom);
	private static AdmissionWorkflowTransitionHistory Link(AdmissionApplicationStageInstance from, AdmissionApplicationStageInstance to) =>
		new(Guid.NewGuid(), null, Guid.NewGuid(), from.WorkflowInstanceId, from.Id, to.Id, null,
			AdmissionWorkflowTransitionAction.Completed, Guid.NewGuid().ToString(), null);
}
