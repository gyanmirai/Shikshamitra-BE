using System;

namespace ShikshaMitra.HR.Employees.FamilyInfo;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class FamilyInfoContractMapper
{
	/// <summary>
	/// Converts API request to internal update DTO
	/// </summary>
	public static UpdateEmployeeFamilyDetailsDto ToUpdateDto(FamilyInfoRequest request)
	{
		return new UpdateEmployeeFamilyDetailsDto
		{
			FatherDetails = ToContactDetailsDto(request.FatherDetails),
			MotherDetails = ToContactDetailsDto(request.MotherDetails),
			SpouseDetails = ToContactDetailsDto(request.SpouseDetails)
		};
	}

	/// <summary>
	/// Converts internal DTO to API response
	/// </summary>
	public static FamilyInfoResponse ToResponse(Guid employeeId, UpdateEmployeeFamilyDetailsDto dto)
	{
		return new FamilyInfoResponse
		{
			Id = employeeId,
			FatherDetails = ToFamilyMemberInfo(dto.FatherDetails),
			MotherDetails = ToFamilyMemberInfo(dto.MotherDetails),
			SpouseDetails = ToFamilyMemberInfo(dto.SpouseDetails)
		};
	}

	/// <summary>
	/// Converts internal DTOs to API response
	/// </summary>
	public static FamilyInfoResponse ToResponse(Guid employeeId, ContactDetailsDto? father, ContactDetailsDto? mother, ContactDetailsDto? spouse)
	{
		return new FamilyInfoResponse
		{
			Id = employeeId,
			FatherDetails = ToFamilyMemberInfo(father),
			MotherDetails = ToFamilyMemberInfo(mother),
			SpouseDetails = ToFamilyMemberInfo(spouse)
		};
	}

	private static ContactDetailsDto? ToContactDetailsDto(FamilyMemberInfo? memberInfo)
	{
		if (memberInfo == null)
		{
			return null;
		}

		return new ContactDetailsDto
		{
			Name = memberInfo.Name,
			Occupation = memberInfo.Occupation,
			ContactNo = memberInfo.MobileNo,
			Address1 = memberInfo.Address1,
			Address2 = memberInfo.Address2,
			Address3 = memberInfo.Address3,
			City = memberInfo.City,
			StateName = memberInfo.State?.Name,
			StateIsoCode = memberInfo.State?.IsoCode,
			CountryName = memberInfo.Country?.Name,
			CountryIsoCode = memberInfo.Country?.IsoCode,
			PinCode = memberInfo.Pincode
		};
	}

		private static FamilyMemberInfo? ToFamilyMemberInfo(ContactDetailsDto? contactDetails)
		{
			if (contactDetails == null)
			{
				return null;
			}

			return new FamilyMemberInfo
			{
				Name = contactDetails.Name,
				Occupation = contactDetails.Occupation,
				MobileNo = contactDetails.ContactNo,
				Address1 = contactDetails.Address1,
				Address2 = contactDetails.Address2,
				Address3 = contactDetails.Address3,
				City = contactDetails.City,
				State = !string.IsNullOrEmpty(contactDetails.StateName)
					? new StateInfo { Name = contactDetails.StateName, IsoCode = contactDetails.StateIsoCode ?? string.Empty }
					: null,
				Country = !string.IsNullOrEmpty(contactDetails.CountryName)
					? new CountryInfo { Name = contactDetails.CountryName, IsoCode = contactDetails.CountryIsoCode ?? string.Empty }
					: null,
				Pincode = contactDetails.PinCode
			};
		}
	}
