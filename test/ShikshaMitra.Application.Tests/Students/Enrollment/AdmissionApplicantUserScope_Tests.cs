using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Applicant_identity_notes_and_activity_direct_ids_require_academic_scope() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var applicant = await CreateApplicantAsync("Protected scope");
			await _applicationRepository.InsertAsync(new AdmissionApplication(Guid.NewGuid(), TestTenantId,
				$"SCOPE-{Guid.NewGuid():N}", applicant.Id, null, offering.AdmissionCycleId, offering.Id,
				offering.WorkflowDefinitionId!.Value, offering.ApplicationFormDefinitionId!.Value), true);
			var service = GetRequiredService<IAdmissionApplicantAppService>();
			var note = await service.CreateNoteAsync(applicant.Id, new() { Text = "Protected note" });
			var userId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(userId, $"applicant-scope-{userId:N}");
			using var principal = _principalAccessor.Change(ProfilePrincipal(userId));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(applicant.Id));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetActivitiesAsync(applicant.Id));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetNotesAsync(applicant.Id));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.CreateNoteAsync(applicant.Id, new() { Text = "Denied" }));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.UpdateNoteAsync(applicant.Id, note.Id, new() { Text = "Denied" }));
			await Should.ThrowAsync<EntityNotFoundException>(() => GetRequiredService<IAdmissionActivityAppService>().GetForApplicantAsync(applicant.Id));
		});

	[Fact]
	public Task Division_scope_checks_must_use_the_persisted_year_and_class() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var division = await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<ShikshaMitra.TenantSettings.Divisions.Division, Guid>>()
					.InsertAsync(await GetRequiredService<ShikshaMitra.TenantSettings.AcademicSetups.DivisionManager>().CreateAsync(offering.StandardId, "Scope tuple", 30), true);
				var service = GetRequiredService<StudentScopeAccessService>();
				var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
				await service.EnsureCanAccessAttendanceScopeAsync(cycle.AcademicYearId, offering.StandardId, division.Id, "test");
				(await Should.ThrowAsync<BusinessException>(() => service.EnsureCanAccessAttendanceScopeAsync(Guid.NewGuid(), offering.StandardId, division.Id, "test")))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				(await Should.ThrowAsync<BusinessException>(() => service.EnsureCanAccessAttendanceScopeAsync(cycle.AcademicYearId, Guid.NewGuid(), division.Id, "test")))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				(await Should.ThrowAsync<BusinessException>(() => service.EnsureCanAccessStudentsAsync([Guid.NewGuid()], "test")))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				var foreignStudent = new Student(Guid.NewGuid(), Guid.NewGuid(), "Foreign tenant", null, null,
					cycle.AcademicYearId, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId);
				(await Should.ThrowAsync<BusinessException>(() => service.EnsureCanAccessStudentAsync(foreignStudent, "test")))
					.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			});
		});

	[Fact]
	public Task Applicant_overview_filters_sibling_applications_and_rejects_shared_identity_edits() =>
		RunCertificationScenarioAsync(async () =>
		{
			var allowed = await CreateUserScopeOfferingAsync();
			var other = await WithUnitOfWorkAsync(async () =>
			{
				var standard = await _standardRepository.InsertAsync(await _standardManager.CreateAsync(allowed.StageId,
					$"Scope grade {Guid.NewGuid():N}"[..28], 11, null), true);
				var offering = new AdmissionOffering(Guid.NewGuid(), TestTenantId, allowed.AdmissionCycleId, null,
					allowed.BoardId, allowed.MediumId, allowed.StageId, standard.Id, "SCOPE_OTHER", true, null, null);
				offering.ConfigureReferences(allowed.WorkflowDefinitionId, allowed.InquiryFormDefinitionId, allowed.ApplicationFormDefinitionId);
				return await _offeringRepository.InsertAsync(offering, true);
			});
			var applicant = await CreateApplicantAsync("Shared scope");
			var applicationIds = new Guid[2];
			foreach (var (offering, index) in new[] { (allowed, 0), (other, 1) })
			{
				var application = await _applicationRepository.InsertAsync(new AdmissionApplication(Guid.NewGuid(), TestTenantId,
					$"SCOPE-{Guid.NewGuid():N}", applicant.Id, null, offering.AdmissionCycleId, offering.Id,
					offering.WorkflowDefinitionId!.Value, offering.ApplicationFormDefinitionId!.Value), true);
				applicationIds[index] = application.Id;
				await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionActivity, Guid>>().InsertAsync(
					new AdmissionActivity(Guid.NewGuid(), TestTenantId, "ScopeTest", $"Application {index}", applicant.Id, application.Id), true);
			}
			var userId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(userId, $"overview-scope-{userId:N}");
			var year = (await _cycleRepository.GetAsync(allowed.AdmissionCycleId)).AcademicYearId;
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: userId,
				academicYearId: year, educationBoardId: allowed.BoardId, educationMediumId: allowed.MediumId,
				educationMediumStageId: allowed.StageId, standardId: allowed.StandardId), true);
			using var principal = _principalAccessor.Change(ProfilePrincipal(userId));
			var service = GetRequiredService<IAdmissionApplicantAppService>();
			var overview = await service.GetAsync(applicant.Id);
			overview.Applications.Select(item => item.Id).ShouldBe([applicationIds[0]]);
			overview.Activities.Select(item => item.ApplicationId).ShouldBe([applicationIds[0]]);
			await Should.ThrowAsync<EntityNotFoundException>(() => service.UpdateProfileAsync(applicant.Id,
				new() { FirstName = "Denied", DateOfBirth = new DateTime(2015, 1, 1), Gender = "Male" }));
			await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(() => GetRequiredService<AdmissionOfferingReadService>().GetAsync(other.Id)));
			await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(() => GetRequiredService<AdmissionOfferingCommandService>().DeactivateAsync(other.Id, other.ConcurrencyStamp)));
			(await _applicantRepository.GetAsync(applicant.Id)).FirstName.ShouldBe(applicant.FirstName);
		});

	[Theory]
	[InlineData("division")]
	[InlineData("class")]
	[InlineData("medium")]
	[InlineData("year")]
	[InlineData("tenant")]
	public Task Academic_ancestors_allow_navigation_but_shared_writes_require_explicit_wider_scope(string level) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateUserScopeOfferingAsync();
			var userId = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(userId, $"placement-scope-{userId:N}");
			await WithUnitOfWorkAsync(async () =>
			{
				var divisions = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<ShikshaMitra.TenantSettings.Divisions.Division, Guid>>();
				var manager = GetRequiredService<ShikshaMitra.TenantSettings.AcademicSetups.DivisionManager>();
				var assigned = await divisions.InsertAsync(await manager.CreateAsync(offering.StandardId, "Scope A", 30), true);
				var sibling = await divisions.InsertAsync(await manager.CreateAsync(offering.StandardId, "Scope B", 30), true);
				var year = (await _cycleRepository.GetAsync(offering.AdmissionCycleId)).AcademicYearId;
				var narrow = level is "division" or "class";
				await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: userId,
					academicYearId: level == "tenant" ? null : year,
					educationBoardId: level is "tenant" or "year" ? null : offering.BoardId,
					educationMediumId: level is "tenant" or "year" ? null : offering.MediumId,
					educationMediumStageId: narrow ? offering.StageId : null,
					standardId: narrow ? offering.StandardId : null,
					divisionId: level == "division" ? assigned.Id : null), true);
				using var principal = _principalAccessor.Change(ProfilePrincipal(userId));
				var service = GetRequiredService<StudentScopeAccessService>();
				await service.EnsureCanAccessMediumAsync(offering.MediumId, "test");
				await service.EnsureCanAccessStandardAsync(offering.StandardId, "test");
				await service.EnsureCanAccessPlacementAsync(year, offering.StandardId, assigned.Id, "test");
				var scopedDivisions = await service.ApplyCurrentUserDivisionScopeAsync(await divisions.GetQueryableAsync(), year);
				(await GetRequiredService<Volo.Abp.Linq.IAsyncQueryableExecuter>().AnyAsync(scopedDivisions.Where(item => item.Id == sibling.Id)))
					.ShouldBe(level != "division");
				if (level == "division")
				{
					await Should.ThrowAsync<BusinessException>(() => service.EnsureCanAccessPlacementAsync(year, offering.StandardId, sibling.Id, "test"));
					await Should.ThrowAsync<BusinessException>(() => service.EnsureCanAccessPlacementAsync(year, offering.StandardId, null, "test"));
				}
				else await service.EnsureCanAccessPlacementAsync(year, offering.StandardId, null, "test");
				if (narrow) await Should.ThrowAsync<BusinessException>(() => service.EnsureCanManageMediumAsync(offering.MediumId, "test"));
				else await service.EnsureCanManageMediumAsync(offering.MediumId, "test");
				await service.EnsureSettingsScopeAsync(year, false, "test");
				if (level is "year" or "tenant") await service.EnsureSettingsScopeAsync(year, true, "test");
				else await Should.ThrowAsync<BusinessException>(() => service.EnsureSettingsScopeAsync(year, true, "test"));
				if (level == "tenant") await service.EnsureSettingsScopeAsync(null, true, "test");
				else await Should.ThrowAsync<BusinessException>(() => service.EnsureSettingsScopeAsync(null, true, "test"));
				(await GetRequiredService<ShikshaMitra.Students.StudentRecords.IStudentRecordsSettingsAppService>().GetRoleNamesAsync()).ShouldNotBeNull();
			});
		});

	private async Task<AdmissionOffering> CreateUserScopeOfferingAsync()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), TestTenantId, "Scope fixture", $"SCOPE-{Guid.NewGuid():N}");
		await GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow, true);
		return await CreateAvailableOfferingAsync(workflow.Id, assertValid: false);
	}
}
