using System;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Records;

public class StudentRecordLegacyOverride_Tests
{
	[Fact]
	public void Legacy_override_requires_a_reason_and_cannot_be_self_approved_or_redecided()
	{
		var requester = Guid.NewGuid();
		Should.Throw<ArgumentException>(() => new StudentRecordLegacyOverride(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), requester, "", null));
		var request = new StudentRecordLegacyOverride(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), requester, "Verified old school register", "register/2024/page-12");
		Should.Throw<BusinessException>(() => request.Decide(requester, true, null, DateTime.UtcNow)).Code.ShouldBe("Students:Records:SelfApprovalDenied");
		request.Status.ShouldBe("Pending");
		var approver = Guid.NewGuid();
		request.Decide(approver, true, "Original evidence verified", DateTime.UtcNow);
		request.Status.ShouldBe("Approved"); request.DecidedByUserId.ShouldBe(approver); request.DecidedAt.ShouldNotBeNull();
		Should.Throw<BusinessException>(() => request.Decide(Guid.NewGuid(), false, null, DateTime.UtcNow)).Code.ShouldBe("Students:Records:LegacyOverrideAlreadyDecided");
	}
}
