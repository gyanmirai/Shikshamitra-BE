using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.Enrollments.Registrations;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Enrolled_profile_reads_and_updates_details_without_changing_placement() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "Profile", "Reader");
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await _studentRepository.GetAsync(student.Id);
			current.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.UpdateAsync(current, autoSave: true);
		});
		var general = GetRequiredService<IStudentGeneralInfoAppService>();
		(await general.GetRegistrationGeneralDetailsByIdAsync(student.Id)).FirstName.ShouldBe("Profile");
		var update = new UpdateGeneralInfoDto { FormNumber = "X", FirstName = "Changed", LastName = "Reader",
			PrimaryContactNo = "9876543210", FamilyCast = "", City = "Surat",
			State = new State("Gujarat", "GJ"), Country = new Country("India", "IN") };
		(await general.UpdateGeneralInfoAsync(student.Id, update)).FirstName.ShouldBe("Changed");
		(await general.GetRegistrationGeneralDetailsByIdAsync(student.Id)).FirstName.ShouldBe("Changed");
		update.StandardId = Guid.NewGuid();
		(await Should.ThrowAsync<BusinessException>(() => general.UpdateGeneralInfoAsync(student.Id, update)))
			.Code.ShouldBe(StudentRegistrationErrorCodes.InvalidStudentStatusForGeneralInfo);
	});

	[Fact]
	public Task Student_record_profile_reads_current_canonical_identity_and_placement_without_mutation() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "Original", "Student");
		var applicant = await CreateCertificationApplicantAsync("Stale", true, true, student.Id);
		var birth = new DateTimeOffset(2019, 6, 15, 0, 0, 0, TimeSpan.Zero);
		var stamp = await WithUnitOfWorkAsync(async () =>
		{
			var current = (await _studentRepository.GetRegistrationAggregateAsync(student.Id))!;
			current.UpdateGeneralInformation("Current", "K", "Patel");
			current.AssignGrNumber("GR-2026-0098", 98);
			current.AssignRollNo(17);
			var details = current.AdditionalDetail!;
			details.UpdateDetails(birth, GenderEnum.Female, details.DateOfInquiry, details.InquiryPreferenceId, details.PreviousSchool);
			await _studentRepository.UpdateAsync(current, autoSave: true);
			return current.ConcurrencyStamp;
		});
		var result = await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(student.Id);
		result.Id.ShouldBe(student.Id);
		result.StudentId.ShouldBe(student.Id);
		result.StudentId.ShouldNotBe(applicant.Id);
		result.FirstName.ShouldBe("Current"); result.MiddleName.ShouldBe("K"); result.LastName.ShouldBe("Patel");
		result.DateOfBirth.ShouldBe(birth); result.Gender.ShouldBe(GenderEnum.Female);
		result.GrNo.ShouldBe(98); result.GrNumber.ShouldBe("GR-2026-0098"); result.RollNo.ShouldBe(17);
		result.StandardId.ShouldBe(offering.StandardId);
		result.StandardName.ShouldBe(await WithUnitOfWorkAsync(async () => (await _standardRepository.GetAsync(offering.StandardId)).Name));
		result.AcademicMediumId.ShouldBe(offering.MediumId);
		result.AcademicMediumName.ShouldBe(await WithUnitOfWorkAsync(async () => (await _mediumRepository.GetAsync(offering.MediumId)).Name));
		result.Status.ShouldBe(student.Status); result.DivisionId.ShouldBeNull(); result.DivisionName.ShouldBeNull();
		(await WithUnitOfWorkAsync(async () => (await _studentRepository.GetAsync(student.Id)).ConcurrencyStamp)).ShouldBe(stamp);
	});

	[Fact]
	public Task Student_record_profile_denies_other_standard_in_same_board_for_narrow_staff() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var allowed = await CreateExistingStudentAsync(offering, "Allowed", "Student");
		var denied = await CreateExistingStudentAsync(offering, "Other", "Student");
		var staffId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(staffId, $"profile-staff-{staffId:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			var other = await _standardManager.CreateAsync(offering.StageId, $"Profile {Guid.NewGuid():N}"[..24], 11, null);
			await _standardRepository.InsertAsync(other, autoSave: true);
			var student = await _studentRepository.GetAsync(denied.Id);
			student.UpdateAcademicPlacement(allowed.AcademicYearId!.Value, offering.BoardId, offering.MediumId, offering.StageId, other.Id, null);
			await _studentRepository.UpdateAsync(student, autoSave: true);
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(staffId,
				$"profile-staff-{staffId:N}", $"profile-staff-{staffId:N}@example.test", TestTenantId), autoSave: true);
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: staffId,
				academicYearId: allowed.AcademicYearId, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
				educationMediumStageId: offering.StageId, standardId: offering.StandardId), autoSave: true);
		});
		using var principal = _principalAccessor.Change(ProfilePrincipal(staffId));
		var service = GetRequiredService<IStudentRecordProfileAppService>();
		(await service.GetAsync(allowed.Id)).Id.ShouldBe(allowed.Id);
		(await Should.ThrowAsync<BusinessException>(() => service.GetAsync(denied.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		var evidence = GetRequiredService<IStudentAdmissionEvidenceAppService>();
		(await evidence.GetListAsync(allowed.Id)).ShouldBeEmpty();
		(await Should.ThrowAsync<BusinessException>(() => evidence.GetListAsync(denied.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		(await Should.ThrowAsync<BusinessException>(() => evidence.DownloadAsync(denied.Id, Guid.NewGuid()))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	});

	[Fact]
	public Task Student_record_profile_denies_missing_foreign_tenant_and_absent_scope() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "Scoped", "Student");
		var service = GetRequiredService<IStudentRecordProfileAppService>();
		await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(Guid.NewGuid()));
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(student.Id));
		using (_principalAccessor.Change(ProfilePrincipal(Guid.NewGuid())))
			(await Should.ThrowAsync<BusinessException>(() => service.GetAsync(student.Id))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	});

	[Fact]
	public Task Student_record_profile_supports_unplaced_records_with_missing_optional_details() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await WithUnitOfWorkAsync(async () =>
		{
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			return await _studentRepository.InsertAsync(new Student(Guid.NewGuid(), TestTenantId, "Unplaced", null, null,
				cycle.AcademicYearId, offering.BoardId, offering.MediumId, offering.StageId), autoSave: true);
		});
		var result = await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(student.Id);
		result.Id.ShouldBe(student.Id); result.FirstName.ShouldBe("Unplaced");
		result.StandardId.ShouldBeNull(); result.StandardName.ShouldBeNull(); result.DivisionId.ShouldBeNull();
		result.DateOfBirth.ShouldBeNull(); result.Gender.ShouldBeNull(); result.GrNumber.ShouldBeNull();
	});

	[Fact]
	public Task Student_record_profile_guid_loads_accepted_admission_evidence_after_finalization() => RunCertificationScenarioAsync(async () =>
	{
		var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate" };
		var fixture = await CreateDocumentHandoffFixtureAsync(HandoffPolicy(requirement));
		var evidence = await AddHandoffEvidenceAsync(fixture, requirement);
		var ready = await CompleteDocumentHandoffApplicationAsync(fixture);
		var finalized = await FinalizeAsync(ready, true, "profile-evidence-read");
		var profile = await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(finalized.StudentId);
		var linked = await GetRequiredService<IStudentAdmissionEvidenceAppService>().GetListAsync(profile.Id);
		profile.StudentId.ShouldBe(finalized.StudentId);
		linked.Single().Id.ShouldBe(evidence.Id);
		linked.Single().Status.ShouldBe("accepted");
	});

	private static ClaimsPrincipal ProfilePrincipal(Guid userId) => new(new ClaimsIdentity(
	[
		new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.UserName, "profile-test-staff")
	], "Test"));
}
