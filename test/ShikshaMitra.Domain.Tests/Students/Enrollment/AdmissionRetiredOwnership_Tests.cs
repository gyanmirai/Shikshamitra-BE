using System;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionRetiredOwnership_Tests
{
	[Fact]
	public void Historical_policy_cannot_restore_assignment_or_assigned_only_actions()
	{
		var legacy = new AdmissionInquiryPolicy
		{
			Version = 1, Enabled = true, AssignmentMode = AdmissionCounsellorAssignmentMode.RoundRobin,
			DefaultCounsellorId = Guid.NewGuid(), CounsellorPoolUserIds = [Guid.NewGuid()],
			RestrictActionsToAssignedCounsellor = true, FollowUpDueInDays = 5, MaxOpenFollowUps = 7
		};
		var current = AdmissionInquiryPolicy.Read(JsonSerializer.Serialize(new { inquiry = legacy }))!;
		current.DefaultCounsellorId.ShouldBeNull();
		current.CounsellorPoolUserIds.ShouldBeEmpty();
		current.RestrictActionsToAssignedCounsellor.ShouldBeFalse();
		current.FollowUpDueInDays.ShouldBe(5);
		current.MaxOpenFollowUps.ShouldBe(7);
	}

	[Fact]
	public void Invalid_retired_pool_does_not_block_historical_policy_reads()
	{
		var json = """{"inquiry":{"version":1,"enabled":true,"assignmentMode":1,"defaultCounsellorId":"00000000-0000-0000-0000-000000000000","counsellorPoolUserIds":[],"restrictActionsToAssignedCounsellor":true}}""";
		var policy = AdmissionInquiryPolicy.Read(json)!;
		policy.Validate();
		policy.RestrictActionsToAssignedCounsellor.ShouldBeFalse();
	}
	[Fact]
	public void Audit_capture_preserves_action_metadata_and_overwrites_untrusted_actor_values()
	{
		var actor = Guid.NewGuid();
		var metadata = StudentJourneyActorMetadata.Capture("""{"scheduledAt":"2026-10-22T12:00:00Z","ActorName":"Untrusted"}""", actor, "Asha Sharma");
		using var parsed = JsonDocument.Parse(metadata);
		parsed.RootElement.GetProperty("scheduledAt").GetString().ShouldBe("2026-10-22T12:00:00Z");
		parsed.RootElement.GetProperty("ActorUserId").GetGuid().ShouldBe(actor);
		StudentJourneyActorMetadata.Name(metadata).ShouldBe("Asha Sharma");
	}

}
