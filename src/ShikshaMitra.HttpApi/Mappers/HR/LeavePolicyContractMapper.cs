using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using System;
using System.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeavePolicyInfo;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class LeavePolicyContractMapper
{
	/// <summary>
	/// Converts API create request to internal DTO
	/// </summary>
	public static CreateLeavePolicyDto ToCreateDto(LeavePolicyCreateRequest request)
	{
		return new CreateLeavePolicyDto
		{
			PolicyName = request.PolicyTitle,
			LeaveType = request.LeaveType.ToString(),
			MaximumDaysPerYear = request.MaxLeavePerYear,
			PaidType = ParsePaidType(request.PaidType),
			CarryForward = request.CarryForward,
			Encashable = request.EncashmentAllowed,
			RequiresApproval = request.RequiresApproval,
			IsDefault = request.IsDefault,
			Description = request.Description
		};
	}

	/// <summary>
	/// Converts API update request to internal DTO
	/// </summary>
	public static UpdateLeavePolicyDto ToUpdateDto(LeavePolicyCreateRequest request)
	{
		return new UpdateLeavePolicyDto
		{
			PolicyName = request.PolicyTitle,
			LeaveType = request.LeaveType.ToString(),
			MaximumDaysPerYear = request.MaxLeavePerYear,
			PaidType = ParsePaidType(request.PaidType),
			CarryForward = request.CarryForward,
			Encashable = request.EncashmentAllowed,
			RequiresApproval = request.RequiresApproval,
			IsDefault = request.IsDefault,
			Description = request.Description
		};
	}

	/// <summary>
	/// Converts internal DTO to API response
	/// </summary>
	public static LeavePolicyResponse ToResponse(LeavePolicyDto dto)
	{
		return new LeavePolicyResponse
		{
			Id = dto.Id.ToString(),
			PolicyTitle = dto.PolicyName,
			LeaveType = Guid.Empty,
			MaxLeavePerYear = dto.RuleCount,
			PaidType = string.Empty,
			CarryForward = false,
			EncashmentAllowed = false,
			RequiresApproval = false,
			IsDefault = dto.IsDefault,
			Description = dto.Description,
			CreatedAt = dto.CreationTime
		};
	}

	/// <summary>
	/// Converts internal DTO to API updated response
	/// </summary>
	public static LeavePolicyUpdatedResponse ToUpdatedResponse(LeavePolicyDto dto)
	{
		return new LeavePolicyUpdatedResponse
		{
			Id = dto.Id.ToString(),
			PolicyTitle = dto.PolicyName,
			LeaveType = Guid.Empty,
			MaxLeavePerYear = dto.RuleCount,
			PaidType = string.Empty,
			CarryForward = false,
			EncashmentAllowed = false,
			RequiresApproval = false,
			IsDefault = dto.IsDefault,
			Description = dto.Description,
			UpdatedAt = dto.LastModificationTime ?? dto.CreationTime
		};
	}

	/// <summary>
	/// Converts internal DTO to API list item response
	/// </summary>
	public static LeavePolicyItemResponse ToItemResponse(LeavePolicyDto dto, int index)
	{
		return new LeavePolicyItemResponse
		{
			Id = index,
			Title = dto.PolicyName,
			LeaveType = $"{dto.RuleCount} rule(s)",
			MaximumPerYear = dto.RuleCount,
			CarryForward = "-",
			Encashable = "-",
			PaidType = "-",
			RequiresApproval = "-"
		};
	}

	/// <summary>
	/// Converts paginated internal DTOs to API list response
	/// </summary>
	public static LeavePolicyListResponse ToListResponse(
		Volo.Abp.Application.Dtos.PagedResultDto<LeavePolicyDto> pagedResult,
		int currentPage,
		int pageSize)
	{
		var totalPages = (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize);

		return new LeavePolicyListResponse
		{
			TotalCount = (int)pagedResult.TotalCount,
			CurrentPage = currentPage,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = pagedResult.Items.Select((dto, index) => ToItemResponse(dto, index + 1)).ToList()
		};
	}

	/// <summary>
	/// Creates delete response
	/// </summary>
	public static LeavePolicyDeleteResponse ToDeleteResponse(Guid id, DateTime deletedAt)
	{
		return new LeavePolicyDeleteResponse
		{
			Id = id.ToString(),
			Message = "Leave policy deleted successfully",
			DeletedAt = deletedAt
		};
	}

	private static LeavePaidType ParsePaidType(string paidType)
	{
		return paidType.ToLowerInvariant() switch
		{
			"paid" => LeavePaidType.Paid,
			"unpaid" => LeavePaidType.Unpaid,
			_ => LeavePaidType.Paid
		};
	}
}
