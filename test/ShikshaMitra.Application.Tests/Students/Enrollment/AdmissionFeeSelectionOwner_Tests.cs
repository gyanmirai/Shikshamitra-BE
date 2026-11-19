using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeSelectionOwner_Tests
{
	[Fact]
	public void First_Payment_on_actual_path_owns_selection_even_when_diagram_order_is_reversed()
	{
		var start = Stage(AdmissionWorkflowStageType.Custom, 0); var owner = Stage(AdmissionWorkflowStageType.Payment, 9); var later = Stage(AdmissionWorkflowStageType.Payment, 1);
		AdmissionFeeSelectionAccessService.ResolveOwner([start], []).ShouldBeNull();
		start.Complete(Guid.NewGuid(), null); owner.Enter();
		AdmissionFeeSelectionAccessService.ResolveOwner([start, owner], [Link(start, owner)]).ShouldBe(owner);
		owner.Complete(Guid.NewGuid(), null); later.Enter();
		var events = new[] { Link(owner, later), Link(start, owner) };
		AdmissionFeeSelectionAccessService.ResolveOwner([later, start, owner], events).ShouldBe(owner);
	}

	[Fact]
	public void Retired_Offer_stage_does_not_own_fee_selection()
	{
		var retired = Stage(AdmissionWorkflowStageType.Offer, 0); retired.Enter();
		AdmissionFeeSelectionAccessService.ResolveOwner([retired], []).ShouldBeNull();
	}

	private static AdmissionApplicationStageInstance Stage(AdmissionWorkflowStageType type, int order) =>
		new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "QA", "QA", type, order, type == AdmissionWorkflowStageType.Custom);
	private static AdmissionWorkflowTransitionHistory Link(AdmissionApplicationStageInstance from, AdmissionApplicationStageInstance to) =>
		new(Guid.NewGuid(), null, Guid.NewGuid(), from.WorkflowInstanceId, from.Id, to.Id, null,
			AdmissionWorkflowTransitionAction.Completed, Guid.NewGuid().ToString(), null);
}
