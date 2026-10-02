using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task GrantCompletedRecordAsync(Student student)
	{
		var offering = await WithUnitOfWorkAsync(async () => (await _offeringRepository.GetListAsync(o => o.StandardId == student.StandardId)).First());
		var applicant = await CreateCertificationApplicantAsync($"Record-{Guid.NewGuid():N}", true, true, student.Id);
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await _studentRepository.GetAsync(student.Id);
			current.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.UpdateAsync(current, autoSave: true);
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, student.Id, student.AcademicYearId!.Value, student.StandardId, student.DivisionId);
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(enrollment, autoSave: true);
			var completed = new StudentOnboardingCase(Guid.NewGuid(), TestTenantId, application.Id, applicant.Id, student.Id, offering.Id,
				student.AcademicYearId.Value, student.AcademicBoardId!.Value, student.AcademicMediumId, student.AcademicMediumStageId!.Value,
				student.StandardId ?? Guid.NewGuid(), $"record-fixture-{student.Id:N}", null, DateTime.UtcNow.AddDays(3));
			completed.CompleteHistoricalImport(enrollment.Id, student.DivisionId, student.RollNo, DateTime.UtcNow);
			await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(completed, autoSave: true);
		});
	}

	private async Task PublishDirectoryPolicyAsync(StudentRecordsPolicy policy)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var config = new StudentRecordsConfiguration(Guid.NewGuid(), TestTenantId, null, policy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().InsertAsync(config, autoSave: true);
			var version = new StudentRecordsPolicyVersion(Guid.NewGuid(), TestTenantId, config.Id, null, 1, policy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().InsertAsync(version, autoSave: true);
			config.Publish(version.Id);
			await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().UpdateAsync(config, autoSave: true);
		});
	}

	[Theory]
	[InlineData("OnboardingOnly")]
	[InlineData("Summary")]
	[InlineData("Full")]
	public Task Directory_excludes_provisioning_while_direct_profile_obeys_published_school_mode(string mode) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "ProvisioningDirectory", "Student");
		var applicant = await CreateCertificationApplicantAsync("Provisioning", true, true, student.Id);
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await _studentRepository.GetAsync(student.Id); current.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.UpdateAsync(current, autoSave: true);
			var enrollmentId = Guid.NewGuid();
			var onboarding = new StudentOnboardingCase(Guid.NewGuid(), TestTenantId, application.Id, applicant.Id, student.Id, offering.Id,
				student.AcademicYearId!.Value, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId,
				$"provisioning-record-{student.Id:N}", null, DateTime.UtcNow.AddDays(1));
			onboarding.RecordPlacement(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
			onboarding.RecordIdentifier(null, 1, DateTime.UtcNow); onboarding.MarkReady(DateTime.UtcNow); onboarding.BeginProvisioning(enrollmentId);
			await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(onboarding, autoSave: true);
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(StudentEnrollment.Create(enrollmentId, TestTenantId,
				student.Id, student.AcademicYearId.Value, student.StandardId, null, null, onboarding.Id), autoSave: true);
		});
		var policy = StudentRecordsPolicy.Starter(); policy.Provisioning.Mode = mode;
		await PublishDirectoryPolicyAsync(policy);
		(await GetRequiredService<IStudentDirectoryAppService>().GetListAsync(new() { Search = "ProvisioningDirectory" })).TotalCount.ShouldBe(0);
		(await GetRequiredService<IStudentDirectoryAppService>().GetLegacyCandidatesAsync(new() { Search = "ProvisioningDirectory" })).TotalCount.ShouldBe(0);
		var profileService = GetRequiredService<IStudentRecordProfileAppService>();
		if (mode == "OnboardingOnly")
			(await Should.ThrowAsync<BusinessException>(() => profileService.GetAsync(student.Id))).Code.ShouldBe("Students:Records:OnboardingIncomplete");
		else
		{
			var profile = await profileService.GetAsync(student.Id); profile.RecordsAccess.ShouldBe(mode); profile.PlacementEditable.ShouldBeFalse();
			if (mode == "Summary") { profile.EnrollmentHistory.ShouldBeEmpty(); profile.DateOfBirth.ShouldBeNull(); }
			else profile.EnrollmentHistory.Count.ShouldBe(1);
		}
	});

	[Fact]
	public Task Directory_options_are_scoped_and_completed_identity_stays_visible_during_renewal_provisioning() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "RenewalPermanent", "Student"); await GrantCompletedRecordAsync(student);
		var service = GetRequiredService<IStudentDirectoryAppService>();
		var options = await service.GetOptionsAsync(student.AcademicYearId);
		options.AcademicYears.ShouldContain(y => y.Id == student.AcademicYearId);
		options.Boards.ShouldContain(b => b.Id == offering.BoardId && b.ParentId == student.AcademicYearId);
		options.Standards.ShouldContain(c => c.Id == offering.StandardId && c.ParentId == offering.MediumId);
		var noScopeId = Guid.NewGuid(); await EnsureTestIdentityUserAsync(noScopeId, $"directory-options-{noScopeId:N}");
		using (_principalAccessor.Change(ProfilePrincipal(noScopeId))) (await service.GetOptionsAsync()).AcademicYears.ShouldBeEmpty();
		await WithUnitOfWorkAsync(async () =>
		{
			var enrollment = await GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(e => e.StudentId == student.Id && e.TenantId == TestTenantId);
			var renewal = new StudentRecordRenewalCase(Guid.NewGuid(), TestTenantId, student.Id, student.AcademicYearId!.Value,
				student.StandardId!.Value, student.DivisionId ?? Guid.NewGuid(), student.RollNo, null, StudentRecordsPolicy.Starter().ToJson(), "{}", "{}", "renewal-visibility");
			renewal.Activate(enrollment.Id); await GetRequiredService<IRepository<StudentRecordRenewalCase, Guid>>().InsertAsync(renewal, true);
		});
		(await service.GetListAsync(new() { StudentId = student.Id })).TotalCount.ShouldBe(1);
		var profile = await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(student.Id);
		profile.RecordsAccess.ShouldBe("Full"); profile.EntrySource.ShouldBe("CompletedOnboarding"); profile.PlacementEditable.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordsAccessService>().EnsureWritableAsync(student.Id, true)))).Code.ShouldBe("Students:Records:ReadOnly");
	});

	[Fact]
	public Task Directory_and_profile_mask_identity_using_database_roles_and_search_does_not_disclose_hidden_names() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "MaskedDirectory", "Student");
		await GrantCompletedRecordAsync(student);
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks.Add(new() { FieldGroup = "Identity", AllowedRoleNames = [] });
		await PublishDirectoryPolicyAsync(policy);
		var profile = await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(student.Id);
		profile.FirstName.ShouldBeNull(); profile.DateOfBirth.ShouldBeNull(); profile.MaskedFields.ShouldContain("Identity");
		var directory = GetRequiredService<IStudentDirectoryAppService>();
		(await directory.GetListAsync(new() { Search = "MaskedDirectory" })).TotalCount.ShouldBe(0);
		var row = (await directory.GetListAsync(new())).Items.Single(r => r.Id == student.Id);
		row.FirstName.ShouldBeNull(); row.GrNumber.ShouldBeNull(); row.MaskedFields.ShouldContain("Identity");
	});

	[Fact]
	public Task Directory_excludes_admission_only_and_includes_completed_active_and_former_with_server_paging() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var first = await CreateExistingStudentAsync(offering, "DirectoryA", "Student");
		var second = await CreateExistingStudentAsync(offering, "DirectoryB", "Student");
		var pending = await CreateExistingStudentAsync(offering, "DirectoryC", "Pending");
		await GrantCompletedRecordAsync(first); await GrantCompletedRecordAsync(second);
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(second.Id);
			student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
			await _studentRepository.UpdateAsync(student, autoSave: true);
		});
		var service = GetRequiredService<IStudentDirectoryAppService>();
		var page = await service.GetListAsync(new() { Search = "Directory", MaxResultCount = 1, Sorting = "name asc" });
		page.TotalCount.ShouldBe(2); page.Items.Count.ShouldBe(1); page.Items[0].Id.ShouldBe(first.Id);
		var next = await service.GetListAsync(new() { Search = "Directory", SkipCount = 1, MaxResultCount = 1 });
		next.Items.Single().Id.ShouldBe(second.Id);
		(await service.GetListAsync(new() { Search = "Directory", Population = "Former" })).Items.Single().Id.ShouldBe(second.Id);
		(await service.GetListAsync(new() { Search = "Directory", Population = "Active" })).Items.Single().Id.ShouldBe(first.Id);
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentRecordProfileAppService>().GetAsync(pending.Id))).Code.ShouldBe("Students:Records:OnboardingIncomplete");
	});

	[Fact]
	public Task Directory_requires_scope_and_explicit_tenant_even_if_filters_are_disabled() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "DirectoryScoped", "Student");
		await GrantCompletedRecordAsync(student);
		var service = GetRequiredService<IStudentDirectoryAppService>();
		var noScopeId = Guid.NewGuid(); await EnsureTestIdentityUserAsync(noScopeId, $"directory-scope-{noScopeId:N}");
		using (_principalAccessor.Change(ProfilePrincipal(noScopeId)))
			(await service.GetListAsync(new() { Search = "DirectoryScoped" })).TotalCount.ShouldBe(0);
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetListAsync(new() { Search = "DirectoryScoped" }));
	});

	[Fact]
	public Task Legacy_override_denies_cancelled_modern_onboarding_despite_existing_active_enrollment() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "CancelledModernRecord", "Student");
		var applicant = await CreateCertificationApplicantAsync("CancelledModern", true, true, student.Id);
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await _studentRepository.GetAsync(student.Id); current.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.UpdateAsync(current, true);
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, student.Id, student.AcademicYearId!.Value, student.StandardId, student.DivisionId);
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(enrollment, true);
			var onboarding = new StudentOnboardingCase(Guid.NewGuid(), TestTenantId, application.Id, applicant.Id, student.Id, offering.Id,
				student.AcademicYearId.Value, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId,
				$"cancelled-modern-record-{student.Id:N}", null, DateTime.UtcNow.AddDays(1));
			onboarding.Cancel("Modern onboarding cancelled before completion", DateTime.UtcNow);
			await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(onboarding, true);
		});
		var service = GetRequiredService<IStudentDirectoryAppService>();
		(await service.GetLegacyCandidatesAsync(new() { StudentId = student.Id })).TotalCount.ShouldBe(0);
		(await Should.ThrowAsync<BusinessException>(() => service.RequestLegacyOverrideAsync(student.Id, new() { Reason = "Cannot bypass modern cancelled onboarding" })))
			.Code.ShouldBe("Students:Records:LegacyStudentNotEligible");
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentRecordProfileAppService>().GetAsync(student.Id)))
			.Code.ShouldBe("Students:Records:OnboardingIncomplete");
	});

	[Fact]
	public Task Legacy_override_requires_historical_enrollment_and_different_approver() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "LegacyDirectory", "Student");
		var service = GetRequiredService<IStudentDirectoryAppService>();
		await Should.ThrowAsync<BusinessException>(() => service.RequestLegacyOverrideAsync(student.Id, new() { Reason = "Verified historical school register" }));
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await _studentRepository.GetAsync(student.Id); current.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.UpdateAsync(current, autoSave: true);
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
				student.Id, student.AcademicYearId!.Value, student.StandardId, null), autoSave: true);
		});
		var request = await service.RequestLegacyOverrideAsync(student.Id, new() { Reason = "Verified historical school register" });
		(await Should.ThrowAsync<BusinessException>(() => service.ApproveLegacyOverrideAsync(request.Id, new() { ConcurrencyStamp = request.ConcurrencyStamp }))).Code.ShouldBe("Students:Records:SelfApprovalDenied");
		(await service.GetListAsync(new() { Search = "LegacyDirectory" })).TotalCount.ShouldBe(0);
		await WithUnitOfWorkAsync(async () =>
		{
			var item = await GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().GetAsync(request.Id);
			item.Decide(Guid.NewGuid(), true, "Checked original register", DateTime.UtcNow);
			await GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().UpdateAsync(item, autoSave: true);
		});
		(await service.GetListAsync(new() { Search = "LegacyDirectory" })).Items.Single().EntrySource.ShouldBe("LegacyOverride");
		(await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(student.Id)).EnrollmentHistory.Count.ShouldBe(1);
	});
}
