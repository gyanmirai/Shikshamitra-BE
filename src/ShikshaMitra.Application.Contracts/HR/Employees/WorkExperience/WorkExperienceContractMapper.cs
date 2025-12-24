using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Employees.WorkExperienceContracts;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class WorkExperienceContractMapper
{
	/// <summary>
	/// Converts API request list to internal update DTO list
	/// </summary>
	public static List<CreateUpdateWorkExperienceDto> ToUpdateDtoList(List<WorkExperienceContract> requests)
	{
		return requests.Select(ToUpdateDto).ToList();
	}

	/// <summary>
	/// Converts API request to internal update DTO
	/// </summary>
	private static CreateUpdateWorkExperienceDto ToUpdateDto(WorkExperienceContract request)
	{
		// Combine location info into organization address
		var addressParts = new List<string>();
		if (!string.IsNullOrEmpty(request.City)) addressParts.Add(request.City);
		if (request.State != null && !string.IsNullOrEmpty(request.State.Name)) addressParts.Add(request.State.Name);
		if (request.Country != null && !string.IsNullOrEmpty(request.Country.Name)) addressParts.Add(request.Country.Name);
		
		var organizationAddress = addressParts.Count > 0 ? string.Join(", ", addressParts) : null;

		return new CreateUpdateWorkExperienceDto
		{
			OrganizationName = request.MediumStageName,
			OrganizationAddress = organizationAddress,
			FromDate = request.StartDate.HasValue ? DateOnly.FromDateTime(request.StartDate.Value) : null,
			ToDate = request.EndDate.HasValue ? DateOnly.FromDateTime(request.EndDate.Value) : null,
			Department = request.Department,
			Designation = request.Designation,
			Responsibilities = null,
			References = null
		};
	}

	/// <summary>
	/// Converts internal DTO list to API response list
	/// </summary>
	public static List<WorkExperienceContract> ToResponseList(List<WorkExperienceDto> dtos)
	{
		return dtos.Select(ToResponse).ToList();
	}

	/// <summary>
	/// Converts internal DTO to API response
	/// </summary>
	private static WorkExperienceContract ToResponse(WorkExperienceDto dto)
	{
		// Parse organization address back to location components
		CountryInfo? country = null;
		StateInfo? state = null;
		string? city = null;

		if (!string.IsNullOrEmpty(dto.OrganizationAddress))
		{
			var parts = dto.OrganizationAddress.Split(',').Select(p => p.Trim()).ToArray();
			if (parts.Length > 0) city = parts[0];
			if (parts.Length > 1) state = new StateInfo { Name = parts[1], IsoCode = "" };
			if (parts.Length > 2) country = new CountryInfo { Name = parts[2], IsoCode = "" };
		}

		return new WorkExperienceContract
		{
			Id = Guid.NewGuid(), // Generate ID for response
			MediumStageName = dto.OrganizationName,
			Designation = dto.Designation,
			Department = dto.Department,
			Country = country,
			State = state,
			City = city,
			StartDate = dto.FromDate.HasValue ? dto.FromDate.Value.ToDateTime(TimeOnly.MinValue) : null,
			EndDate = dto.ToDate.HasValue ? dto.ToDate.Value.ToDateTime(TimeOnly.MinValue) : null
		};
	}
}
