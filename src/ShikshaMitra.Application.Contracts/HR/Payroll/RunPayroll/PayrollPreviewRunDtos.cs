using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class StartPayrollPreviewRequest
{
	[Required, MaxLength(30)] public string Month { get; set; } = "";
	public Guid? AcademicYearId { get; set; }
	public bool Refresh { get; set; }
}

public class PayrollPreviewStatusDto
{
	public Guid? LastCompletedRunId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid Id { get; set; }
	public string Month { get; set; } = "";
	public string Status { get; set; } = "";
	public int EmployeeCount { get; set; }
	public int CompletedEmployees { get; set; }
	public bool IsCurrent { get; set; }
	public DateTime CreatedAtUtc { get; set; }
	public DateTime? StartedAtUtc { get; set; }
	public DateTime? CompletedAtUtc { get; set; }
	public string? ErrorCode { get; set; }
	public string? ErrorMessage { get; set; }
}

public class PayrollPreviewPageInput
{
	[Range(0, int.MaxValue)] public int Page { get; set; }
	[Range(1, 100)] public int PageSize { get; set; } = 25;
	[MaxLength(100)] public string? Search { get; set; }
}

public interface IPayrollPreviewRunAppService
{
	Task<PayrollPreviewStatusDto> StartAsync(StartPayrollPreviewRequest request);
	Task<PayrollPreviewStatusDto> GetAsync(Guid id);
	Task<RunPayrollResponse> GetEmployeesAsync(Guid id, PayrollPreviewPageInput input);
}

public class PayrollPreviewJobArgs
{
	public Guid? TenantId { get; set; }
	public Guid RunId { get; set; }
}
