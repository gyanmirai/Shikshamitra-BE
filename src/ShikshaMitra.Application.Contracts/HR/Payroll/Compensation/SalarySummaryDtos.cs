using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Compensation;

public class SalarySummaryDto
{
	public string Month { get; set; } = string.Empty;
	public Guid? RevisionId { get; set; }
	public DateTime? SalaryEffectiveMonth { get; set; }
	public decimal GrossSalary { get; set; }
	public decimal? TotalDeductions { get; set; }
	public decimal? NetSalary { get; set; }
	public decimal? EmployerCost { get; set; }
	public decimal? QuotedEmployerCost { get; set; }
	public bool IsComplete { get; set; }
	public List<SalarySummaryLineDto> Earnings { get; set; } = [];
	public List<SalarySummaryLineDto> Deductions { get; set; } = [];
	public List<SalarySummaryLineDto> EmployerContributions { get; set; } = [];
	public List<SalaryStatutorySummaryDto> StatutorySummary { get; set; } = [];
	public List<string> BlockingIssues { get; set; } = [];
}

public record SalarySummaryLineDto(string Code, string Name, decimal Amount);

public class SalaryStatutorySummaryDto
{
	public string Scheme { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
	public bool RegistrationPending { get; set; }
	public decimal? EmployeeAmount { get; set; }
	public decimal? EmployerAmount { get; set; }
	public decimal? ContributionWages { get; set; }
	public decimal? EmployeePercentage { get; set; }
	public decimal? EmployerPercentage { get; set; }
	public Guid? RuleId { get; set; }
	public int? RuleVersion { get; set; }
}

public interface ISalarySummaryAppService : IApplicationService
{
	Task<SalarySummaryDto> GetAsync(Guid employeeId, string month);
}
