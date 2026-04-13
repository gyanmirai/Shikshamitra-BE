using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.FamilyInfo;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class EmployeeFamilyInfoAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeFamilyMemberRepository familyMemberRepository,
	EmployeeManager employeeManager)
	: ApplicationService, IEmployeeFamilyInfoAppService
{
	/// <inheritdoc />
	public async Task<FamilyInfoResponse> GetAsync(Guid employeeId)
	{
		// Validate employee exists
		var employee = await employeeRepository.GetAsync(employeeId);

		// Get family members from new table
		var familyMembers = await familyMemberRepository.GetByEmployeeIdAsync(employeeId);

		var father = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Father);
		var mother = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Mother);
		var spouse = familyMembers.FirstOrDefault(fm => fm.RelationType == FamilyRelationType.Spouse);

		return new FamilyInfoResponse
		{
			Id = employee.Id,
			FatherDetails = MapFamilyMemberToFamilyMemberInfo(father),
			MotherDetails = MapFamilyMemberToFamilyMemberInfo(mother),
			SpouseDetails = MapFamilyMemberToFamilyMemberInfo(spouse)
		};
	}

	/// <inheritdoc />
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task UpsertAsync(Guid employeeId, FamilyInfoRequest input)
	{
		// Map DTOs to domain value objects (for backward compatibility)
		var fatherDetails = MapFamilyMemberInfoToContactDetails(input.FatherDetails);
		var motherDetails = MapFamilyMemberInfoToContactDetails(input.MotherDetails);
		var spouseDetails = MapFamilyMemberInfoToContactDetails(input.SpouseDetails);

		// Update family info using EmployeeManager (now updates separate table)
		await employeeManager.UpdateFamilyInfoByEmployeeIdAsync(
			employeeId,
			fatherDetails,
			motherDetails,
			spouseDetails);
	}

	private static FamilyMemberInfo? MapFamilyMemberToFamilyMemberInfo(EmployeeFamilyMember? member)
	{
		if (member == null)
		{
			return null;
		}

		return new FamilyMemberInfo
		{
			Name = member.Name,
			Occupation = member.Occupation,
			MobileNo = member.ContactNo,
			Address1 = member.Address1,
			Address2 = member.Address2,
			Address3 = member.Address3,
			City = member.City,
			Pincode = member.PinCode,
			Country = !string.IsNullOrEmpty(member.CountryName) || !string.IsNullOrEmpty(member.CountryIsoCode)
				? new CountryInfo
				{
					Name = member.CountryName ?? string.Empty,
					IsoCode = member.CountryIsoCode ?? string.Empty
				}
				: null,
			State = !string.IsNullOrEmpty(member.StateName) || !string.IsNullOrEmpty(member.StateIsoCode)
				? new StateInfo
				{
					Name = member.StateName ?? string.Empty,
					IsoCode = member.StateIsoCode ?? string.Empty
				}
				: null
		};
	}

	private static ContactDetails? MapFamilyMemberInfoToContactDetails(FamilyMemberInfo? info)
	{
		if (info == null)
		{
			return null;
		}

		return new ContactDetails(
			name: info.Name,
			occupation: info.Occupation,
			contactNo: info.MobileNo,
			address1: info.Address1,
			address2: info.Address2,
			address3: info.Address3,
			city: info.City,
			stateName: info.State?.Name,
			stateIsoCode: info.State?.IsoCode,
			countryName: info.Country?.Name,
			countryIsoCode: info.Country?.IsoCode,
			pinCode: info.Pincode
		);
	}
}

