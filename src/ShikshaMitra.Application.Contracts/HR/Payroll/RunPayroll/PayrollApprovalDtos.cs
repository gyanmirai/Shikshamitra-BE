using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public record PayrollApprovalSettingsDto(string Mode, int Version, Guid? ApproverId, bool AllowSelfFinalization);
public record PayrollApproverDto(Guid Id, string Name);

public class PayrollApprovalSettingsCommand
{
	[Range(0, int.MaxValue)] public int ExpectedVersion { get; set; }
	[Required, StringLength(500)] public string Reason { get; set; } = "";
}

public class ConfigurePayrollApprovalCommand : PayrollApprovalSettingsCommand
{
	public Guid ApproverId { get; set; }
	public bool AllowSelfFinalization { get; set; }
}

public class SubmitPayrollCommand : PayrollPreviewMonthInput
{
	public Guid CommandKey { get; set; }
	public Guid PreviewRunId { get; set; }
	public Guid? PreviousSubmissionId { get; set; }
	public int ExpectedSettingsVersion { get; set; }
	public bool SelfFinalize { get; set; }
	[MinLength(1), MaxLength(1000)] public List<Guid> EmployeeIds { get; set; } = [];
}

public class ResolvePayrollSubmissionCommand
{
	public Guid CommandKey { get; set; }
	[Range(1, int.MaxValue)] public int ExpectedVersion { get; set; }
	[StringLength(500)] public string? Reason { get; set; }
}

public record PayrollSubmissionDto(Guid Id, string Month, string State, int Version, int StaffCount,
	decimal NetPay, Guid SubmitterId, Guid ApproverId, bool SelfFinalization, DateTime SubmittedAt,
	DateTime? ResolvedAt, string? ReasonCode, bool IsStale, string[] AllowedActions);

public record PayrollSubmissionDetailDto(PayrollSubmissionDto Submission, List<RunPayrollItem> Items);

public interface IPayrollApprovalAppService : IApplicationService
{
	Task<PayrollApprovalSettingsDto> GetSettingsAsync();
	Task<List<PayrollApproverDto>> GetApproversAsync(string? search);
	Task<PayrollApprovalSettingsDto> ConfigureAsync(ConfigurePayrollApprovalCommand command);
	Task<PayrollApprovalSettingsDto> ChangeModeAsync(string action, PayrollApprovalSettingsCommand command);
	Task<List<PayrollSubmissionDto>> GetListAsync(PayrollPreviewMonthInput input);
	Task<PayrollSubmissionDetailDto> GetAsync(Guid id);
	Task<PayrollSubmissionDto> SubmitAsync(SubmitPayrollCommand command);
	Task<PayrollSubmissionDto> ResolveAsync(Guid id, string action, ResolvePayrollSubmissionCommand command);
}
