using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public record PayslipCorrectionDto(Guid Id, Guid SourceDocumentId, string Reason, string Status,
	string ConcurrencyStamp, RunPayrollItem? Candidate, Guid? ReviewedBy, DateTime? ReviewedAt,
	Guid? ResultDocumentId, string? SettlementMonth)
{
	public string SourceMonth { get; init; } = string.Empty;
	public List<PayslipCorrectionAttendanceDto> Attendance { get; init; } = [];
	public decimal OriginalEarnings { get; init; }
	public decimal OriginalDeductions { get; init; }
	public decimal OriginalNetPay { get; init; }
	public decimal OriginalEmployerCost { get; init; }
}

public class StartPayslipCorrectionDto
{
	public Guid DocumentId { get; set; }
	public Guid IdempotencyKey { get; set; }
	[Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public class PayslipCorrectionActionDto
{
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class PayslipCorrectionAttendanceDto
{
	public DateOnly Date { get; set; }
	public EmployeeAttendanceStatusType Status { get; set; }
	public DateTimeOffset? TimeIn { get; set; }
	public DateTimeOffset? TimeOut { get; set; }
}

public class CalculatePayslipCorrectionDto : PayslipCorrectionActionDto
{
	[Required] public List<PayslipCorrectionAttendanceDto> Attendance { get; set; } = [];
}

public class SettlePayslipCorrectionDto : PayslipCorrectionActionDto
{
	[Required] public string TargetMonth { get; set; } = string.Empty;
	public Guid PreviewRunId { get; set; }
}

public interface IPayslipCorrectionAppService : IApplicationService
{
	Task<List<PayslipCorrectionDto>> GetAsync(Guid documentId);
	Task<PayslipCorrectionDto> StartAsync(StartPayslipCorrectionDto input);
	Task<PayslipCorrectionDto> CalculateAsync(Guid id, CalculatePayslipCorrectionDto input);
	Task<PayslipCorrectionDto> ReviewAsync(Guid id, PayslipCorrectionActionDto input);
	Task<PayslipCorrectionDto> CancelAsync(Guid id, PayslipCorrectionActionDto input);
	Task<PayslipCorrectionDto> ApplyAsync(Guid id, PayslipCorrectionActionDto input);
	Task<PayslipCorrectionDto> SettleAsync(Guid id, SettlePayslipCorrectionDto input);
}
