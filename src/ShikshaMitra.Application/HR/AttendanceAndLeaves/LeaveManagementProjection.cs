using System;
using System.Collections.Generic;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class LeaveManagementProjection
{
	public static LeaveManagementDto ToManagementDto(
		EmployeeLeave leave,
		(string Name, string? EmployeeCode) submitter) =>
		new()
		{
			Id = leave.Id,
			PublicId = leave.PublicId,
			EmployeeId = leave.EmployeeId,
			Name = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeIdentifier = leave.Employee?.EmployeeCode ?? "Unknown",
			SubmittedByName = submitter.Name,
			SubmittedByEmployeeCode = submitter.EmployeeCode,
			LeaveType = leave.LeaveType?.Type ?? "Unknown",
			LeaveTypeMasterId = leave.LeaveTypeId,
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			HalfDay = leave.HalfDay,
			ApprovedLeaveUnits = GetApprovedLeaveUnits(leave),
			MediumStageName = leave.LeavePolicy?.AcademicYear?.Name ?? "",
			Description = leave.Description ?? "",
			Remarks = leave.RejectionReason,
			Status = ToApiStatus(leave.Status)
		};

	public static LeaveManagementCalendarItemDto ToCalendarItemDto(
		EmployeeLeave leave,
		IReadOnlyDictionary<Guid, (Guid MediumStageId, string MediumStageName)> employeeMediumStageLookup)
	{
		(Guid MediumStageId, string MediumStageName)? mediumStage = null;
		if (leave.Employee?.UserId is Guid userId &&
		    employeeMediumStageLookup.TryGetValue(userId, out var resolvedMediumStage))
		{
			mediumStage = resolvedMediumStage;
		}

		return new LeaveManagementCalendarItemDto
		{
			MediumStageId = mediumStage?.MediumStageId,
			Id = leave.Id,
			PublicId = leave.PublicId,
			EmployeeId = leave.EmployeeId,
			EmployeeName = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeCode = leave.Employee?.EmployeeCode ?? "Unknown",
			LeaveTypeName = leave.LeaveType?.Type ?? "Unknown",
			LeaveTypeColor = leave.LeaveType?.Color,
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			HalfDay = leave.HalfDay,
			ApprovedLeaveUnits = GetApprovedLeaveUnits(leave),
			Status = ToApiStatus(leave.Status),
			MediumStageName = mediumStage?.MediumStageName ?? string.Empty
		};
	}

	public static LeaveRequestDetailsDto ToDetailsDto(
		EmployeeLeave leave,
		(string Name, string? EmployeeCode) submitter) =>
		new()
		{
			Id = leave.Id,
			PublicId = leave.PublicId,
			MediumStageId = leave.LeavePolicy?.AcademicYearId ?? Guid.Empty,
			MediumStageName = leave.LeavePolicy?.AcademicYear?.Name ?? "",
			EmployeeId = leave.EmployeeId,
			EmployeeName = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeCode = leave.Employee?.EmployeeCode ?? "Unknown",
			SubmittedByName = submitter.Name,
			SubmittedByEmployeeCode = submitter.EmployeeCode,
			LeavePolicyId = leave.LeavePolicyId,
			LeaveTypeMasterId = leave.LeaveTypeId,
			LeaveType = leave.LeaveType?.Type ?? "Unknown",
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay),
			HalfDay = leave.HalfDay,
			ApprovedLeaveUnits = GetApprovedLeaveUnits(leave),
			Description = leave.Description ?? "",
			AttachmentUrl = leave.Attachment != null ? $"/api/leave-management/{leave.Id}/attachment" : null,
			Remarks = leave.RejectionReason,
			Status = ToApiStatus(leave.Status)
		};

	public static decimal CalculateDays(DateTime startDate, DateTime endDate, bool isHalfDay) =>
		LeaveDurationCalculator.CountLeaveUnits(startDate, endDate, isHalfDay);

	public static decimal GetApprovedLeaveUnits(EmployeeLeave leave) =>
		leave.ApprovedLeaveUnits > 0
			? leave.ApprovedLeaveUnits
			: leave.Status == EmployeeLeaveStatusType.Approved
				? CalculateDays(leave.StartDate.DateTime, leave.EndDate.DateTime, leave.HalfDay)
				: 0m;

	private static string ToApiStatus(EmployeeLeaveStatusType status) => status.ToString().ToUpperInvariant();
}
