using System;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentRollNumberBatch_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 16, 4, 0, 0, TimeSpan.Zero);
	private static StudentRollNumberBatch Batch(string operation = "assign-missing", string? issue = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), operation, "hash", "{}", JsonSerializer.Serialize(new[] { new StudentRollBatchRow(Guid.NewGuid(), Guid.NewGuid(), null, "Jay Shah", "student-stamp", "case-stamp", null, 1, issue) }), "{}", Now);
	[Fact] public void Direct_assignment_is_a_single_explicit_lifecycle()
	{
		var batch = Batch(); batch.Submit("request-1", "", "[]", Now); batch.Status.ShouldBe("Approved"); batch.Apply(Now); batch.Status.ShouldBe("Applied");
		Should.Throw<BusinessException>(() => batch.Apply(Now)).Code.ShouldBe(StudentRollNumberErrorCodes.InvalidState);
	}
	[Fact] public void Renumber_requires_a_reason() => Should.Throw<BusinessException>(() => Batch("renumber").Submit("key", "", "[]", Now)).Code.ShouldBe(StudentRollNumberErrorCodes.InvalidBatch);
	[Fact] public void Stale_previews_cannot_be_submitted() => Should.Throw<BusinessException>(() => Batch().Submit("key", "", "[]", Now.AddMinutes(16))).Code.ShouldBe(StudentRollNumberErrorCodes.StalePreview);
	[Fact] public void Blocked_rows_cannot_partially_commit() => Should.Throw<BusinessException>(() => Batch(issue: "Missing gender").Submit("key", "", "[]", Now)).Code.ShouldBe(StudentRollNumberErrorCodes.BatchBlocked);
	[Fact] public void Grouped_approval_prevents_self_or_repeated_approvers()
	{
		var batch = Batch("renumber"); batch.Submit("key", "Reviewed roster", "[{},{}]", Now);
		Should.Throw<BusinessException>(() => batch.Decide(batch.RequestedBy, true, "", Now));
		var approver = Guid.NewGuid(); batch.Decide(approver, true, "Reviewed", Now); batch.Status.ShouldBe("PendingApproval");
		Should.Throw<BusinessException>(() => batch.Decide(approver, true, "", Now));
		batch.Decide(Guid.NewGuid(), true, "Reviewed", Now); batch.Status.ShouldBe("Approved"); batch.Apply(Now);
	}
	[Fact] public void Rejection_never_applies_numbers()
	{
		var batch = Batch("renumber"); batch.Submit("key", "Reviewed roster", "[{}]", Now); batch.Decide(Guid.NewGuid(), false, "Not accepted", Now);
		batch.Status.ShouldBe("Rejected"); Should.Throw<BusinessException>(() => batch.Apply(Now));
	}
}
