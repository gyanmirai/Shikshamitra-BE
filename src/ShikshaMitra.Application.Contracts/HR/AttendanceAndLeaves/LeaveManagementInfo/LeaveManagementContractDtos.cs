using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public class LeaveManagementItemResponse
{
	[Required]
	public Guid Id { get; set; }
	[Required]
	public int PublicId { get; set; }
	[Required]
	public Guid EmployeeId { get; set; }
	[Required]
	public string Name { get; set; } = string.Empty;
	[Required]
	public string EmployeeIdentifier { get; set; } = string.Empty;
	[Required]
	public string SubmittedByName { get; set; } = string.Empty;
	public string? SubmittedByEmployeeCode { get; set; }
	[Required]
	public string LeaveType { get; set; } = string.Empty;
	[Required]
	public DateTime StartDate { get; set; }
	[Required]
	public DateTime EndDate { get; set; }
	[Required]
	public decimal TotalDays { get; set; }
	public bool HalfDay { get; set; }
	public decimal ApprovedLeaveUnits { get; set; }
	[Required]
	public string MediumStageName { get; set; } = string.Empty;
	[Required]
	public string Description { get; set; } = string.Empty;
	public string? Remarks { get; set; }
	[Required]
	public string Status { get; set; } = string.Empty;
}

public class LeaveManagementListResponse
{
	[Required]
	public int TotalCount { get; set; }
	[Required]
	public int CurrentPage { get; set; }
	[Required]
	public int PageSize { get; set; }
	[Required]
	public int TotalPages { get; set; }
	[Required]
	public List<LeaveManagementItemResponse> Items { get; set; } = new();
}

public class LeaveRequestCreatedResponse
{
	[Required]
	public Guid Id { get; set; }
	[Required]
	public int PublicId { get; set; }
	[Required]
	public string Status { get; set; } = string.Empty;
}

public class LeaveRequestDetailsResponse
{
	[Required]
	public Guid Id { get; set; }
	[Required]
	public int PublicId { get; set; }
	[Required]
	public Guid MediumStageId { get; set; }
	[Required]
	public string MediumStageName { get; set; } = string.Empty;
	[Required]
	public Guid EmployeeId { get; set; }
	[Required]
	public string EmployeeName { get; set; } = string.Empty;
	[Required]
	public string EmployeeCode { get; set; } = string.Empty;
	[Required]
	public string SubmittedByName { get; set; } = string.Empty;
	public string? SubmittedByEmployeeCode { get; set; }
	[Required]
	public Guid LeavePolicyId { get; set; }
	/// <summary>Same value as <see cref="LeavePolicyId"/> (legacy JSON name).</summary>
	[Required]
	public Guid LeaveTypeId { get; set; }
	[Required]
	public string LeaveType { get; set; } = string.Empty;
	[Required]
	public DateTime StartDate { get; set; }
	[Required]
	public DateTime EndDate { get; set; }
	[Required]
	public decimal TotalDays { get; set; }
	public bool HalfDay { get; set; }
	public decimal ApprovedLeaveUnits { get; set; }
	[Required]
	public string Description { get; set; } = string.Empty;
	public string? AttachmentUrl { get; set; }
	public string? Remarks { get; set; }
	[Required]
	public string Status { get; set; } = string.Empty;
}

public class LeaveRequestDeleteResponse
{
	[Required]
	public Guid Id { get; set; }
	[Required]
	public string Message { get; set; } = "Leave request cancelled successfully";
}

public class LeaveActionResponse
{
	[Required]
	public Guid Id { get; set; }
	[Required]
	public string Status { get; set; } = string.Empty;
	[Required]
	public string Message { get; set; } = string.Empty;
}
