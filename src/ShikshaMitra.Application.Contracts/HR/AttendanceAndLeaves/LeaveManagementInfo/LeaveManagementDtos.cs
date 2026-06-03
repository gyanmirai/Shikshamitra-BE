using System;
using System.Collections.Generic;
using EmployeeLeaveStatusType = ShikshaMitra.HR.AttendanceAndLeaves.EmployeeLeaveStatusType;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public class LeaveManagementGetListInput
{
	public Guid AcademicYearId { get; set; }
	public DateTime? MonthYearAnchor { get; set; }
	public string? Search { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public bool UseExportBatching { get; set; }
	public EmployeeLeaveStatusType? Status { get; set; }
	public Guid? EmployeeId { get; set; }
	public Guid? LeavePolicyId { get; set; }
	public Guid? LeaveTypeMasterId { get; set; }
}

public class LeaveManagementCalendarInput
{
	public Guid AcademicYearId { get; set; }
	public DateTimeOffset RangeStart { get; set; }
	public DateTimeOffset RangeEnd { get; set; }
	public string? Search { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 100;
	public EmployeeLeaveStatusType? Status { get; set; }
	public Guid? LeavePolicyId { get; set; }
	public Guid? LeaveTypeMasterId { get; set; }
}

public class LeaveManagementCalendarResultDto
{
	public int TotalCount { get; set; }
	public List<LeaveManagementCalendarItemDto> Items { get; set; } = new();
}

public class LeaveManagementCalendarItemDto
{
	public Guid Id { get; set; }
	public int PublicId { get; set; }
	public Guid EmployeeId { get; set; }
	public Guid? MediumStageId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string LeaveTypeName { get; set; } = string.Empty;
	public string? LeaveTypeColor { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public decimal TotalDays { get; set; }
	public bool HalfDay { get; set; }
	public decimal ApprovedLeaveUnits { get; set; }
	public string Status { get; set; } = string.Empty;
	public string MediumStageName { get; set; } = string.Empty;
}

public class LeaveBalanceListDto
{
	public List<LeaveBalanceItemDto> Items { get; set; } = new();
}

public class LeaveBalanceItemDto
{
	public Guid LeavePolicyId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public Guid LeaveTypeMasterId { get; set; }
	public string LeaveTypeName { get; set; } = string.Empty;
	public int EntitledDays { get; set; }
	public decimal UsedDays { get; set; }
	public decimal RemainingDays { get; set; }
}

public class LeaveManagementDto
{
	public Guid Id { get; set; }
	public int PublicId { get; set; }
	public Guid EmployeeId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string SubmittedByName { get; set; } = string.Empty;
	public string? SubmittedByEmployeeCode { get; set; }
	public string LeaveType { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public decimal TotalDays { get; set; }
	public bool HalfDay { get; set; }
	public decimal ApprovedLeaveUnits { get; set; }
	public string MediumStageName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string? Remarks { get; set; }
	public string Status { get; set; } = string.Empty;
}

public class PagedLeaveManagementResultDto
{
	public int TotalCount { get; set; }
	public List<LeaveManagementDto> Items { get; set; } = new();
}

public class LeaveRequestDetailsDto
{
	public Guid Id { get; set; }
	public int PublicId { get; set; }
	public Guid MediumStageId { get; set; }
	public string MediumStageName { get; set; } = string.Empty;
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string SubmittedByName { get; set; } = string.Empty;
	public string? SubmittedByEmployeeCode { get; set; }
	public Guid LeavePolicyId { get; set; }
	public string LeaveType { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public decimal TotalDays { get; set; }
	public bool HalfDay { get; set; }
	public decimal ApprovedLeaveUnits { get; set; }
	public string Description { get; set; } = string.Empty;
	public string? AttachmentUrl { get; set; }
	public string? Remarks { get; set; }
	public string Status { get; set; } = string.Empty;
}

public class LeaveRequestCreateDto
{
	public Guid? ForEmployeeId { get; set; }
	public Guid? LeavePolicyId { get; set; }
	public Guid LeaveTypeId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public bool HalfDay { get; set; }
	public string Reason { get; set; } = string.Empty;

	public Guid ResolveLeavePolicyId()
	{
		if (LeavePolicyId.HasValue && LeavePolicyId.Value != Guid.Empty)
		{
			return LeavePolicyId.Value;
		}

		if (LeaveTypeId != Guid.Empty)
		{
			return LeaveTypeId;
		}

		return Guid.Empty;
	}
}

public class LeaveRequestCreatedDto
{
	public Guid Id { get; set; }
	public int PublicId { get; set; }
	public string Status { get; set; } = string.Empty;
}

public class LeaveRequestDeleteDto
{
	public Guid Id { get; set; }
	public string Message { get; set; } = "Leave request cancelled successfully";
}

public class LeaveApprovalRequestDto
{
	public string? Remarks { get; set; }
}

public class LeaveRejectionRequestDto
{
	public string Remarks { get; set; } = string.Empty;
}

public class LeaveActionResultDto
{
	public Guid Id { get; set; }
	public string Status { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
}

public class LeaveAttachmentDto
{
	public byte[] FileContent { get; set; } = Array.Empty<byte>();
	public string ContentType { get; set; } = "application/octet-stream";
	public string FileName { get; set; } = "attachment";
}
