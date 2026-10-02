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
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var assigned = await CreateExistingStudentAsync(offering, "Assigned", "Student");
		var other = await CreateExistingStudentAsync(offering, "Other", "Division");
		var unplaced = await CreateExistingStudentAsync(offering, "Unplaced", "Student");
		await GrantCompletedRecordAsync(assigned); await GrantCompletedRecordAsync(other); await GrantCompletedRecordAsync(unplaced);
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
}
