using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public interface ILeaveManagementAppService
{
	Task<PagedLeaveManagementResultDto> GetListAsync(
		Guid academicYearId,
		DateTime monthYearAnchor,
		string? search,
		int page,
		int pageSize);

	Task<LeaveRequestDetailsDto> GetAsync(Guid leaveRequestId);

	Task<LeaveRequestCreatedDto> CreateAsync(Guid academicYearId, LeaveRequestCreateDto input, object? attachment);

	Task<LeaveActionResultDto> ApproveAsync(Guid leaveRequestId, LeaveApprovalRequestDto? input);

	Task<LeaveActionResultDto> RejectAsync(Guid leaveRequestId, LeaveRejectionRequestDto input);

	Task<LeaveRequestDeleteDto> DeleteAsync(Guid leaveRequestId);

	Task<LeaveAttachmentDto> GetAttachmentAsync(Guid leaveRequestId);
}

public class LeaveManagementDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string LeaveType { get; set; } = string.Empty;
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public int TotalDays { get; set; }
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
	public Guid MediumStageId { get; set; }
	public string MediumStageName { get; set; } = string.Empty;
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
}

public class LeaveRequestCreateDto
{
	public Guid LeaveTypeId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class LeaveRequestCreatedDto
{
	public Guid Id { get; set; }
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
