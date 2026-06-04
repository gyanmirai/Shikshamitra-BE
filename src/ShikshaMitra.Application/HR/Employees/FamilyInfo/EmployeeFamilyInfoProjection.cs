using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Employees.FamilyInfo;

public static class EmployeeFamilyInfoProjection
{
	public static FamilyInfoResponse ToResponse(Guid employeeId, IReadOnlyCollection<EmployeeFamilyMember> members)
	{
		return new FamilyInfoResponse
		{
			Id = employeeId,
			FatherDetails = ToFamilyMemberInfo(members.FirstOrDefault(x => x.RelationType == FamilyRelationType.Father)),
			MotherDetails = ToFamilyMemberInfo(members.FirstOrDefault(x => x.RelationType == FamilyRelationType.Mother)),
			SpouseDetails = ToFamilyMemberInfo(members.FirstOrDefault(x => x.RelationType == FamilyRelationType.Spouse))
		};
	}

	public static ContactDetails? ToContactDetails(FamilyMemberInfo? info)
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
			pinCode: info.Pincode);
	}

	private static FamilyMemberInfo? ToFamilyMemberInfo(EmployeeFamilyMember? member)
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
			Country = HasCountry(member)
				? new CountryInfo
				{
					Name = member.CountryName ?? string.Empty,
					IsoCode = member.CountryIsoCode ?? string.Empty
				}
				: null,
			State = HasState(member)
				? new StateInfo
				{
					Name = member.StateName ?? string.Empty,
					IsoCode = member.StateIsoCode ?? string.Empty
				}
				: null
		};
	}

	private static bool HasCountry(EmployeeFamilyMember member)
	{
		return !string.IsNullOrEmpty(member.CountryName) || !string.IsNullOrEmpty(member.CountryIsoCode);
	}

	private static bool HasState(EmployeeFamilyMember member)
	{
		return !string.IsNullOrEmpty(member.StateName) || !string.IsNullOrEmpty(member.StateIsoCode);
	}
}
