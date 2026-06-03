using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeePersonalInfoProjection(
	EmployeeOrganizationScopeResolver organizationScopeResolver,
	IObjectMapper objectMapper) : ITransientDependency
{
	public async Task<EmployeePersonalInfoDto> ToPersonalInfoDtoAsync(Employee employee)
	{
		var dto = ToPersonalInfoDto(employee);
		await PopulateScopedOrganizationsAsync(dto, employee.UserId);
		return dto;
	}

	public async Task<EmployeeDto> ToEmployeeDtoAsync(Employee employee)
	{
		var dto = objectMapper.Map<Employee, EmployeeDto>(employee);
		await PopulateScopedOrganizationsAsync(dto, employee.UserId);
		return dto;
	}

	public async Task PopulateScopedOrganizationsAsync(EmployeePersonalInfoDto dto, Guid? userId)
	{
		var organizations = await ResolveScopedOrganizationsAsync(userId);

		dto.OrganizationIds = organizations.Select(x => x.Id).ToList();
		dto.Organizations = organizations;
		dto.OrganizationNames = organizations
			.Select(x => x.Name)
			.Distinct()
			.OrderBy(x => x)
			.ToList();
	}

	public async Task PopulateScopedOrganizationsAsync(EmployeeDto dto, Guid? userId)
	{
		var organizations = await ResolveScopedOrganizationsAsync(userId);

		dto.OrganizationIds = organizations.Select(x => x.Id).ToList();
		dto.Organizations = organizations;
		dto.OrganizationNames = organizations
			.Select(x => x.Name)
			.Distinct()
			.OrderBy(x => x)
			.ToList();
	}

	public void PopulateFamilyDetails(EmployeeDto dto, IReadOnlyCollection<EmployeeFamilyMember> familyMembers)
	{
		dto.FatherDetails = ToContactDetailsDto(familyMembers.FirstOrDefault(x => x.RelationType == FamilyRelationType.Father));
		dto.MotherDetails = ToContactDetailsDto(familyMembers.FirstOrDefault(x => x.RelationType == FamilyRelationType.Mother));
		dto.SpouseDetails = ToContactDetailsDto(familyMembers.FirstOrDefault(x => x.RelationType == FamilyRelationType.Spouse));
	}

	private static EmployeePersonalInfoDto ToPersonalInfoDto(Employee employee) =>
		new()
		{
			Id = employee.Id,
			EmployeeCode = employee.EmployeeCode,
			FullName = employee.EmployeeName,
			UserId = employee.UserId,
			FirstName = employee.FirstName ?? string.Empty,
			LastName = employee.LastName ?? string.Empty,
			MiddleName = employee.MiddleName,
			ShortName = employee.ShortName,
			EmployeeStatusType = employee.EmployeeStatusType ?? EmployeeStatusType.Active,
			DateOfBirth = employee.DateOfBirth,
			FamilyCaste = employee.FamilyCast,
			CastType = employee.CastType,
			GenderType = employee.GenderType,
			Religion = employee.Religion,
			MaritalStatus = employee.MaritalStatus,
			BloodGroup = employee.BloodGroup,
			MobileNo = employee.MobileNo ?? string.Empty,
			SecondaryMobileNo = employee.SecondaryMobileNo,
			Email = employee.Email,
			PanNo = employee.PanNo,
			AdharNo = employee.AdharNo,
			Department = employee.Department,
			Designation = employee.Designation,
			DateOfJoining = employee.DateOfJoining,
			EmployeeGroup = employee.EmployeeGroup,
			Qualification = employee.Qualification,
			RoleId = employee.RoleId,
			RoleName = employee.Role?.Name,
			ProfilePictureBlobName = employee.ProfilePictureBlobName,
			IdentificationMark1 = employee.IdentificationMark1,
			Address1 = employee.Address1,
			Address2 = employee.Address2,
			Address3 = employee.Address3,
			Country = employee.Country,
			State = employee.State,
			City = employee.City,
			Pincode = employee.PinCode,
			ShiftId = employee.ShiftId,
			Shift = null,
			OrganizationIds = [],
			Organizations = [],
			OrganizationNames = []
		};

	private static ContactDetailsDto? ToContactDetailsDto(EmployeeFamilyMember? member)
	{
		if (member == null)
		{
			return null;
		}

		return new ContactDetailsDto
		{
			Name = member.Name,
			Occupation = member.Occupation,
			ContactNo = member.ContactNo,
			Address1 = member.Address1,
			Address2 = member.Address2,
			Address3 = member.Address3,
			City = member.City,
			StateName = member.StateName,
			StateIsoCode = member.StateIsoCode,
			CountryName = member.CountryName,
			CountryIsoCode = member.CountryIsoCode,
			PinCode = member.PinCode
		};
	}

	private async Task<List<EmployeeOrganizationDto>> ResolveScopedOrganizationsAsync(Guid? userId)
	{
		var organizationsByUserId = await organizationScopeResolver.GetOrganizationsByUserIdsAsync([userId]);
		return userId.HasValue && organizationsByUserId.TryGetValue(userId.Value, out var scopedOrganizations)
			? scopedOrganizations
			: [];
	}
}
