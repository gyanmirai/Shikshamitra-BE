using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Statutory;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class SaveEmployeeStatutoryProfileDto
{
	[Volo.Abp.Auditing.DisableAuditing, JsonRequired]
	public PayrollStatutoryProfile Profile { get; set; } = new();
	public string? EffectiveMonth { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public Guid IdempotencyKey { get; set; }
	public bool UpdatePf { get; set; } = true;
	public bool UpdateEsic { get; set; } = true;
}
public class EmployeeStatutoryProfileDto
{
	public Guid EmployeeId { get; set; }
	public PayrollStatutoryProfile Profile { get; set; } = new();
	public string EffectiveMonth { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
	public List<string> Warnings { get; set; } = [];
	public List<EmployeeStatutoryProfileHistoryDto> History { get; set; } = [];
	public bool CanViewPf { get; set; }
	public bool CanViewEsic { get; set; }
	public bool CanManagePf { get; set; }
	public bool CanManageEsic { get; set; }
}
public record EmployeeStatutoryProfileHistoryDto(Guid Id, string EffectiveMonth, bool ApplyPf, bool ApplyEsic,
	bool PfNumberPresent, bool EsicNumberPresent, string? PfDisabledReason, string? EsicDisabledReason, DateTime CreatedAt);
public interface IEmployeeStatutoryProfileAppService : IApplicationService
{
	Task<PayrollRegistrationReadinessDto> GetReadinessAsync(string? month = null, int page = 1, int pageSize = 25, string? search = null);
	Task<EmployeeStatutoryProfileDto> GetAsync(Guid employeeId, string? month = null);
	Task<EmployeeStatutoryProfileDto> SaveAsync(Guid employeeId, SaveEmployeeStatutoryProfileDto input);
}

public class PayrollRegistrationReadinessDto
{
	public int TotalCount { get; set; }
	public List<PayrollRegistrationReadinessRowDto> Items { get; set; } = [];
}
public record PayrollRegistrationReadinessRowDto(Guid EmployeeId, string EmployeeName, string? EmployeeCode,
	bool ApplyPf, bool PfNumberPresent, bool ApplyEsic, bool EsicNumberPresent, string EffectiveMonth);
