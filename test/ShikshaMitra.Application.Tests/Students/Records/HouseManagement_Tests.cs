using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.Records.HouseManagement;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(IHouseManagementAppService Service, Student Student, Guid EnrollmentId, Guid DivisionId)> HouseFixtureAsync()
	{
		var fixture = await TransitionFixtureAsync();
		var division = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new Division(Guid.NewGuid(), TestTenantId, "HouseA", fixture.Student.StandardId!.Value, 100, ""), true));
		var enrollmentId = await WithUnitOfWorkAsync(async () =>
		{
			var enrollment = await _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id && x.IsActive);
			enrollment.ChangePlacement(fixture.Student.StandardId!.Value, division.Id, null);
			await _studentEnrollmentRepository.UpdateAsync(enrollment, true);
			return enrollment.Id;
		});
		return (GetRequiredService<IHouseManagementAppService>(), fixture.Student, enrollmentId, division.Id);
	}

	private static SaveHouseRequest NewHouse(Student student, string name) => new()
	{
		AcademicMediumId = student.AcademicMediumId,
		Name = name,
		Color = "#2468ab"
	};
	[Fact]
	public Task House_management_moves_clear_only_house_leadership_and_legacy_delete_is_blocked() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var first = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Falcons"));
		var next = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Eagles"));
		var assignment = new HouseAssignmentRequest
		{
			AcademicMediumId = f.Student.AcademicMediumId,
			HouseId = first.Id,
			EnrollmentIds = [f.EnrollmentId]
		};
		assignment.PreviewToken = (await f.Service.PreviewAssignmentAsync(assignment)).PreviewToken;
		await f.Service.CommitAssignmentAsync(assignment);
		var query = new HouseManagementQuery
		{
			AcademicMediumId = f.Student.AcademicMediumId,
			MaxResultCount = 100
		};
		first = (await f.Service.GetHousesAsync(query)).Items.Single(x => x.Id == first.Id);
		await f.Service.ReplaceHouseLeaderAsync(first.Id, new() { AcademicMediumId = query.AcademicMediumId, EnrollmentId = f.EnrollmentId, ConcurrencyStamp = first.ConcurrencyStamp });
		var division = (await f.Service.GetDivisionsAsync(query)).Items.Single(x => x.Id == f.DivisionId);
		await f.Service.ReplaceDivisionLeaderAsync(division.Id, new() { AcademicMediumId = query.AcademicMediumId, EnrollmentId = f.EnrollmentId, ConcurrencyStamp = division.ConcurrencyStamp });
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IHouseSettingsAppService>().DeleteHouseSettingsAsync(first.Id))).Code.ShouldBe(HouseManagementErrorCodes.HousePopulated);
		// A reviewed no-op keeps leadership intact.
		assignment.PreviewToken = (await f.Service.PreviewAssignmentAsync(assignment)).PreviewToken;
		await f.Service.CommitAssignmentAsync(assignment);
		(await f.Service.GetStudentsAsync(query)).Items.Single(x => x.EnrollmentId == f.EnrollmentId).IsHouseLeader.ShouldBeTrue();
		assignment.HouseId = next.Id;
		var preview = await f.Service.PreviewAssignmentAsync(assignment);
		preview.ClearedHouseLeaders.Single().EnrollmentId.ShouldBe(f.EnrollmentId);
		assignment.PreviewToken = preview.PreviewToken;
		await f.Service.CommitAssignmentAsync(assignment);
		var row = (await f.Service.GetStudentsAsync(query)).Items.Single(x => x.EnrollmentId == f.EnrollmentId);
		row.HouseId.ShouldBe(next.Id);
		row.IsHouseLeader.ShouldBeFalse();
		row.IsDivisionLeader.ShouldBeTrue();
		(await f.Service.GetHousesAsync(query)).Items.Single(x => x.Id == first.Id).Leader.ShouldBeNull();
	});
	[Fact]
	public Task House_management_rejects_stale_preview_and_mixed_invalid_batches_without_partial_writes() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var house = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Falcons"));
		var input = new HouseAssignmentRequest
		{
			AcademicMediumId = f.Student.AcademicMediumId,
			HouseId = house.Id,
			EnrollmentIds = [f.EnrollmentId]
		};
		input.PreviewToken = (await f.Service.PreviewAssignmentAsync(input)).PreviewToken;
		await f.Service.UpdateHouseAsync(house.Id, new() { AcademicMediumId = input.AcademicMediumId, Name = "Falcons revised", Color = house.Color, ConcurrencyStamp = house.ConcurrencyStamp });
		(await Should.ThrowAsync<BusinessException>(() => f.Service.CommitAssignmentAsync(input))).Code.ShouldBe(HouseManagementErrorCodes.StalePreview);
		input.EnrollmentIds.Add(Guid.NewGuid());
		await Should.ThrowAsync<BusinessException>(() => f.Service.PreviewAssignmentAsync(input));
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(f.EnrollmentId))).HouseId.ShouldBeNull();
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<Exception>(() => f.Service.GetStudentsAsync(new() { AcademicMediumId = input.AcademicMediumId }));
	});
	[Fact]
	public Task House_management_narrow_scope_masks_peers_and_cannot_replace_medium_leader_or_create_house() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var house = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Falcons"));
		var outsiderId = await WithUnitOfWorkAsync(async () =>
		{
			var otherDivision = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new Division(Guid.NewGuid(), TestTenantId, "HiddenB", f.Student.StandardId!.Value, 50, ""), true);
			var outsider = new Student(Guid.NewGuid(), TestTenantId, "Hidden", "", "Peer", f.Student.AcademicYearId!.Value, f.Student.AcademicBoardId!.Value, f.Student.AcademicMediumId, f.Student.AcademicMediumStageId!.Value, f.Student.StandardId, otherDivision.Id, status: StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.InsertAsync(outsider, true);
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, outsider.Id, f.Student.AcademicYearId.Value, f.Student.StandardId, otherDivision.Id);
			await _studentEnrollmentRepository.InsertAsync(enrollment, true);
			return enrollment.Id;
		});
		var hiddenAssignment = new HouseAssignmentRequest
		{
			AcademicMediumId = f.Student.AcademicMediumId,
			HouseId = house.Id,
			EnrollmentIds = [outsiderId]
		};
		hiddenAssignment.PreviewToken = (await f.Service.PreviewAssignmentAsync(hiddenAssignment)).PreviewToken;
		await f.Service.CommitAssignmentAsync(hiddenAssignment);
		house = (await f.Service.GetHousesAsync(new() { AcademicMediumId = f.Student.AcademicMediumId })).Items.Single(x => x.Id == house.Id);
		await f.Service.ReplaceHouseLeaderAsync(house.Id, new() { AcademicMediumId = f.Student.AcademicMediumId, EnrollmentId = outsiderId, ConcurrencyStamp = house.ConcurrencyStamp });
		var operatorId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(operatorId, "house-operator");
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: operatorId, academicYearId: f.Student.AcademicYearId, educationBoardId: f.Student.AcademicBoardId, educationMediumId: f.Student.AcademicMediumId, educationMediumStageId: f.Student.AcademicMediumStageId, standardId: f.Student.StandardId, divisionId: f.DivisionId), true));
		using (_principalAccessor.Change(ProfilePrincipal(operatorId)))
		{
			var page = await f.Service.GetHousesAsync(new() { AcademicMediumId = f.Student.AcademicMediumId });
			page.CanManage.ShouldBeFalse();
			var visibleHouse = page.Items.Single(x => x.Id == house.Id);
			visibleHouse.CanManage.ShouldBeFalse();
			visibleHouse.MemberCount.ShouldBe(0);
			visibleHouse.Leader.ShouldBeNull();
			var visibleStudents = await f.Service.GetStudentsAsync(new() { AcademicMediumId = f.Student.AcademicMediumId });
			visibleStudents.Items.ShouldNotContain(x => x.EnrollmentId == outsiderId);
			await Should.ThrowAsync<BusinessException>(() => f.Service.CommitAssignmentAsync(new() { AcademicMediumId = f.Student.AcademicMediumId, HouseId = house.Id, EnrollmentIds = [f.EnrollmentId, outsiderId], PreviewToken = hiddenAssignment.PreviewToken }));
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(f.EnrollmentId))).HouseId.ShouldBeNull();
			await Should.ThrowAsync<BusinessException>(() => f.Service.CreateHouseAsync(NewHouse(f.Student, "Not permitted")));
			await Should.ThrowAsync<BusinessException>(() => f.Service.ReplaceHouseLeaderAsync(house.Id, new() { AcademicMediumId = f.Student.AcademicMediumId, ConcurrencyStamp = house.ConcurrencyStamp }));
			var input = new HouseAssignmentRequest
			{
				AcademicMediumId = f.Student.AcademicMediumId,
				HouseId = house.Id,
				EnrollmentIds = [f.EnrollmentId]
			};
			input.PreviewToken = (await f.Service.PreviewAssignmentAsync(input)).PreviewToken;
			await WithUnitOfWorkAsync(async () =>
			{
				var e = await _studentEnrollmentRepository.GetAsync(f.EnrollmentId);
				e.AssignDivision(null);
				await _studentEnrollmentRepository.UpdateAsync(e, true);
			});
			await Should.ThrowAsync<BusinessException>(() => f.Service.CommitAssignmentAsync(input));
		}

		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(f.EnrollmentId))).HouseId.ShouldBeNull();
	});
	[Fact]
	public Task House_management_same_year_medium_move_cleans_source_roles_and_allows_new_membership() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var house = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Original Falcons"));
		var input = new HouseAssignmentRequest
		{
			AcademicMediumId = f.Student.AcademicMediumId,
			HouseId = house.Id,
			EnrollmentIds = [f.EnrollmentId]
		};
		input.PreviewToken = (await f.Service.PreviewAssignmentAsync(input)).PreviewToken;
		await f.Service.CommitAssignmentAsync(input);
		house = (await f.Service.GetHousesAsync(new() { AcademicMediumId = input.AcademicMediumId })).Items.Single(x => x.Id == house.Id);
		await f.Service.ReplaceHouseLeaderAsync(house.Id, new() { AcademicMediumId = input.AcademicMediumId, EnrollmentId = f.EnrollmentId, ConcurrencyStamp = house.ConcurrencyStamp });
		var sourceDivision = (await f.Service.GetDivisionsAsync(new() { AcademicMediumId = input.AcademicMediumId })).Items.Single(x => x.Id == f.DivisionId);
		await f.Service.ReplaceDivisionLeaderAsync(sourceDivision.Id, new() { AcademicMediumId = input.AcademicMediumId, EnrollmentId = f.EnrollmentId, ConcurrencyStamp = sourceDivision.ConcurrencyStamp });
		var target = await WithUnitOfWorkAsync(async () =>
		{
			var medium = await _mediumRepository.InsertAsync(new EducationMedium(Guid.NewGuid(), TestTenantId, "New medium", f.Student.AcademicBoardId!.Value), true);
			var stage = await _stageRepository.InsertAsync(new EducationMediumStage(Guid.NewGuid(), TestTenantId, "New stage", null, medium.Id), true);
			var standard = await _standardRepository.InsertAsync(new Standard(Guid.NewGuid(), TestTenantId, "New class", 10, stage.Id, null), true);
			var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new Division(Guid.NewGuid(), TestTenantId, "NewA", standard.Id, 50, ""), true);
			return (MediumId: medium.Id, StandardId: standard.Id, DivisionId: division.Id);
		});
		await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordPlacementService>().ApplyAsync(f.Student.Id, new() { AcademicYearId = f.Student.AcademicYearId!.Value, StandardId = target.StandardId, DivisionId = target.DivisionId }));
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(f.EnrollmentId))).HouseId.ShouldBeNull();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Division, Guid>>().GetAsync(f.DivisionId))).LeaderEnrollmentId.ShouldBeNull();
		house = (await f.Service.GetHousesAsync(new() { AcademicMediumId = input.AcademicMediumId })).Items.Single(x => x.Id == house.Id);
		house.Leader.ShouldBeNull();
		await f.Service.DeleteHouseAsync(house.Id, input.AcademicMediumId, house.ConcurrencyStamp);
		var newHouse = await f.Service.CreateHouseAsync(new() { AcademicMediumId = target.MediumId, Name = "New Eagles", Color = "#2468ab" });
		input.AcademicMediumId = target.MediumId;
		input.HouseId = newHouse.Id;
		input.PreviewToken = (await f.Service.PreviewAssignmentAsync(input)).PreviewToken;
		await f.Service.CommitAssignmentAsync(input);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(f.EnrollmentId))).HouseId.ShouldBe(newHouse.Id);
	});
}
