using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.Records.HouseManagement;
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
	public Task House_management_search_matches_name_or_GR_number_case_insensitively() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(f.Student.Id);
			student.AssignGrNumber("GR-HOUSE-781", reason: "House search regression");
			await _studentRepository.UpdateAsync(student, true);
		});
		var input = new HouseManagementQuery { AcademicMediumId = f.Student.AcademicMediumId, Filter = " gr-house-781 " };
		(await f.Service.GetStudentsAsync(input)).Items.ShouldContain(x => x.EnrollmentId == f.EnrollmentId);
		input.Filter = f.Student.FirstName;
		(await f.Service.GetStudentsAsync(input)).Items.ShouldContain(x => x.EnrollmentId == f.EnrollmentId);
		input.Filter = "nonexistent-student-identifier";
		(await f.Service.GetStudentsAsync(input)).TotalCount.ShouldBe(0);
	});

	[Fact]
	public Task House_management_has_one_house_leader_per_division_and_scopes_division_filters() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var mediumId = f.Student.AcademicMediumId;
		var first = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Falcons"));
		var second = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Eagles"));
		var (peerId, separateEnrollmentId, separateDivisionId) = await WithUnitOfWorkAsync(async () =>
		{
			var anotherDivision = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
				new Division(Guid.NewGuid(), TestTenantId, "HouseB", f.Student.StandardId!.Value, 50, ""), true);
			async Task<Guid> InsertMemberAsync(string name, Guid divisionId)
			{
				var student = new Student(Guid.NewGuid(), TestTenantId, name, "", "Test", f.Student.AcademicYearId!.Value,
					f.Student.AcademicBoardId!.Value, mediumId, f.Student.AcademicMediumStageId!.Value, f.Student.StandardId,
					divisionId, status: StudentLifeCycleStatusEnum.Enrolled);
				await _studentRepository.InsertAsync(student, true);
				var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, student.Id, f.Student.AcademicYearId.Value, f.Student.StandardId, divisionId);
				await _studentEnrollmentRepository.InsertAsync(enrollment, true);
				return enrollment.Id;
			}
			return (await InsertMemberAsync("Peer", f.DivisionId), await InsertMemberAsync("Another", anotherDivision.Id), anotherDivision.Id);
		});
		async Task<HouseManagementDivisionDto> ReadDivisionAsync(Guid divisionId) =>
			(await f.Service.GetDivisionsAsync(new() { AcademicMediumId = mediumId, DivisionId = divisionId })).Items.Single();
		async Task AssignAsync(Guid divisionId, Guid houseId, params Guid[] enrollmentIds)
		{
			var request = new HouseAssignmentRequest { AcademicMediumId = mediumId, DivisionId = divisionId, HouseId = houseId, EnrollmentIds = enrollmentIds.ToList() };
			request.PreviewToken = (await f.Service.PreviewAssignmentAsync(request)).PreviewToken;
			await f.Service.CommitAssignmentAsync(request);
		}

		var appointment = new ReplaceStudentLeaderRequest
		{
			AcademicMediumId = mediumId,
			EnrollmentId = f.EnrollmentId,
			ConcurrencyStamp = (await ReadDivisionAsync(f.DivisionId)).ConcurrencyStamp
		};
		(await Should.ThrowAsync<BusinessException>(() => f.Service.ReplaceDivisionHouseLeaderAsync(f.DivisionId, appointment))).Code.ShouldBe(HouseManagementErrorCodes.IneligibleLeader);
		await f.Service.ReplaceDivisionLeaderAsync(f.DivisionId, appointment);
		var mixed = new HouseAssignmentRequest
		{
			AcademicMediumId = mediumId, DivisionId = f.DivisionId, HouseId = first.Id,
			EnrollmentIds = [f.EnrollmentId, separateEnrollmentId]
		};
		(await Should.ThrowAsync<BusinessException>(() => f.Service.PreviewAssignmentAsync(mixed))).Code.ShouldBe(HouseManagementErrorCodes.AssignmentDivisionMismatch);
		await AssignAsync(f.DivisionId, first.Id, f.EnrollmentId);
		await AssignAsync(separateDivisionId, first.Id, separateEnrollmentId);
		await AssignAsync(f.DivisionId, second.Id, peerId);
		mixed.HouseId = null;
		mixed.EnrollmentIds = [f.EnrollmentId];
		mixed.PreviewToken = (await f.Service.PreviewAssignmentAsync(mixed)).PreviewToken;
		mixed.DivisionId = separateDivisionId;
		(await Should.ThrowAsync<BusinessException>(() => f.Service.CommitAssignmentAsync(mixed))).Code.ShouldBe(HouseManagementErrorCodes.AssignmentDivisionMismatch);
		mixed.EnrollmentIds.Add(separateEnrollmentId);
		(await Should.ThrowAsync<BusinessException>(() => f.Service.PreviewAssignmentAsync(mixed))).Code.ShouldBe(HouseManagementErrorCodes.AssignmentDivisionMismatch);
		(await Should.ThrowAsync<BusinessException>(() => f.Service.CommitAssignmentAsync(mixed))).Code.ShouldBe(HouseManagementErrorCodes.AssignmentDivisionMismatch);
		var eligible = await f.Service.GetStudentsAsync(new() { AcademicMediumId = mediumId, DivisionId = f.DivisionId, AssignedOnly = true });
		eligible.Items.Select(x => x.EnrollmentId).ShouldBe([f.EnrollmentId, peerId], ignoreOrder: true);
		appointment.ConcurrencyStamp = (await ReadDivisionAsync(f.DivisionId)).ConcurrencyStamp;
		await f.Service.ReplaceDivisionHouseLeaderAsync(f.DivisionId, appointment);
		appointment.EnrollmentId = separateEnrollmentId;
		appointment.ConcurrencyStamp = (await ReadDivisionAsync(separateDivisionId)).ConcurrencyStamp;
		await f.Service.ReplaceDivisionHouseLeaderAsync(separateDivisionId, appointment);
		(await f.Service.GetHousesAsync(new() { AcademicMediumId = mediumId })).Items.Single(x => x.Id == first.Id).HouseLeaderCount.ShouldBe(2);

		appointment.EnrollmentId = peerId;
		appointment.ConcurrencyStamp = (await ReadDivisionAsync(f.DivisionId)).ConcurrencyStamp;
		await f.Service.ReplaceDivisionHouseLeaderAsync(f.DivisionId, appointment);
		var filtered = await f.Service.GetDivisionsAsync(new() { AcademicMediumId = mediumId, DivisionId = f.DivisionId, HouseId = first.Id });
		var occupied = filtered.Items.Single();
		occupied.HouseLeader!.EnrollmentId.ShouldBe(peerId);
		occupied.HouseLeader.HouseId.ShouldBe(second.Id);
		occupied.Leader!.EnrollmentId.ShouldBe(f.EnrollmentId);
		(await f.Service.GetDivisionsAsync(new() { AcademicMediumId = mediumId, DivisionId = separateDivisionId, HouseId = second.Id })).Items.ShouldBeEmpty();
		(await f.Service.GetHousesAsync(new() { AcademicMediumId = mediumId })).Items.Single(x => x.Id == first.Id).HouseLeaderCount.ShouldBe(1);
	});

	[Fact]
	public Task House_management_deactivation_clears_both_roles_on_the_source_division() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var house = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Falcons"));
		var assignment = new HouseAssignmentRequest { AcademicMediumId = f.Student.AcademicMediumId, DivisionId = f.DivisionId, HouseId = house.Id, EnrollmentIds = [f.EnrollmentId] };
		assignment.PreviewToken = (await f.Service.PreviewAssignmentAsync(assignment)).PreviewToken;
		await f.Service.CommitAssignmentAsync(assignment);
		var divisionQuery = new HouseManagementQuery { AcademicMediumId = f.Student.AcademicMediumId, DivisionId = f.DivisionId };
		var row = (await f.Service.GetDivisionsAsync(divisionQuery)).Items.Single();
		var appointment = new ReplaceStudentLeaderRequest { AcademicMediumId = f.Student.AcademicMediumId, EnrollmentId = f.EnrollmentId, ConcurrencyStamp = row.ConcurrencyStamp };
		await f.Service.ReplaceDivisionHouseLeaderAsync(f.DivisionId, appointment);
		appointment.ConcurrencyStamp = (await f.Service.GetDivisionsAsync(divisionQuery)).Items.Single().ConcurrencyStamp;
		await f.Service.ReplaceDivisionLeaderAsync(f.DivisionId, appointment);

		await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordPlacementService>()
			.ApplyStatusAsync(f.Student.Id, new() { Status = StudentLifeCycleStatusEnum.InActive }));
		var division = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Division, Guid>>().GetAsync(f.DivisionId));
		division.HouseLeaderEnrollmentId.ShouldBeNull();
		division.LeaderEnrollmentId.ShouldBeNull();
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(f.EnrollmentId))).IsActive.ShouldBeFalse();
	});

	[Fact]
	public Task House_management_moves_clear_only_house_leadership_and_legacy_delete_is_blocked() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var first = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Falcons"));
		var next = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Eagles"));
		var assignment = new HouseAssignmentRequest
		{
			AcademicMediumId = f.Student.AcademicMediumId,
			DivisionId = f.DivisionId,
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
		var division = (await f.Service.GetDivisionsAsync(query)).Items.Single(x => x.Id == f.DivisionId);
		assignment.HouseId = next.Id;
		assignment.PreviewToken = (await f.Service.PreviewAssignmentAsync(assignment)).PreviewToken;
		await f.Service.ReplaceDivisionHouseLeaderAsync(division.Id, new() { AcademicMediumId = query.AcademicMediumId, EnrollmentId = f.EnrollmentId, ConcurrencyStamp = division.ConcurrencyStamp });
		(await Should.ThrowAsync<BusinessException>(() => f.Service.CommitAssignmentAsync(assignment))).Code.ShouldBe(HouseManagementErrorCodes.StalePreview);
		division = (await f.Service.GetDivisionsAsync(query)).Items.Single(x => x.Id == f.DivisionId);
		await f.Service.ReplaceDivisionLeaderAsync(division.Id, new() { AcademicMediumId = query.AcademicMediumId, EnrollmentId = f.EnrollmentId, ConcurrencyStamp = division.ConcurrencyStamp });
		var populatedHouse = (await f.Service.GetHousesAsync(query)).Items.Single(x => x.Id == first.Id);
		(await Should.ThrowAsync<BusinessException>(() => f.Service.DeleteHouseAsync(populatedHouse.Id, query.AcademicMediumId, populatedHouse.ConcurrencyStamp))).Code.ShouldBe(HouseManagementErrorCodes.HousePopulated);
		// A reviewed no-op keeps leadership intact.
		assignment.HouseId = first.Id;
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
		(await f.Service.GetHousesAsync(query)).Items.Single(x => x.Id == first.Id).HouseLeaderCount.ShouldBe(0);
	});
	[Fact]
	public Task House_management_rejects_stale_preview_and_mixed_invalid_batches_without_partial_writes() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var house = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Falcons"));
		var input = new HouseAssignmentRequest
		{
			AcademicMediumId = f.Student.AcademicMediumId,
			DivisionId = f.DivisionId,
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
	public Task House_management_narrow_scope_masks_peers_and_cannot_replace_outside_division_or_create_house() => RunCertificationScenarioAsync(async () =>
	{
		var f = await HouseFixtureAsync();
		var house = await f.Service.CreateHouseAsync(NewHouse(f.Student, "Falcons"));
		var outsider = await WithUnitOfWorkAsync(async () =>
		{
			var otherDivision = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(new Division(Guid.NewGuid(), TestTenantId, "HiddenB", f.Student.StandardId!.Value, 50, ""), true);
			var outsiderStudent = new Student(Guid.NewGuid(), TestTenantId, "Hidden", "", "Peer", f.Student.AcademicYearId!.Value, f.Student.AcademicBoardId!.Value, f.Student.AcademicMediumId, f.Student.AcademicMediumStageId!.Value, f.Student.StandardId, otherDivision.Id, status: StudentLifeCycleStatusEnum.Enrolled);
			await _studentRepository.InsertAsync(outsiderStudent, true);
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, outsiderStudent.Id, f.Student.AcademicYearId.Value, f.Student.StandardId, otherDivision.Id);
			await _studentEnrollmentRepository.InsertAsync(enrollment, true);
			return (EnrollmentId: enrollment.Id, DivisionId: otherDivision.Id);
		});
		var hiddenAssignment = new HouseAssignmentRequest
		{
			AcademicMediumId = f.Student.AcademicMediumId,
			DivisionId = outsider.DivisionId,
			HouseId = house.Id,
			EnrollmentIds = [outsider.EnrollmentId]
		};
		hiddenAssignment.PreviewToken = (await f.Service.PreviewAssignmentAsync(hiddenAssignment)).PreviewToken;
		await f.Service.CommitAssignmentAsync(hiddenAssignment);
		var hiddenDivision = (await f.Service.GetDivisionsAsync(new() { AcademicMediumId = f.Student.AcademicMediumId, DivisionId = outsider.DivisionId })).Items.Single();
		await f.Service.ReplaceDivisionHouseLeaderAsync(hiddenDivision.Id, new() { AcademicMediumId = f.Student.AcademicMediumId, EnrollmentId = outsider.EnrollmentId, ConcurrencyStamp = hiddenDivision.ConcurrencyStamp });
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
			visibleHouse.HouseLeaderCount.ShouldBe(0);
			var visibleStudents = await f.Service.GetStudentsAsync(new() { AcademicMediumId = f.Student.AcademicMediumId });
			visibleStudents.Items.ShouldNotContain(x => x.EnrollmentId == outsider.EnrollmentId);
			await Should.ThrowAsync<BusinessException>(() => f.Service.CommitAssignmentAsync(new() { AcademicMediumId = f.Student.AcademicMediumId, DivisionId = f.DivisionId, HouseId = house.Id, EnrollmentIds = [f.EnrollmentId, outsider.EnrollmentId], PreviewToken = hiddenAssignment.PreviewToken }));
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(f.EnrollmentId))).HouseId.ShouldBeNull();
			await Should.ThrowAsync<BusinessException>(() => f.Service.CreateHouseAsync(NewHouse(f.Student, "Not permitted")));
			await Should.ThrowAsync<BusinessException>(() => f.Service.ReplaceDivisionHouseLeaderAsync(hiddenDivision.Id, new() { AcademicMediumId = f.Student.AcademicMediumId, ConcurrencyStamp = hiddenDivision.ConcurrencyStamp }));
			var input = new HouseAssignmentRequest
			{
				AcademicMediumId = f.Student.AcademicMediumId,
				DivisionId = f.DivisionId,
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
			DivisionId = f.DivisionId,
			HouseId = house.Id,
			EnrollmentIds = [f.EnrollmentId]
		};
		input.PreviewToken = (await f.Service.PreviewAssignmentAsync(input)).PreviewToken;
		await f.Service.CommitAssignmentAsync(input);
		var sourceDivision = (await f.Service.GetDivisionsAsync(new() { AcademicMediumId = input.AcademicMediumId })).Items.Single(x => x.Id == f.DivisionId);
		await f.Service.ReplaceDivisionHouseLeaderAsync(sourceDivision.Id, new() { AcademicMediumId = input.AcademicMediumId, EnrollmentId = f.EnrollmentId, ConcurrencyStamp = sourceDivision.ConcurrencyStamp });
		sourceDivision = (await f.Service.GetDivisionsAsync(new() { AcademicMediumId = input.AcademicMediumId, DivisionId = f.DivisionId })).Items.Single();
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
		var departedDivision = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Division, Guid>>().GetAsync(f.DivisionId));
		departedDivision.HouseLeaderEnrollmentId.ShouldBeNull();
		departedDivision.LeaderEnrollmentId.ShouldBeNull();
		house = (await f.Service.GetHousesAsync(new() { AcademicMediumId = input.AcademicMediumId })).Items.Single(x => x.Id == house.Id);
		house.HouseLeaderCount.ShouldBe(0);
		await f.Service.DeleteHouseAsync(house.Id, input.AcademicMediumId, house.ConcurrencyStamp);
		var newHouse = await f.Service.CreateHouseAsync(new() { AcademicMediumId = target.MediumId, Name = "New Eagles", Color = "#2468ab" });
		input.AcademicMediumId = target.MediumId;
		input.DivisionId = target.DivisionId;
		input.HouseId = newHouse.Id;
		input.PreviewToken = (await f.Service.PreviewAssignmentAsync(input)).PreviewToken;
		await f.Service.CommitAssignmentAsync(input);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(f.EnrollmentId))).HouseId.ShouldBe(newHouse.Id);
	});
}
