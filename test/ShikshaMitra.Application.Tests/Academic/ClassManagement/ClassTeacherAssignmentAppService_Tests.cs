using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public class ClassTeacherAssignmentAppService_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IClassTeacherAssignmentAppService _service;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<IdentityRole, Guid> _roleRepository;

	public ClassTeacherAssignmentAppService_Tests()
	{
		_service = GetRequiredService<IClassTeacherAssignmentAppService>();
		_employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
		_roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
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
			new IdentityRole(roleId, $"class-teacher-role-{suffix[..8]}", tenantId: null),
			autoSave: true);

		var employee = new Employee(
			Guid.NewGuid(),
			tenantId: null,
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
