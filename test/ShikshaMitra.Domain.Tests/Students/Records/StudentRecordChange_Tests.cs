using System;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentRecordChange_Tests
{
	private static StudentRecordChange New(int approvals = 0, DateTimeOffset? at = null) => new(Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "communication", "Contact", "request", "Verified phone correction",
		"{}", "{}", "{}", Guid.NewGuid(), "[]", approvals, at ?? DateTimeOffset.UtcNow);
	[Fact]
	public void Requester_cannot_approve_or_reject_own_request()
	{
		var change = New(1);
		Should.Throw<BusinessException>(() => change.Approve(change.RequestedBy)).Code.ShouldBe("Students:Records:SelfApproval");
		Should.Throw<BusinessException>(() => change.Reject(change.RequestedBy)).Code.ShouldBe("Students:Records:SelfApproval");
		change.Status.ShouldBe("Pending"); change.ApprovalCount.ShouldBe(0);
	}
	[Fact]
	public void All_ordered_approvals_are_required_before_scheduling_and_application()
	{
		var change = New(3);
		change.Approve(Guid.NewGuid()); change.Approve(Guid.NewGuid());
		Should.Throw<BusinessException>(change.Schedule);
		change.Approve(Guid.NewGuid()); change.Status.ShouldBe("Approved");
		change.Schedule(); change.Apply(DateTimeOffset.UtcNow.AddSeconds(1));
		change.Status.ShouldBe("Applied"); change.AppliedAt.ShouldNotBeNull();
		Should.Throw<BusinessException>(() => change.Approve(Guid.NewGuid()));
		Should.Throw<BusinessException>(() => change.Cancel(change.RequestedBy));
	}
	[Fact]
	public void Future_change_cannot_apply_early_and_only_requester_can_cancel()
	{
		var change = New(at: DateTimeOffset.UtcNow.AddDays(1)); change.Schedule();
		Should.Throw<BusinessException>(() => change.Apply(DateTimeOffset.UtcNow));
		Should.Throw<BusinessException>(() => change.Cancel(Guid.NewGuid())).Code.ShouldBe("Students:Records:RequesterOnly");
		change.Cancel(change.RequestedBy); change.Status.ShouldBe("Cancelled");
		Should.Throw<BusinessException>(() => change.Apply(DateTimeOffset.UtcNow.AddDays(2)));
	}
	[Fact]
	public void Conflict_preserves_approved_payload_and_cannot_be_applied()
	{
		var change = New(); var payload = change.PayloadJson;
		change.MarkConflict("Students:Records:StaleSnapshot");
		change.Status.ShouldBe("Conflict"); change.PayloadJson.ShouldBe(payload);
		Should.Throw<BusinessException>(() => change.Apply(DateTimeOffset.UtcNow.AddSeconds(1)));
	}
}
