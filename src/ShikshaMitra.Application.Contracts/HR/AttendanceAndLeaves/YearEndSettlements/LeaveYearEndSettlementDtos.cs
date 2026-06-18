using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;

public class LeaveYearEndSettlementPreviewRequest
{
	public Guid AcademicYearId { get; set; }
}

public class LeaveYearEndSettlementListRequest
{
	public Guid AcademicYearId { get; set; }
}

public class LeaveYearEndSettlementRunDto
{
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid? TargetAcademicYearId { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTime PreviewedAt { get; set; }
	public DateTime? FinalizedAt { get; set; }
	public int TotalItems { get; set; }
	public int FinalizedItems { get; set; }
	public int BlockedItems { get; set; }
	public decimal TotalCarriedDays { get; set; }
	public decimal TotalEncashedDays { get; set; }
	public decimal TotalForfeitedDays { get; set; }
	public decimal TotalEncashmentAmount { get; set; }
	public List<LeaveYearEndSettlementItemDto> Items { get; set; } = new();
}

public class LeaveYearEndSettlementItemDto
{
	public Guid Id { get; set; }
	public Guid RunId { get; set; }
	public Guid SourceAcademicYearId { get; set; }
	public Guid? TargetAcademicYearId { get; set; }
	public Guid EmployeeId { get; set; }
	public string EmployeeName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public Guid SourceLeavePolicyId { get; set; }
	public Guid SourceLeavePolicyRuleId { get; set; }
	public Guid? TargetLeavePolicyRuleId { get; set; }
	public decimal AccruedDays { get; set; }
	public decimal CarryInDays { get; set; }
	public decimal ApprovedUsedDays { get; set; }
	public decimal PendingDays { get; set; }
	public decimal UnusedDays { get; set; }
	public decimal CarriedDays { get; set; }
	public decimal EncashedDays { get; set; }
	public decimal ForfeitedDays { get; set; }
	public string? PayrollMonth { get; set; }
	public decimal Amount { get; set; }
	public string Status { get; set; } = string.Empty;
	public List<string> IssueCodes { get; set; } = new();
	public Guid? PayrollAdjustmentId { get; set; }
}

public interface ILeaveYearEndSettlementAppService
{
	Task<List<LeaveYearEndSettlementRunDto>> GetListAsync(LeaveYearEndSettlementListRequest input);
	Task<LeaveYearEndSettlementRunDto> GetAsync(Guid runId);
	Task<LeaveYearEndSettlementRunDto> PreviewAsync(LeaveYearEndSettlementPreviewRequest input);
	Task<LeaveYearEndSettlementRunDto> FinalizeAsync(Guid runId);
}
