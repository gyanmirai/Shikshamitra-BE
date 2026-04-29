using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmployeeLeaveStatusType = ShikshaMitra.HR.AttendanceAndLeaves.EmployeeLeaveStatusType;

namespace ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;

public interface ILeaveManagementAppService
{
	Task<PagedLeaveManagementResultDto> GetListAsync(LeaveManagementGetListInput input);
	Task<PagedLeaveManagementResultDto> GetMyListAsync(LeaveManagementGetListInput input);

	Task<LeaveRequestDetailsDto> GetAsync(Guid leaveRequestId);
	Task<LeaveRequestDetailsDto> GetMyAsync(Guid leaveRequestId);

	Task<LeaveRequestCreatedDto> CreateAsync(Guid academicYearId, LeaveRequestCreateDto input);
	Task<LeaveRequestCreatedDto> CreateMyAsync(Guid academicYearId, LeaveRequestCreateDto input);
	Task<LeaveRequestCreatedDto> CreateMyAsync(Guid academicYearId, LeaveRequestCreateDto input, object? attachment);

	Task<LeaveRequestCreatedDto> CreateAsync(Guid academicYearId, LeaveRequestCreateDto input, object? attachment);

	Task<LeaveActionResultDto> ApproveAsync(Guid leaveRequestId, LeaveApprovalRequestDto? input);

	Task<LeaveActionResultDto> RejectAsync(Guid leaveRequestId, LeaveRejectionRequestDto input);

	Task<LeaveRequestDeleteDto> DeleteAsync(Guid leaveRequestId);
	Task<LeaveRequestDeleteDto> DeleteMyAsync(Guid leaveRequestId);

	Task<LeaveAttachmentDto> GetAttachmentAsync(Guid leaveRequestId);
	Task<LeaveAttachmentDto> GetMyAttachmentAsync(Guid leaveRequestId);

	Task<LeaveBalanceListDto> GetBalancesAsync(Guid academicYearId, Guid? forEmployeeId);
	Task<LeaveBalanceListDto> GetMyBalancesAsync(Guid academicYearId);

	Task<LeaveManagementCalendarResultDto> GetCalendarAsync(LeaveManagementCalendarInput input);

	Task<byte[]> ExportToExcelAsync(LeaveManagementGetListInput input);
}

/// <summary>
/// List/query parameters for leave management grid and export.
/// </summary>
public class LeaveManagementGetListInput
{
	public Guid AcademicYearId { get; set; }
	/// <summary>When set, list is scoped to this calendar month intersected with the academic year. When null, the full academic year span is used.</summary>
	public DateTime? MonthYearAnchor { get; set; }
	public string? Search { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	/// <summary>When true, allows larger page sizes for batched export (internal use).</summary>
	public bool UseExportBatching { get; set; }
	public EmployeeLeaveStatusType? Status { get; set; }
	/// <summary>Leave policy aggregate id (FK on leave request).</summary>
	public Guid? LeavePolicyId { get; set; }
	/// <summary>Leave type master id (from leave policy's leave type).</summary>
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
	public int TotalDays { get; set; }
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
	public int UsedDays { get; set; }
	public int RemainingDays { get; set; }
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
	public int PublicId { get; set; }
	public Guid MediumStageId { get; set; }
	public string MediumStageName { get; set; } = string.Empty;
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string SubmittedByName { get; set; } = string.Empty;
	public string? SubmittedByEmployeeCode { get; set; }
	/// <summary>Leave policy id (legacy name: was exposed as leave type id).</summary>
	public Guid LeavePolicyId { get; set; }
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
	/// <summary>Optional. When set, leave is created for this employee (manager/HR on-behalf).</summary>
	public Guid? ForEmployeeId { get; set; }

	/// <summary>Preferred: leave policy aggregate id.</summary>
	public Guid? LeavePolicyId { get; set; }

	/// <summary>Legacy alias for <see cref="LeavePolicyId"/> (also the policy primary key).</summary>
	public Guid LeaveTypeId { get; set; }

	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
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
