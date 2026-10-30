using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("activation")]
	[InlineData("completion")]
	public Task Optional_fees_preserve_manual_student_and_parent_credentials(string timing) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync(timing, emailEnabled: false, confirmFees: false);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		fixture.Case.StudentEnrollmentId.ShouldNotBeNull();
		fixture.Case.FeeStructureConfirmed.ShouldBeFalse();
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		(await WithUnitOfWorkAsync(() => requests.CountAsync(r => r.CaseId == fixture.Case.Id))).ShouldBe(0);
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.Recipients.Single(r => r.Relationship == "self").CanSend.ShouldBeTrue();
		workspace = await service.SendCredentialsAsync(fixture.Case.Id, new()
		{
			AccountType = "student", Relationship = "self", ExpectedEmail = fixture.StudentEmail,
			ConcurrencyStamp = workspace.ConcurrencyStamp
		});
		workspace = await service.SendCredentialsAsync(fixture.Case.Id, new()
		{
			AccountType = "guardian", Relationship = "father", ExpectedEmail = fixture.FatherEmail,
			ConfirmRecipientAccess = true, ConcurrencyStamp = workspace.ConcurrencyStamp
		});
		(await WithUnitOfWorkAsync(() => requests.CountAsync(r => r.CaseId == fixture.Case.Id))).ShouldBe(2);
		var current = await service.GetAsync(fixture.Case.Id);
		current.StudentEnrollmentId.ShouldBe(fixture.Case.StudentEnrollmentId);
		current.FeeStructureConfirmed.ShouldBeFalse();
		current.StudentAccountStatus.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
		current.GuardianAccountStatus.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
	});

	[Fact]
	public Task Optional_fees_do_not_bypass_missing_roll_number() => RunCertificationScenarioAsync(async () =>
	{
		var (_, item) = await PrepareCurrentCompletionCaseAsync("OptionalFeeMissingRoll");
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _onboardingCaseRepository.GetAsync(item.Id);
			stored.ClearRollNumberForPlacementChange(DateTime.UtcNow);
			await _onboardingCaseRepository.UpdateAsync(stored, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var preview = await service.PreviewActivationAsync(item.Id);
		preview.FeeStructureConfirmed.ShouldBeFalse();
		preview.LastError.ShouldNotBeNull().ShouldContain("roll number");
		(await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(item.Id,
			new() { ConcurrencyStamp = preview.ConcurrencyStamp, IdempotencyKey = "optional-fee-missing-roll" })))
			.Code.ShouldBe(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.OnboardingCaseId == item.Id))).ShouldBe(0);
	});
}
