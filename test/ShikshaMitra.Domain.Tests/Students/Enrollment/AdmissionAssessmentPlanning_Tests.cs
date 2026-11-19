using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentPlanning_Tests
{
	[Fact]
	public void Shared_session_supports_only_its_explicit_offering_stage_pairs()
	{
		var first = Guid.NewGuid(); var second = Guid.NewGuid(); var firstStage = Guid.NewGuid(); var secondStage = Guid.NewGuid();
		var session = Session(3);
		session.ConfigurePlanning("exam", Guid.NewGuid(), Guid.NewGuid(), null, null, null,
			[new(Guid.NewGuid(), session.TenantId, session.Id, first, firstStage), new(Guid.NewGuid(), session.TenantId, session.Id, second, secondStage)]);
		session.Supports(first, firstStage).ShouldBeTrue(); session.Supports(second, secondStage).ShouldBeTrue();
		session.Supports(first, secondStage).ShouldBeFalse(); session.Supports(Guid.NewGuid(), firstStage).ShouldBeFalse();
		session.RoomReady.ShouldBeFalse(); session.RoomReservationStatus = 3; session.RoomReady.ShouldBeTrue(); session.RoomReservationStatus = 8; session.RoomReady.ShouldBeFalse();
	}

	[Fact]
	public void Individual_interview_slots_respect_duration_gap_end_and_capacity()
	{
		var session = Session(3);
		session.ConfigurePlanning("individualInterview", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 15, 5,
			[new(Guid.NewGuid(), session.TenantId, session.Id, session.AdmissionOfferingId, session.WorkflowStageDefinitionId)]);
		var slots = session.GetInterviewSlots(); slots.Count.ShouldBe(3);
		slots[0].StartsAt.ShouldBe(session.StartsAt); slots[1].StartsAt.ShouldBe(session.StartsAt.AddMinutes(20));
		slots.Last().EndsAt.ShouldBe(session.StartsAt.AddMinutes(55));
	}

	[Theory]
	[InlineData("online", 15, 5)]
	[InlineData("individualInterview", 0, 5)]
	[InlineData("individualInterview", 15, -1)]
	public void Invalid_modes_or_slot_layouts_are_rejected(string mode, int duration, int gap)
	{
		var session = Session(3);
		Should.Throw<BusinessException>(() => session.ConfigurePlanning(mode, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), duration, gap,
			[new(Guid.NewGuid(), session.TenantId, session.Id, session.AdmissionOfferingId, session.WorkflowStageDefinitionId)]));
	}
	[Fact]
	public void A_started_interview_session_can_book_a_future_derived_slot_but_not_past_or_invented_times()
	{
		var session = Session(3);
		session.ConfigurePlanning("individualInterview", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 15, 5,
			[new(Guid.NewGuid(), session.TenantId, session.Id, session.AdmissionOfferingId, session.WorkflowStageDefinitionId)]);
		var now = session.StartsAt.AddMinutes(10);
		Should.Throw<BusinessException>(() => session.Reserve(now, session.StartsAt));
		Should.Throw<BusinessException>(() => session.Reserve(now, session.StartsAt.AddMinutes(16)));
		session.Reserve(now, session.StartsAt.AddMinutes(20)); session.BookedCount.ShouldBe(1);
	}
	private static AdmissionAssessmentSession Session(int capacity) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Assessment",
		new DateTime(2035, 1, 2, 3, 30, 0, DateTimeKind.Utc), new DateTime(2035, 1, 2, 4, 30, 0, DateTimeKind.Utc), "Room", capacity);
}
