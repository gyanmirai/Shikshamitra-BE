using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR.Employees;

/// <summary>
/// Tests for EmployeeFamilyMember repository operations
/// </summary>
public class EmployeeFamilyMember_Repository_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	private readonly IEmployeeFamilyMemberRepository _familyMemberRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IGuidGenerator _guidGenerator;

	public EmployeeFamilyMember_Repository_Tests()
	{
		_familyMemberRepository = GetRequiredService<IEmployeeFamilyMemberRepository>();
		_employeeRepository = GetRequiredService<IEmployeeRepository>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task Should_Create_Family_Member()
	{
		// Arrange
		var employee = await GetFirstEmployeeAsync();
		var familyMember = new EmployeeFamilyMember(
			id: _guidGenerator.Create(),
			tenantId: null,
			employeeId: employee.Id,
			relationType: FamilyRelationType.Father,
			name: "Test Father",
			occupation: "Engineer",
			contactNo: "9876543210",
			address1: "Address 1",
			address2: "Address 2",
			address3: null,
			city: "Mumbai",
			stateName: "Maharashtra",
			stateIsoCode: "MH",
			countryName: "India",
			countryIsoCode: "IN",
			pinCode: "400001"
		);

		// Act
		var result = await _familyMemberRepository.InsertAsync(familyMember, autoSave: true);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldNotBe(Guid.Empty);
		result.EmployeeId.ShouldBe(employee.Id);
		result.RelationType.ShouldBe(FamilyRelationType.Father);
		result.Name.ShouldBe("Test Father");
	}

	[Fact]
	public async Task Should_Get_Family_Members_By_EmployeeId()
	{
		// Arrange
		var employee = await GetFirstEmployeeAsync();
		await CreateTestFamilyMembersAsync(employee.Id);

		// Act
		var familyMembers = await _familyMemberRepository.GetByEmployeeIdAsync(employee.Id);

		// Assert
		familyMembers.ShouldNotBeNull();
		familyMembers.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task Should_Get_Family_Member_By_RelationType()
	{
		// Arrange
		var employee = await GetFirstEmployeeAsync();
		await CreateTestFamilyMembersAsync(employee.Id);

		// Act
		var father = await _familyMemberRepository.GetByEmployeeIdAndRelationTypeAsync(
			employee.Id,
			FamilyRelationType.Father);

		// Assert
		father.ShouldNotBeNull();
		father.RelationType.ShouldBe(FamilyRelationType.Father);
		father.EmployeeId.ShouldBe(employee.Id);
	}

	[Fact]
	public async Task Should_Update_Family_Member()
	{
		// Arrange
		var employee = await GetFirstEmployeeAsync();
		var familyMember = await CreateTestFamilyMembersAsync(employee.Id);

		// Act
		familyMember.Update(
			name: "Updated Father Name",
			occupation: "Updated Occupation",
			contactNo: "9999999999",
			address1: "New Address 1",
			address2: "New Address 2",
			address3: null,
			city: "Delhi",
			stateName: "Delhi",
			stateIsoCode: "DL",
			countryName: "India",
			countryIsoCode: "IN",
			pinCode: "110001"
		);

		var updated = await _familyMemberRepository.UpdateAsync(familyMember, autoSave: true);

		// Assert
		updated.Name.ShouldBe("Updated Father Name");
		updated.Occupation.ShouldBe("Updated Occupation");
		updated.ContactNo.ShouldBe("9999999999");
		updated.City.ShouldBe("Delhi");
	}

	[Fact]
	public async Task Should_Delete_Family_Member()
	{
		// Arrange
		var employee = await GetFirstEmployeeAsync();
		var familyMember = await CreateTestFamilyMembersAsync(employee.Id);

		// Act
		await _familyMemberRepository.DeleteAsync(familyMember, autoSave: true);

		// Assert
		var result = await _familyMemberRepository.GetByEmployeeIdAndRelationTypeAsync(
			employee.Id,
			FamilyRelationType.Father);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task Should_Enforce_Unique_Constraint_On_EmployeeId_And_RelationType()
	{
		// Arrange
		var employee = await GetFirstEmployeeAsync();
		await CreateTestFamilyMembersAsync(employee.Id);

		// Attempt to create duplicate Father record
		var duplicateFather = new EmployeeFamilyMember(
			id: _guidGenerator.Create(),
			tenantId: null,
			employeeId: employee.Id,
			relationType: FamilyRelationType.Father,
			name: "Duplicate Father",
			occupation: "Test",
			contactNo: "1234567890",
			address1: null,
			address2: null,
			address3: null,
			city: null,
			stateName: null,
			stateIsoCode: null,
			countryName: null,
			countryIsoCode: null,
			pinCode: null
		);

		// Act & Assert
		await Should.ThrowAsync<Exception>(async () =>
		{
			await _familyMemberRepository.InsertAsync(duplicateFather, autoSave: true);
		});
	}

	[Fact]
	public async Task GetWithFamilyAsync_Should_Load_Family_Members()
	{
		// Arrange
		var employee = await GetFirstEmployeeAsync();
		await CreateTestFamilyMembersAsync(employee.Id);

		// Act
		var employeeWithFamily = await _employeeRepository.GetWithFamilyAsync(employee.Id);

		// Assert
		employeeWithFamily.ShouldNotBeNull();
		employeeWithFamily.FamilyMembers.ShouldNotBeNull();
		employeeWithFamily.FamilyMembers.Count.ShouldBeGreaterThanOrEqualTo(1);
		employeeWithFamily.FamilyMembers.Any(fm => fm.RelationType == FamilyRelationType.Father).ShouldBeTrue();
	}

	#region Helper Methods

	private async Task<Employee> GetFirstEmployeeAsync()
	{
		var employees = await _employeeRepository.GetListAsync();
		return employees.FirstOrDefault()
			?? throw new Exception("No employees found in database. Ensure seeder has run.");
	}

	private async Task<EmployeeFamilyMember> CreateTestFamilyMembersAsync(Guid employeeId)
	{
		// Create Father
		var father = new EmployeeFamilyMember(
			id: _guidGenerator.Create(),
			tenantId: null,
			employeeId: employeeId,
			relationType: FamilyRelationType.Father,
			name: "Test Father",
			occupation: "Engineer",
			contactNo: "9876543210",
			address1: "Test Address 1",
			address2: "Test Address 2",
			address3: null,
			city: "Mumbai",
			stateName: "Maharashtra",
			stateIsoCode: "MH",
			countryName: "India",
			countryIsoCode: "IN",
			pinCode: "400001"
		);
		await _familyMemberRepository.InsertAsync(father, autoSave: true);

		// Create Mother
		var mother = new EmployeeFamilyMember(
			id: _guidGenerator.Create(),
			tenantId: null,
			employeeId: employeeId,
			relationType: FamilyRelationType.Mother,
			name: "Test Mother",
			occupation: "Teacher",
			contactNo: "9876543211",
			address1: "Test Address 1",
			address2: "Test Address 2",
			address3: null,
			city: "Mumbai",
			stateName: "Maharashtra",
			stateIsoCode: "MH",
			countryName: "India",
			countryIsoCode: "IN",
			pinCode: "400001"
		);
		await _familyMemberRepository.InsertAsync(mother, autoSave: true);

		return father;
	}

	#endregion
}
