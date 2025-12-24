using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.FamilyInfo;

/// <summary>
/// Application service for managing employee family information.
/// </summary>
public class EmployeeFamilyInfoAppService(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager)
	: ApplicationService, IEmployeeFamilyInfoAppService
{
	/// <inheritdoc />
	public async Task<FamilyInfoResponse> GetAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		return new FamilyInfoResponse
		{
			Id = employee.Id,
			FatherDetails = MapContactDetailsToFamilyMemberInfo(employee.FatherDetails),
			MotherDetails = MapContactDetailsToFamilyMemberInfo(employee.MotherDetails),
			SpouseDetails = MapContactDetailsToFamilyMemberInfo(employee.SpouseDetails)
		};
	}

	/// <inheritdoc />
	public async Task UpsertAsync(Guid employeeId, FamilyInfoRequest input)
	{
		// Map DTOs to domain value objects
		var fatherDetails = MapFamilyMemberInfoToContactDetails(input.FatherDetails);
		var motherDetails = MapFamilyMemberInfoToContactDetails(input.MotherDetails);
		var spouseDetails = MapFamilyMemberInfoToContactDetails(input.SpouseDetails);

		// Update family info using EmployeeManager
		var employee = await employeeManager.UpdateFamilyInfoByEmployeeIdAsync(
			employeeId,
			fatherDetails,
			motherDetails,
			spouseDetails);

		// Persist changes
		await employeeRepository.UpdateAsync(employee);
	}

	private static FamilyMemberInfo? MapContactDetailsToFamilyMemberInfo(ContactDetails? details)
	{
		if (details == null)
		{
			return null;
		}

		return new FamilyMemberInfo
		{
			Name = details.Name,
			Occupation = details.Occupation,
			MobileNo = details.ContactNo,
			Address1 = details.Address1,
			Address2 = details.Address2,
			Address3 = details.Address3,
			City = details.City,
			Pincode = details.PinCode,
			Country = !string.IsNullOrEmpty(details.CountryName) || !string.IsNullOrEmpty(details.CountryIsoCode)
				? new CountryInfo
				{
					Name = details.CountryName ?? string.Empty,
					IsoCode = details.CountryIsoCode ?? string.Empty
				}
				: null,
			State = !string.IsNullOrEmpty(details.StateName) || !string.IsNullOrEmpty(details.StateIsoCode)
				? new StateInfo
				{
					Name = details.StateName ?? string.Empty,
					IsoCode = details.StateIsoCode ?? string.Empty
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

