using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class PfEligibilityResolver(IRepository<PfCoverageConfiguration, Guid> coverage,
	IRepository<EmployeeCompensationRevision, Guid> revisions,
	IRepository<ShikshaMitra.HR.Payroll.RunPayroll.PayrollRunItemSnapshot, Guid> snapshots,
	IRepository<ShikshaMitra.HR.Payroll.SalarySlips.SalarySlip, Guid> slips, Volo.Abp.Linq.IAsyncQueryableExecuter executer) : ITransientDependency
{
	public async Task<PfEligibilityDecision> ResolveAsync(Guid? employeeId, DateTime month, decimal grossSalary, decimal contributionWages,
		PfEnrollmentDeclaration declaration, bool enrolled)
	{
		var configuration = (await coverage.GetListAsync(x => !x.IsCancelled && x.EffectiveMonth <= month))
			.OrderByDescending(x => x.EffectiveMonth).FirstOrDefault();
		var wasMember = employeeId.HasValue && (await GetMemberHistoryAsync([employeeId.Value], month)).ContainsKey(employeeId.Value);
		return PfEligibilityCalculator.Calculate(grossSalary, contributionWages, configuration?.IsCovered, declaration, wasMember, enrolled);
	}

	public async Task<IReadOnlyDictionary<Guid, bool>> GetMemberHistoryAsync(IReadOnlyCollection<Guid> ids, DateTime month)
	{
		var members = await revisions.GetListAsync(x => ids.Contains(x.EmployeeId) && x.EffectiveMonth <= month &&
			x.PfRuleId != null && (x.Status == CompensationRevisionStatus.Approved || x.Status == CompensationRevisionStatus.Superseded));
		var monthKey = month.ToString("yyyy-MM");
		var locked = await executer.ToListAsync((await snapshots.GetQueryableAsync()).Where(x => ids.Contains(x.EmployeeId) && x.IsLocked &&
			x.ProvidentFundRuleId != null && string.Compare(x.MonthYear, monthKey) < 0).Select(x => new { x.EmployeeId, x.StatutoryApplicationJson }));
		var issued = await executer.ToListAsync((await slips.GetQueryableAsync()).Where(x => ids.Contains(x.EmployeeId) && x.ProvidentFundRuleId != null &&
			string.Compare(x.MonthYear, monthKey) < 0 && (x.Status == ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipStatus.Finalized || x.Status == ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipStatus.Paid))
			.Select(x => new { x.EmployeeId, x.StatutoryApplicationJson }));
		return members.Where(IsEstablishedMember).Select(x => x.EmployeeId).Concat(locked.Concat(issued).Where(x => AppliedMembership(x.StatutoryApplicationJson)).Select(x => x.EmployeeId))
			.Distinct().ToDictionary(x => x, _ => true);
	}
	public async Task<bool?> GetCoverageAsync(DateTime month) => (await coverage.GetListAsync(x => !x.IsCancelled && x.EffectiveMonth <= month))
		.OrderByDescending(x => x.EffectiveMonth).FirstOrDefault()?.IsCovered;
	private static bool AppliedMembership(string? json) => json == null ||
		JsonSerializer.Deserialize<PayrollStatutoryApplication>(json)?.ProvidentFund is { Apply: true, Eligible: true };
	private static bool IsEstablishedMember(EmployeeCompensationRevision revision)
	{
		var calculation = JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(revision.CalculationSnapshotJson);
		return calculation?.PfEnrollment.Membership == "EXISTING_MEMBER" || calculation?.StatutoryApplication == null ||
			calculation.StatutoryApplication.ProvidentFund is { Apply: true, Eligible: true };
	}
	public static PfEnrollmentDeclaration Declaration(EmployeeCompensationRevision? revision)
	{
		if (revision == null) return new();
		try { return JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(revision.CalculationSnapshotJson)?.PfEnrollment ?? new(); }
		catch (JsonException) { return new(); }
	}
}