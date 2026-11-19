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
	[Fact]
	public Task Readiness_operational_handoffs_exclude_retired_failures_without_deleting_history() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false, accountsEnabled: false);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var repository = GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>();
		var finance = new StudentOnboardingServiceHandoff(Guid.NewGuid(), TestTenantId, fixture.Case.Id,
			"finance-first-payment", true, "readiness-legacy-fee");
		finance.Fail("Historical first payment is missing.", DateTime.UtcNow);
		var consent = new StudentOnboardingServiceHandoff(Guid.NewGuid(), TestTenantId, fixture.Case.Id,
			"service-consent", true, "readiness-legacy-consent");
		consent.Fail("Historical blanket consent is missing.", DateTime.UtcNow);
		var optional = new StudentOnboardingServiceHandoff(Guid.NewGuid(), TestTenantId, fixture.Case.Id,
			"library", false, "readiness-optional-library");
		optional.Fail("Optional library service is not prepared.", DateTime.UtcNow);
		await WithUnitOfWorkAsync(async () =>
		{
			await repository.InsertAsync(finance, true);
			await repository.InsertAsync(consent, true);
			await repository.InsertAsync(optional, true);
		});

		var handoffs = await service.GetServiceHandoffsAsync(fixture.Case.Id);
		handoffs.ShouldNotContain(value => value.ServiceKey == "finance-first-payment");
		handoffs.ShouldNotContain(value => value.ServiceKey == "service-consent");
		handoffs.ShouldHaveSingleItem().Required.ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => repository.GetAsync(finance.Id))).Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Failed);
		(await WithUnitOfWorkAsync(() => repository.GetAsync(consent.Id))).Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Failed);
		var item = await service.GetAsync(fixture.Case.Id);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed);
		item.FeeStructureConfirmed.ShouldBeTrue();
		item.FirstPaymentConfirmed.ShouldBeNull();
		item.RequiredServiceHandoffs.ShouldBeEmpty();
		item.RequiredCredentialDeliveryConfirmed.ShouldBeNull();
		item.ProgressPercent.ShouldBe(100);
	});

	[Theory]
	[InlineData(StudentOnboardingServiceHandoffStatus.Pending)]
	[InlineData(StudentOnboardingServiceHandoffStatus.Failed)]
	[InlineData(StudentOnboardingServiceHandoffStatus.Acknowledged)]
	public Task Readiness_case_and_list_project_required_account_handoffs_without_changing_saved_status(StudentOnboardingServiceHandoffStatus status) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false,
			studentAccess: "required", guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>();
			var handoff = await repository.GetAsync(value => value.OnboardingCaseId == fixture.Case.Id && value.ServiceKey == "student-account");
			if (status == StudentOnboardingServiceHandoffStatus.Pending) handoff.QueueRetry();
			else if (status == StudentOnboardingServiceHandoffStatus.Acknowledged) handoff.Acknowledge(DateTime.UtcNow);
			else handoff.Fail("Required student account is not linked.", DateTime.UtcNow);
			await repository.UpdateAsync(handoff, true);
		});
		var before = await WithUnitOfWorkAsync(() => _onboardingCaseRepository.GetAsync(fixture.Case.Id));
		var beforeStatus = before.Status; var beforeStamp = before.ConcurrencyStamp;
		var item = await service.GetAsync(fixture.Case.Id);
		var handoffRead = item.RequiredServiceHandoffs.ShouldHaveSingleItem();
		handoffRead.ServiceKey.ShouldBe("student-account"); handoffRead.Required.ShouldBeTrue(); handoffRead.Status.ShouldBe(status);
		item.RequiredCredentialDeliveryConfirmed.ShouldBeNull();
		if (status == StudentOnboardingServiceHandoffStatus.Acknowledged) item.ProgressPercent.ShouldBe(100);
		else item.ProgressPercent.ShouldBeLessThan(100);
		var listed = (await service.GetListAsync(new() { AcademicYearId = item.AcademicYearId })).Items.Single(value => value.Id == item.Id);
		listed.RequiredServiceHandoffs.ShouldHaveSingleItem().Status.ShouldBe(status);
		listed.ProgressPercent.ShouldBe(item.ProgressPercent);
		var after = await WithUnitOfWorkAsync(() => _onboardingCaseRepository.GetAsync(item.Id));
		after.Status.ShouldBe(beforeStatus); after.ConcurrencyStamp.ShouldBe(beforeStamp);
	});

	[Fact]
	public Task Readiness_completed_required_accounts_remain_in_the_checklist() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false,
			studentAccess: "required", guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var before = await service.GetAsync(fixture.Case.Id);
		before.RequiredServiceHandoffs.ShouldHaveSingleItem().Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Failed);
		await service.ProvisionPortalAccountAsync(before.Id, new() { AccountType = "student", Relationship = "self",
			Email = fixture.StudentEmail, RelationshipVerified = true, ConcurrencyStamp = before.ConcurrencyStamp });
		var completed = await service.GetAsync(before.Id);
		completed.Status.ShouldBe(StudentOnboardingStatus.Completed);
		completed.RequiredServiceHandoffs.ShouldHaveSingleItem().Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
		completed.ProgressPercent.ShouldBe(100); completed.RequiredCredentialDeliveryConfirmed.ShouldBeNull();
	});

	[Fact]
	public Task Readiness_legacy_consent_remains_required_and_can_be_acknowledged() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false,
			studentAccess: "required", guardianAccess: "not-needed");
		await WithUnitOfWorkAsync(async () =>
		{
			var item = await _onboardingCaseRepository.GetAsync(fixture.Case.Id);
			item.ExtraProperties.Remove("ReadinessRulesSnapshot"); await _onboardingCaseRepository.UpdateAsync(item, true);
			var consent = new StudentOnboardingServiceHandoff(Guid.NewGuid(), TestTenantId, item.Id, "service-consent", true, "readiness-applicable-consent");
			consent.Fail("Confirm the saved consent reference.", DateTime.UtcNow);
			await GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>().InsertAsync(consent, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var before = await service.GetAsync(fixture.Case.Id);
		before.RequiredServiceHandoffs.Single(value => value.ServiceKey == "service-consent").Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Failed);
		(await service.GetServiceHandoffsAsync(before.Id)).ShouldContain(value => value.ServiceKey == "service-consent");
		var after = await service.ConfirmServiceConsentAsync(before.Id,
			new() { ConcurrencyStamp = before.ConcurrencyStamp, EvidenceReference = "Saved legacy service consent reference QA-01" });
		after.RequiredServiceHandoffs.Single(value => value.ServiceKey == "service-consent").Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
		after.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
	});

	[Fact]
	public Task Readiness_retired_handoffs_cannot_be_retried_or_confirmed() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false,
			studentAccess: "required", guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var finance = new StudentOnboardingServiceHandoff(Guid.NewGuid(), TestTenantId, fixture.Case.Id, "finance-first-payment", true, "readiness-retired-retry");
		finance.Fail("Historical first payment is missing.", DateTime.UtcNow);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>();
			await repository.InsertAsync(finance, true);
			await repository.InsertAsync(new(Guid.NewGuid(), TestTenantId, fixture.Case.Id, "service-consent", true, "readiness-retired-confirm"), true);
		});
		(await Should.ThrowAsync<BusinessException>(() => service.RetryServiceHandoffAsync(fixture.Case.Id, finance.Id))).Code.ShouldBe(StudentOnboardingErrorCodes.CaseNotEditable);
		var before = await service.GetAsync(fixture.Case.Id);
		(await Should.ThrowAsync<BusinessException>(() => service.ConfirmServiceConsentAsync(before.Id,
			new() { ConcurrencyStamp = before.ConcurrencyStamp, EvidenceReference = "Obsolete consent must not be changed" }))).Code.ShouldBe(StudentOnboardingErrorCodes.ServiceConsentInvalid);
	});

	[Fact]
	public Task Readiness_required_activation_delivery_is_pending_failed_then_confirmed_without_sending_on_read() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", studentAccess: "required", guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(fixture.Case.Id);
		await service.ProvisionPortalAccountAsync(item.Id, new() { AccountType = "student", Relationship = "self", Email = fixture.StudentEmail,
			RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await service.GetAsync(item.Id);
		item.RequiredServiceHandoffs.ShouldAllBe(value => value.Status == StudentOnboardingServiceHandoffStatus.Acknowledged);
		item.RequiredCredentialDeliveryConfirmed.ShouldBe(false); item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		(await WithUnitOfWorkAsync(() => requests.CountAsync(value => value.CaseId == item.Id))).ShouldBe(0);
		var link = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetAsync(value => value.StudentId == item.StudentId && value.AccountType == "student"));
		var request = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, item.Id, item.StudentId, link.Id, link.UserId,
			"student", fixture.StudentEmail, TestUserId, DateTime.UtcNow);
		request.RequireSavedOwnerValidation(TestUserId);
		await WithUnitOfWorkAsync(() => requests.InsertAsync(request, true));
		(await service.GetAsync(item.Id)).RequiredCredentialDeliveryConfirmed.ShouldBe(false);
		await WithUnitOfWorkAsync(async () => { var saved = await requests.GetAsync(request.Id); saved.Failed(DateTime.UtcNow); await requests.UpdateAsync(saved, true); });
		(await service.GetAsync(item.Id)).RequiredCredentialDeliveryConfirmed.ShouldBe(false);
		await WithUnitOfWorkAsync(async () => { var saved = await requests.GetAsync(request.Id); saved.Sent(DateTime.UtcNow); await requests.UpdateAsync(saved, true); });
		item = await service.GetAsync(item.Id);
		item.RequiredCredentialDeliveryConfirmed.ShouldBe(true); item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
		item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed); item.RequiredCredentialDeliveryConfirmed.ShouldBe(true);
		(await WithUnitOfWorkAsync(() => requests.CountAsync(value => value.CaseId == item.Id))).ShouldBe(1);
	});

	[Fact]
	public Task Readiness_completion_timed_delivery_does_not_add_an_activation_gate() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("completion", studentAccess: "required", guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(fixture.Case.Id);
		await service.ProvisionPortalAccountAsync(item.Id, new() { AccountType = "student", Relationship = "self", Email = fixture.StudentEmail,
			RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await service.GetAsync(item.Id);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed); item.RequiredCredentialDeliveryConfirmed.ShouldBeNull();
	});

	[Fact]
	public Task Readiness_optional_delivery_failure_does_not_add_a_required_check() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", studentAccess: "optional", guardianAccess: "not-needed", provisioningMode: "automatic");
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		await WithUnitOfWorkAsync(async () =>
		{
			var request = await requests.GetAsync(value => value.CaseId == fixture.Case.Id);
			request.Failed(DateTime.UtcNow); await requests.UpdateAsync(request, true);
		});
		var item = await GetRequiredService<IStudentOnboardingAppService>().GetAsync(fixture.Case.Id);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed); item.CredentialsPending.ShouldBeTrue();
		item.RequiredServiceHandoffs.ShouldBeEmpty(); item.RequiredCredentialDeliveryConfirmed.ShouldBeNull();
	});
}
