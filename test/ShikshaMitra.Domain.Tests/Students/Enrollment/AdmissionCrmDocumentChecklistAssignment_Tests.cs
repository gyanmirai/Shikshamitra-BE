using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentChecklistAssignment_Tests
{
	[Fact]
	public void Should_Require_Distinct_Users_For_Two_Person_Readiness_Approval()
	{
		var firstApprover = Guid.NewGuid();
		var secondApprover = Guid.NewGuid();
		var at = DateTime.UtcNow;
		var assignment = new AdmissionCrmDocumentChecklistAssignment(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), 1, "{}", at);

		assignment.ConfirmReadiness(firstApprover, 2, false, null, at).ShouldBeFalse();
		assignment.ReadinessApprovalCount.ShouldBe(1);
		assignment.ReadinessConfirmedAt.ShouldBeNull();

		var exception = Should.Throw<BusinessException>(() =>
			assignment.ConfirmReadiness(firstApprover, 2, false, null, at.AddMinutes(1)));
		exception.Code.ShouldBe("AdmissionCrm:DistinctSecondReadinessApproverRequired");

		assignment.ConfirmReadiness(secondApprover, 2, false, null, at.AddMinutes(2)).ShouldBeTrue();
		assignment.ReadinessApprovalCount.ShouldBe(2);
		assignment.ReadinessConfirmedByUserId.ShouldBe(secondApprover);
	}
}
