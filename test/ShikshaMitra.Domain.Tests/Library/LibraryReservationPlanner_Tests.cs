using System;
using System.Linq;
using ShikshaMitra.Library.Reservations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReservationPlanner_Tests
{
	[Fact]
	public void Head_At_Checkout_Quota_Should_Still_Receive_The_First_Hold()
	{
		var head = Waiting(1) with { AtCheckoutLimit = true };
		var later = Waiting(2);
		var copy = Copy(1);

		var plan = LibraryFifoAllocationPlanner.Plan([head, later], [copy]);

		plan.Allocations.Single().ShouldBe(new LibraryReservationAllocation(head.ReservationId, copy.CopyId));
		plan.PermanentlyIneligibleRequests.ShouldBeEmpty();
	}

	[Fact]
	public void Allocations_Should_Follow_Server_Sequence_And_Deterministic_Copy_Order()
	{
		var head = Waiting(1);
		var later = Waiting(2);
		var firstCopy = Copy(1);
		var secondCopy = Copy(2);

		var plan = LibraryFifoAllocationPlanner.Plan([later, head], [secondCopy, firstCopy]);

		plan.Allocations.ShouldBe([
			new LibraryReservationAllocation(head.ReservationId, firstCopy.CopyId),
			new LibraryReservationAllocation(later.ReservationId, secondCopy.CopyId)
		]);
	}

	[Fact]
	public void Copies_After_All_Waiting_Demand_Should_Remain_Unallocated_Surplus()
	{
		var head = Waiting(1);
		var firstCopy = Copy(1);
		var secondCopy = Copy(2);

		var plan = LibraryFifoAllocationPlanner.Plan([head], [firstCopy, secondCopy]);

		plan.Allocations.Single().CopyId.ShouldBe(firstCopy.CopyId);
		plan.Allocations.ShouldNotContain(row => row.CopyId == secondCopy.CopyId);
	}

	[Fact]
	public void Existing_Ready_Holds_Should_Not_Be_Reshuffled_Or_Allocated_Again()
	{
		var ready = Waiting(1) with { State = LibraryReservationState.Ready };
		var waiting = Waiting(2);
		var remainingCopy = Copy(2);

		var plan = LibraryFifoAllocationPlanner.Plan([ready, waiting], [remainingCopy]);

		plan.Allocations.Single().ShouldBe(new LibraryReservationAllocation(waiting.ReservationId, remainingCopy.CopyId));
	}

	[Fact]
	public void An_Unresolved_Legacy_Head_Should_Block_Later_Allocations_Without_Guessing_Its_Order()
	{
		var unresolved = Waiting(1) with { Sequence = null, State = LibraryReservationState.NeedsReview,
			Eligibility = LibraryReservationEligibility.NeedsReview };

		var plan = LibraryFifoAllocationPlanner.Plan([Waiting(2), unresolved], [Copy(1)]);

		plan.BlockedByReview.ShouldBeTrue();
		plan.Allocations.ShouldBeEmpty();
	}

	[Fact]
	public void Permanent_Loss_Of_Eligibility_Should_Retain_A_Cancellation_Decision_And_Allow_The_Next_Head()
	{
		var inactive = Waiting(1) with { Eligibility = LibraryReservationEligibility.PermanentlyIneligible };
		var next = Waiting(2);
		var copy = Copy(1);

		var plan = LibraryFifoAllocationPlanner.Plan([inactive, next], [copy]);

		plan.PermanentlyIneligibleRequests.ShouldBe([inactive.ReservationId]);
		plan.Allocations.Single().ReservationId.ShouldBe(next.ReservationId);
	}

	private static LibraryReservationQueueEntry Waiting(long sequence) => new(Guid.NewGuid(), sequence,
		LibraryReservationState.Waiting, LibraryReservationEligibility.Eligible);
	private static LibraryReservationAvailableCopy Copy(int number) => new(Guid.NewGuid(), number);
}
