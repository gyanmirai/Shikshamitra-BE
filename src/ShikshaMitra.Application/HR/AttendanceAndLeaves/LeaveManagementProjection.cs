using System;
using System.Collections.Generic;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class LeaveManagementProjection
{
	public static LeaveManagementDto ToManagementDto(
		EmployeeLeave leave,
		(string Name, string? EmployeeCode) submitter,
		LeaveManagementUnits units) =>
		new()
		{
			Id = leave.Id,
			PublicId = leave.PublicId,
			AppliedDate = leave.CreationTime,
			EmployeeId = leave.EmployeeId,
			Name = leave.Employee?.EmployeeNameWithFather ?? "Unknown",
			EmployeeIdentifier = leave.Employee?.EmployeeCode ?? "Unknown",
			SubmittedByName = submitter.Name,
			SubmittedByEmployeeCode = submitter.EmployeeCode,
			PaidType = ToPaidType(leave.PaidType),
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = units.TotalDays,
			HalfDay = leave.HalfDay,
			ApprovedLeaveUnits = units.ApprovedLeaveUnits,
			MediumStageName = leave.LeavePolicy?.AcademicYear?.Name ?? "",
			Description = leave.Description ?? "",
			Remarks = leave.RejectionReason,
			Status = ToApiStatus(leave.Status)
		};

	public static LeaveManagementCalendarItemDto ToCalendarItemDto(
		EmployeeLeave leave,
		IReadOnlyDictionary<Guid, (Guid MediumStageId, string MediumStageName)> employeeMediumStageLookup,
		LeaveManagementUnits units)
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
			PaidType = ToPaidType(leave.PaidType),
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = units.TotalDays,
			HalfDay = leave.HalfDay,
			ApprovedLeaveUnits = units.ApprovedLeaveUnits,
			Status = ToApiStatus(leave.Status),
			MediumStageName = mediumStage?.MediumStageName ?? string.Empty
		};
	}

	public static LeaveRequestDetailsDto ToDetailsDto(
		EmployeeLeave leave,
		(string Name, string? EmployeeCode) submitter,
		LeaveManagementUnits units) =>
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
			PaidType = ToPaidType(leave.PaidType),
			StartDate = leave.StartDate.DateTime,
			EndDate = leave.EndDate.DateTime,
			TotalDays = units.TotalDays,
			HalfDay = leave.HalfDay,
			ApprovedLeaveUnits = units.ApprovedLeaveUnits,
			Description = leave.Description ?? "",
			AttachmentUrl = leave.Attachment != null ? $"/api/leave-management/{leave.Id}/attachment" : null,
			Remarks = leave.RejectionReason,
			Status = ToApiStatus(leave.Status)
		};

	private static string ToApiStatus(EmployeeLeaveStatusType status) => status.ToString().ToUpperInvariant();

	private static string ToPaidType(LeavePaidType paidType) =>
		paidType == LeavePaidType.Paid ? "Paid" : "Unpaid";
}

internal sealed record LeaveManagementUnits(decimal TotalDays, decimal ApprovedLeaveUnits);
