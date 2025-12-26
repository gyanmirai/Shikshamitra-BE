using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public interface ILeaveManagementAppService
{
	Task<PagedLeaveManagementResultDto> GetListAsync(Guid mediumStageId, int? month, int? year, int page, int pageSize);
	Task<LeaveRequestDetailsDto> GetAsync(int leaveRequestId);
	Task<LeaveRequestCreatedDto> CreateAsync(LeaveRequestCreateDto input, object attachment);
	Task<LeaveRequestDetailsDto> UpdateAsync(int leaveRequestId, LeaveRequestUpdateRequest input);
	Task<LeaveRequestDeleteDto> DeleteAsync(int leaveRequestId);
}

public class LeaveManagementDto
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string LeaveType { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public int TotalDays { get; set; }
	public string Organization { get; set; } = string.Empty;
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
	public int Id { get; set; }
	public Guid MediumStageId { get; set; }
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public Guid LeaveTypeId { get; set; }
	public string LeaveType { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public int TotalDays { get; set; }
	public string Description { get; set; } = string.Empty;
	public string? AttachmentUrl { get; set; }
	public string? Remarks { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}

public class LeaveRequestCreateDto
{
	public Guid MediumStageId { get; set; }
	public Guid LeaveTypeId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class LeaveRequestCreatedDto
{
	public Guid Id { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTime AppliedAt { get; set; }
}

public class LeaveRequestDeleteDto
{
	public int Id { get; set; }
	public string Message { get; set; } = "Leave request cancelled successfully";
}
