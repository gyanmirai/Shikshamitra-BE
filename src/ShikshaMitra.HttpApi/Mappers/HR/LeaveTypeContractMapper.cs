using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using System.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveTypeInfo;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class LeaveTypeContractMapper
{
	/// <summary>
	/// Converts API create request to internal DTO
	/// </summary>
	public static CreateLeaveTypeDto ToCreateDto(LeaveTypeCreateRequest request)
	{
		return new CreateLeaveTypeDto
		{
			Type = request.Name,
			Color = request.Color
		};
	}

	/// <summary>
	/// Converts API update request to internal DTO
	/// </summary>
	public static UpdateLeaveTypeDto ToUpdateDto(LeaveTypeUpdateRequest request)
	{
		return new UpdateLeaveTypeDto
		{
			Type = request.Name ?? string.Empty,
			Color = request.Color ?? string.Empty
		};
	}

	/// <summary>
	/// Converts internal DTO to API response
	/// </summary>
	public static LeaveTypeResponse ToResponse(LeaveTypeDto dto)
	{
		return new LeaveTypeResponse
		{
			Id = dto.Id,
			Name = dto.Type,
			Description = null, // Not stored in current DTO
			Color = dto.Color
		};
	}

	/// <summary>
	/// Converts internal DTO to API list item response
	/// </summary>
	public static LeaveTypeItemResponse ToItemResponse(LeaveTypeDto dto)
	{
		return new LeaveTypeItemResponse
		{
			Id = dto.Id,
			Name = dto.Type,
			Description = null, // Not stored in current DTO
			Color = dto.Color
		};
	}

	/// <summary>
	/// Converts paginated internal DTOs to API list response
	/// </summary>
	public static LeaveTypeListResponse ToListResponse(
		Volo.Abp.Application.Dtos.PagedResultDto<LeaveTypeDto> pagedResult,
		int currentPage,
		int pageSize)
	{
		var totalPages = (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize);

		return new LeaveTypeListResponse
		{
			TotalCount = (int)pagedResult.TotalCount,
			CurrentPage = currentPage,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = pagedResult.Items.Select(ToItemResponse).ToList()
		};
	}

	/// <summary>
	/// Creates delete response
	/// </summary>
	public static LeaveTypeDeleteResponse ToDeleteResponse(Guid id)
	{
		return new LeaveTypeDeleteResponse
		{
			Id = id,
			Message = "Leave type deleted successfully"
		};
	}
}
