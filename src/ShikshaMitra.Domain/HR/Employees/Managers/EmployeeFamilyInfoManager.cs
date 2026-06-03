using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Employees;

public class EmployeeFamilyInfoManager(
	IEmployeeRepository employeeRepository,
	IEmployeeFamilyMemberRepository familyMemberRepository)
	: DomainService
{
	public async Task UpdateFamilyInfoByEmployeeIdAsync(
		Guid employeeId,
		ContactDetails? fatherDetails,
		ContactDetails? motherDetails,
		ContactDetails? spouseDetails)
	{
		await EnsureEmployeeExistsAsync(employeeId);

		await UpsertFamilyMemberAsync(employeeId, FamilyRelationType.Father, fatherDetails);
		await UpsertFamilyMemberAsync(employeeId, FamilyRelationType.Mother, motherDetails);
		await UpsertFamilyMemberAsync(employeeId, FamilyRelationType.Spouse, spouseDetails);
	}

	public async Task<List<EmployeeFamilyMember>> GetFamilyMembersByEmployeeIdAsync(Guid employeeId)
	{
		await EnsureEmployeeExistsAsync(employeeId);

		return await familyMemberRepository.GetByEmployeeIdAsync(employeeId);
	}

	private async Task EnsureEmployeeExistsAsync(Guid employeeId)
	{
		_ = await employeeRepository.FindAsync(employeeId, includeDetails: false)
			?? throw new BusinessException(HRErrorCodes.EmployeeNotFound);
	}

	private async Task UpsertFamilyMemberAsync(Guid employeeId, FamilyRelationType relationType, ContactDetails? details)
	{
		var existingMember = await familyMemberRepository.GetByEmployeeIdAndRelationTypeAsync(employeeId, relationType);

		if (details == null || string.IsNullOrWhiteSpace(details.Name))
		{
			if (existingMember != null)
			{
				await familyMemberRepository.DeleteAsync(existingMember);
			}

			return;
		}

		ValidateFamilyMember(details);

		if (existingMember == null)
		{
			var newMember = new EmployeeFamilyMember(
				id: GuidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				employeeId: employeeId,
				relationType: relationType,
				name: details.Name,
				occupation: details.Occupation,
				contactNo: details.ContactNo,
				address1: details.Address1,
				address2: details.Address2,
				address3: details.Address3,
				city: details.City,
				stateName: details.StateName,
				stateIsoCode: details.StateIsoCode,
				countryName: details.CountryName,
				countryIsoCode: details.CountryIsoCode,
				pinCode: details.PinCode);
			await familyMemberRepository.InsertAsync(newMember);
			return;
		}

		existingMember.Update(
			name: details.Name,
			occupation: details.Occupation,
			contactNo: details.ContactNo,
			address1: details.Address1,
			address2: details.Address2,
			address3: details.Address3,
			city: details.City,
			stateName: details.StateName,
			stateIsoCode: details.StateIsoCode,
			countryName: details.CountryName,
			countryIsoCode: details.CountryIsoCode,
			pinCode: details.PinCode);
		await familyMemberRepository.UpdateAsync(existingMember);
	}

	private static void ValidateFamilyMember(ContactDetails details)
	{
		if (string.IsNullOrWhiteSpace(details.Name))
		{
			throw new BusinessException(HRErrorCodes.FamilyMemberNameRequired);
		}

		if (details.Name.Length > 256)
		{
			throw new BusinessException(HRErrorCodes.FamilyMemberNameTooLong);
		}

		if (!string.IsNullOrWhiteSpace(details.ContactNo) && details.ContactNo.Length > 20)
		{
			throw new BusinessException(HRErrorCodes.FamilyContactTooLong);
		}

		if (!string.IsNullOrWhiteSpace(details.PinCode) && details.PinCode.Length > 10)
		{
			throw new BusinessException(HRErrorCodes.FamilyPincodeTooLong);
		}
	}
}
