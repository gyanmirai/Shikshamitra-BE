using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public static class LeaveManagementContractMapper
{
	public static LeaveManagementItemResponse ToItemResponse(LeaveManagementDto dto)
	{
		return new LeaveManagementItemResponse
		{
			Id = dto.Id,
			Name = dto.Name,
			EmployeeCode = dto.EmployeeCode,
			LeaveType = dto.LeaveType,
			StartDate = dto.StartDate,
			EndDate = dto.EndDate,
			TotalDays = dto.TotalDays,
			Organization = dto.Organization,
			Description = dto.Description,
			Remarks = dto.Remarks,
			Status = dto.Status
		};
	}

	public static LeaveManagementListResponse ToListResponse(PagedLeaveManagementResultDto pagedResult, int currentPage, int pageSize)
	{
		var totalPages = (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize);
		return new LeaveManagementListResponse
		{
			TotalCount = pagedResult.TotalCount,
			CurrentPage = currentPage,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = pagedResult.Items.Select(ToItemResponse).ToList()
		};
	}

	public static LeaveRequestDetailsResponse ToDetailsResponse(LeaveRequestDetailsDto dto)
	{
		return new LeaveRequestDetailsResponse
		{
			Id = dto.Id,
			MediumStageId = dto.MediumStageId,
			EmployeeId = dto.EmployeeId,
			EmployeeName = dto.EmployeeName,
			EmployeeCode = dto.EmployeeCode,
			LeaveTypeId = dto.LeaveTypeId,
			LeaveType = dto.LeaveType,
			StartDate = dto.StartDate,
			EndDate = dto.EndDate,
			TotalDays = dto.TotalDays,
			Description = dto.Description,
			AttachmentUrl = dto.AttachmentUrl,
			Remarks = dto.Remarks,
			Status = dto.Status,
			CreatedAt = dto.CreatedAt,
			UpdatedAt = dto.UpdatedAt
		};
	}

	public static LeaveRequestCreatedResponse ToCreatedResponse(LeaveRequestCreatedDto dto)
	{
		return new LeaveRequestCreatedResponse
		{
			Id = dto.Id,
			Status = dto.Status,
			AppliedAt = dto.AppliedAt
		};
	}

	public static LeaveRequestDeleteResponse ToDeleteResponse(LeaveRequestDeleteDto dto)
	{
		return new LeaveRequestDeleteResponse
		{
			Id = dto.Id,
			Message = dto.Message
		};
	}
}
