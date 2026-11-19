using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeCollectionDeferralValidator_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Deferral_requires_a_nonblank_reason(string? reason)
	{
		var command = Command(); command.Reason = reason;
		new AdvanceAdmissionWorkflowDtoValidator().Validate(command).IsValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public void Deferral_cannot_skip_or_override_workflow(bool skip, bool adminOverride)
	{
		var command = Command(); command.SkipCurrentStage = skip; command.AdminOverride = adminOverride;
		new AdvanceAdmissionWorkflowDtoValidator().Validate(command).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Reason_uses_existing_512_character_contract()
	{
		var command = Command(); command.Reason = new string('x', 512);
		new AdvanceAdmissionWorkflowDtoValidator().Validate(command).IsValid.ShouldBeTrue();
		command.Reason += "x";
		new AdvanceAdmissionWorkflowDtoValidator().Validate(command).IsValid.ShouldBeFalse();
	}

	private static AdvanceAdmissionWorkflowDto Command() => new()
	{
		ExpectedCurrentStageInstanceId = Guid.NewGuid(), ConcurrencyStamp = "current", IdempotencyKey = "deferral-test",
		DeferFeeCollection = true, Reason = "Parent requested collection in Onboarding"
	};
}
