using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Charges;

public class ChargeWorkflowStatusResolver_Tests
{
	[Fact]
	public void Empty_or_partially_reviewed_batch_is_pending()
	{
		ChargeWorkflowStatusResolver.Resolve([]).ShouldBe(ChargeWorkflowStatus.Pending);
		ChargeWorkflowStatusResolver.Resolve([
			new(ChargeStatus.Pending, 0m), new(ChargeStatus.Approved, 0m)
		]).ShouldBe(ChargeWorkflowStatus.Pending);
	}

	[Fact]
	public void Any_unsettled_student_keeps_the_batch_in_process()
	{
		ChargeWorkflowStatusResolver.Resolve([
			new(ChargeStatus.Approved, 0m), new(ChargeStatus.Approved, 0.01m)
		]).ShouldBe(ChargeWorkflowStatus.InProcess);
	}

	[Fact]
	public void Every_approved_student_must_be_settled_to_complete()
	{
		ChargeWorkflowStatusResolver.Resolve([
			new(ChargeStatus.Approved, 0m), new(ChargeStatus.Approved, 0m)
		]).ShouldBe(ChargeWorkflowStatus.Completed);
	}

	[Fact]
	public void Rejected_students_are_excluded_from_collection_requirements()
	{
		ChargeWorkflowStatusResolver.Resolve([
			new(ChargeStatus.Approved, 0m), new(ChargeStatus.Rejected, 500m)
		]).ShouldBe(ChargeWorkflowStatus.Completed);
		ChargeWorkflowStatusResolver.Resolve([
			new(ChargeStatus.Approved, 100m), new(ChargeStatus.Rejected, 0m)
		]).ShouldBe(ChargeWorkflowStatus.InProcess);
	}

	[Fact]
	public void Non_collectible_batches_are_rejected_and_never_completed()
	{
		ChargeWorkflowStatusResolver.Resolve([
			new(ChargeStatus.Rejected, 0m), new(ChargeStatus.Rejected, 0m)
		]).ShouldBe(ChargeWorkflowStatus.Rejected);
		ChargeWorkflowStatusResolver.Resolve([
			new(ChargeStatus.Rejected, 0m), new(ChargeStatus.Voided, 0m)
		]).ShouldBe(ChargeWorkflowStatus.Rejected);
	}
}
