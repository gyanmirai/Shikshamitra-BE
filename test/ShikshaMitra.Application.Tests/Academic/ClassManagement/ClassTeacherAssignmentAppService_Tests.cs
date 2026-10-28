using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public partial class ClassTeacherAssignmentAppService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");

	private readonly IClassTeacherAssignmentAppService _service;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<IdentityRole, Guid> _roleRepository;
	private readonly IRepository<ClassTeacher.ClassTeacher, Guid> _classTeacherRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<Division, Guid> _divisionRepository;
	private readonly IRepository<EducationMediumStage, Guid> _stageRepository;
	private readonly ICurrentTenant _currentTenant;

	public ClassTeacherAssignmentAppService_Tests()
	{
		_service = GetRequiredService<IClassTeacherAssignmentAppService>();
		_employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
		_roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
		_classTeacherRepository = GetRequiredService<IRepository<ClassTeacher.ClassTeacher, Guid>>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_divisionRepository = GetRequiredService<IRepository<Division, Guid>>();
		_stageRepository = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Save_Draft_Should_Remain_Pending_Until_Confirmed()
	{
		using (_currentTenant.Change(TestTenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await _stageRepository.GetAsync(stageId);
				var standard = await _standardRepository.InsertAsync(
					new Standard(Guid.NewGuid(), TestTenantId, "Draft class", 8, stage.Id, shiftId: null),
					autoSave: true);
				var division = await _divisionRepository.InsertAsync(
					new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "D-101"),
					autoSave: true);
				var teacher = await CreateEmployeeAsync(
					"Draft Teacher",
					EmployeeStatusType.Active,
					EmployeeGroupType.Teaching);
				var input = new SaveClassTeacherAssignmentsDto
				{
					OrganizationId = stage.Id,
					SaveAsDraft = true,
					Assignments =
					[
						new ClassTeacherAssignmentUpdateDto
						{
							ClassId = standard.Id,
							DivisionId = division.Id,
							TeacherId = teacher.Id
						}
					]
				};

				await _service.SaveClassTeacherAssignmentsAsync(input);

				var draft = await _classTeacherRepository.SingleAsync(assignment =>
					assignment.StandardId == standard.Id && assignment.DivisionId == division.Id);
				draft.Status.ShouldBe(ClassTeacherAssignmentStatus.Draft);
				var draftRows = await _service.GetClassTeacherAssignmentsAsync(stage.Id);
				var draftRow = draftRows.Single(row => row.ClassId == standard.Id && row.DivisionId == division.Id);
				draftRow.AssignmentStatus.ShouldBe("Changed");
				draftRow.ApprovalStatus.ShouldBe("Pending");
				draftRow.Issues.ShouldContain(issue => issue.Code == "DRAFT_ASSIGNMENT");
				var draftSummary = await _service.GetClassTeacherAssignmentSummaryAsync(stage.Id);
				draftSummary.PendingChanges.ShouldBe(1);
				draftSummary.Conflicts.ShouldBe(1);

				input.SaveAsDraft = false;
				await _service.SaveClassTeacherAssignmentsAsync(input);

				var active = await _classTeacherRepository.SingleAsync(assignment =>
					assignment.StandardId == standard.Id && assignment.DivisionId == division.Id);
				active.Status.ShouldBe(ClassTeacherAssignmentStatus.Active);
				var activeRows = await _service.GetClassTeacherAssignmentsAsync(stage.Id);
				var activeRow = activeRows.Single(row => row.ClassId == standard.Id && row.DivisionId == division.Id);
				activeRow.AssignmentStatus.ShouldBe("Active");
				activeRow.ApprovalStatus.ShouldBe("Approved");
				var activeSummary = await _service.GetClassTeacherAssignmentSummaryAsync(stage.Id);
				activeSummary.PendingChanges.ShouldBe(0);
				activeSummary.Conflicts.ShouldBe(0);
			});
		}
	}

	[Fact]
	public async Task Eligible_Teachers_Should_Include_Only_Active_Teaching_Staff()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var activeTeacher = await CreateEmployeeAsync(
				"Active",
				EmployeeStatusType.Active,
				EmployeeGroupType.Teaching);
			var inactiveTeacher = await CreateEmployeeAsync(
				"Inactive",
				EmployeeStatusType.Inactive,
				EmployeeGroupType.Teaching);
			var administrator = await CreateEmployeeAsync(
				"Administrator",
				EmployeeStatusType.Active,
				EmployeeGroupType.NonTeaching);

			var candidates = await _service.GetEligibleTeachersAsync(Guid.NewGuid());

			candidates.ShouldContain(candidate => candidate.TeacherId == activeTeacher.Id);
			candidates.ShouldNotContain(candidate => candidate.TeacherId == inactiveTeacher.Id);
			candidates.ShouldNotContain(candidate => candidate.TeacherId == administrator.Id);
		});
	}

	[Fact]
	public async Task Validation_And_Save_Should_Reject_NonTeaching_Employees()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var administrator = await CreateEmployeeAsync(
				"Administrator",
				EmployeeStatusType.Active,
				EmployeeGroupType.NonTeaching);
			var input = new SaveClassTeacherAssignmentsDto
			{
				OrganizationId = Guid.NewGuid(),
				SaveAsDraft = true,
				Assignments =
				[
					new ClassTeacherAssignmentUpdateDto
					{
						ClassId = Guid.NewGuid(),
						DivisionId = Guid.NewGuid(),
						TeacherId = administrator.Id
					}
				]
			};

			var validation = await _service.ValidateClassTeacherAssignmentsAsync(input);

			validation.CanConfirm.ShouldBeFalse();
			validation.Issues.ShouldContain(issue =>
				issue.Code == "NON_TEACHING_EMPLOYEE" &&
				issue.TeacherId == administrator.Id &&
				issue.Severity == "Critical");

			var exception = await Should.ThrowAsync<BusinessException>(() =>
				_service.SaveClassTeacherAssignmentsAsync(input));
			exception.Code.ShouldBe("ClassManagement:ClassTeacherAssignmentValidationFailed");
		});
	}

	private async Task<Employee> CreateEmployeeAsync(
		string firstName,
		EmployeeStatusType status,
		EmployeeGroupType employeeGroup)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var roleId = Guid.NewGuid();
		await _roleRepository.InsertAsync(
			new IdentityRole(roleId, $"class-teacher-role-{suffix[..8]}", _currentTenant.Id),
			autoSave: true);

		var employee = new Employee(
			Guid.NewGuid(),
			tenantId: _currentTenant.Id,
			userId: null,
			employeeCode: $"CTA{suffix[..10]}",
			basicInfo: new EmployeeBasicInfo(
				ProfilePictureBlobName: null,
				LastName: "Candidate",
				FirstName: firstName,
				MiddleName: null,
				ShortName: firstName,
				EmployeeStatusType: status,
				DateOfBirth: new DateOnly(1990, 1, 1),
				FamilyCast: null,
				CastType: null,
				GenderType: null,
				Religion: null,
				MaritalStatus: null,
				BloodGroup: null,
				MobileNo: null,
				SecondaryMobileNo: null,
				Email: $"class-teacher-{suffix[..12]}@test.local",
				PanNo: $"ABCDE{suffix[..4].ToUpperInvariant()}F",
				AdharNo: $"1{Math.Abs((long)suffix.GetHashCode()):D11}"[..12],
				Address1: null,
				Address2: null,
				Address3: null,
				Department: employeeGroup == EmployeeGroupType.Teaching ? "Academics" : "Administration",
				Designation: employeeGroup == EmployeeGroupType.Teaching ? "Teacher" : "Administrator",
				DateOfJoining: new DateOnly(2025, 1, 1),
				EmployeeGroup: employeeGroup,
				Qualification: null,
				RoleId: roleId,
				IdentificationMark1: null,
				City: null,
				State: null,
				Country: null,
				PinCode: null,
				ShiftId: null),
			bankDetails: new BankDetails(null, null, null, null, null),
			workExperiences: null);

		return await _employeeRepository.InsertAsync(employee, autoSave: true);
	}
}
