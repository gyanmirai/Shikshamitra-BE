using System;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Student_record_profile_respects_division_boundary_and_explicit_wider_grants(bool standardWide) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateUserScopeOfferingAsync();
		var assigned = await CreateExistingStudentAsync(offering, "Assigned", "Student");
		var other = await CreateExistingStudentAsync(offering, "Other", "Division");
		var unplaced = await CreateExistingStudentAsync(offering, "Unplaced", "Student");
		await CompleteScopeFixtureRecordAsync(assigned, offering);
		await CompleteScopeFixtureRecordAsync(other, offering);
		await CompleteScopeFixtureRecordAsync(unplaced, offering);
		var staffId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(staffId, $"profile-staff-{staffId:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<DivisionManager>();
			var divisions = GetRequiredService<IRepository<Division, Guid>>();
			var selected = await divisions.InsertAsync(await manager.CreateAsync(offering.StandardId, "Profile A", 30), autoSave: true);
			var denied = await divisions.InsertAsync(await manager.CreateAsync(offering.StandardId, "Profile B", 30), autoSave: true);
			foreach (var (studentId, divisionId) in new[] { (assigned.Id, selected.Id), (other.Id, denied.Id) })
			{
				var student = await _studentRepository.GetAsync(studentId);
				student.UpdateAcademicPlacement(assigned.AcademicYearId!.Value, offering.BoardId, offering.MediumId,
					offering.StageId, offering.StandardId, divisionId);
				await _studentRepository.UpdateAsync(student, autoSave: true);
			}
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: staffId,
				academicYearId: assigned.AcademicYearId, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
				educationMediumStageId: offering.StageId, standardId: offering.StandardId,
				divisionId: standardWide ? null : selected.Id), autoSave: true);
		});
		using var principal = _principalAccessor.Change(ProfilePrincipal(staffId));
		var service = GetRequiredService<IStudentRecordProfileAppService>();
		var profile = await service.GetAsync(assigned.Id);
		profile.DivisionName.ShouldBe("Profile A");
		foreach (var studentId in new[] { other.Id, unplaced.Id })
		{
			if (standardWide) (await service.GetAsync(studentId)).Id.ShouldBe(studentId);
			else (await Should.ThrowAsync<BusinessException>(() => service.GetAsync(studentId))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
	});

	private async Task CompleteScopeFixtureRecordAsync(Student student, AdmissionOffering offering)
	{
		var applicant = await CreateApplicantAsync($"Scope record {student.Id:N}");
		var application = await _applicationRepository.InsertAsync(new AdmissionApplication(Guid.NewGuid(), TestTenantId,
			$"PROFILE-{Guid.NewGuid():N}", applicant.Id, null, offering.AdmissionCycleId, offering.Id,
			offering.WorkflowDefinitionId!.Value, offering.ApplicationFormDefinitionId!.Value), true);
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await _studentRepository.GetAsync(student.Id);
			current.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.UpdateAsync(current, true);
			var enrollment = ShikshaMitra.Students.StudentEnrollments.StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
				student.Id, student.AcademicYearId!.Value, student.StandardId, student.DivisionId);
			await GetRequiredService<IRepository<ShikshaMitra.Students.StudentEnrollments.StudentEnrollment, Guid>>().InsertAsync(enrollment, true);
			var completed = new ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingCase(Guid.NewGuid(), TestTenantId,
				application.Id, applicant.Id, student.Id, offering.Id, student.AcademicYearId.Value, offering.BoardId,
				offering.MediumId, offering.StageId, offering.StandardId, $"profile-scope-{student.Id:N}", null, DateTime.UtcNow.AddDays(3));
			completed.CompleteHistoricalImport(enrollment.Id, student.DivisionId, student.RollNo, DateTime.UtcNow);
			await GetRequiredService<IRepository<ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingCase, Guid>>().InsertAsync(completed, true);
		});
	}
}
