using System;
using System.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public static class LeaveManagementContractMapper
{
	public static LeaveManagementItemResponse ToItemResponse(LeaveManagementDto dto)
	{
		return new LeaveManagementItemResponse
		{
			Id = dto.Id,
			PublicId = dto.PublicId,
			EmployeeId = dto.EmployeeId,
			Name = dto.Name,
			EmployeeIdentifier = dto.EmployeeIdentifier,
			SubmittedByName = dto.SubmittedByName,
			SubmittedByEmployeeCode = dto.SubmittedByEmployeeCode,
			PaidType = dto.PaidType,
			StartDate = dto.StartDate,
			EndDate = dto.EndDate,
			TotalDays = dto.TotalDays,
			HalfDay = dto.HalfDay,
			ApprovedLeaveUnits = dto.ApprovedLeaveUnits,
			MediumStageName = dto.MediumStageName,
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
			PublicId = dto.PublicId,
			MediumStageId = dto.MediumStageId,
			MediumStageName = dto.MediumStageName,
			EmployeeId = dto.EmployeeId,
			EmployeeName = dto.EmployeeName,
			EmployeeCode = dto.EmployeeCode,
			SubmittedByName = dto.SubmittedByName,
			SubmittedByEmployeeCode = dto.SubmittedByEmployeeCode,
			LeavePolicyId = dto.LeavePolicyId,
			PaidType = dto.PaidType,
			StartDate = dto.StartDate,
			EndDate = dto.EndDate,
			TotalDays = dto.TotalDays,
			HalfDay = dto.HalfDay,
			ApprovedLeaveUnits = dto.ApprovedLeaveUnits,
			Description = dto.Description,
			AttachmentUrl = dto.AttachmentUrl,
			Remarks = dto.Remarks,
			Status = dto.Status
		};
	}

	public static LeaveRequestCreatedResponse ToCreatedResponse(LeaveRequestCreatedDto dto)
	{
		return new LeaveRequestCreatedResponse
		{
			Id = dto.Id,
			PublicId = dto.PublicId,
			Status = dto.Status
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

	public static LeaveActionResponse ToActionResponse(LeaveActionResultDto dto)
	{
		return new LeaveActionResponse
		{
			Id = dto.Id,
			Status = dto.Status,
			Message = dto.Message
		};
	}
}
