using ShikshaMitra.HR.Employees.AdditionalDetails;

namespace ShikshaMitra.HR.Employees.AdditionalInfo;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class AdditionalInfoContractMapper
{
	/// <summary>
	/// Converts API request to internal update DTO
	/// </summary>
	public static UpdateEmployeeAdditionalDetailsDto ToUpdateDto(AdditionalInfoContract request)
	{
		return new UpdateEmployeeAdditionalDetailsDto
		{
			BplCardNo = request.BplCardNo,
			PersonWithDisability = request.PersonWithDisability,
			SecondLanguage = request.SecondLanguage,
			LibraryMember = request.IsLibraryMember,
			TransportMember = request.IsTransportMember
		};
	}

	/// <summary>
	/// Converts internal DTO to API response
	/// </summary>
	public static AdditionalInfoContract ToResponse(EmployeeAdditionalDetailsDto dto)
	{
		return new AdditionalInfoContract
		{
			BplCardNo = dto.BplCardNo,
			PersonWithDisability = dto.PersonWithDisability,
			SecondLanguage = dto.SecondLanguage,
			IsLibraryMember = dto.LibraryMember,
			IsTransportMember = dto.TransportMember
		};
	}
}
