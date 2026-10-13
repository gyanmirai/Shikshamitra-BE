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
	IRepository<EmployeeCompensationRevision, Guid> revisions) : ITransientDependency
{
	public async Task<PfEligibilityDecision> ResolveAsync(Guid? employeeId, DateTime month, decimal grossSalary, decimal contributionWages,
		PfEnrollmentDeclaration declaration, bool enrolled)
	{
		var configuration = (await coverage.GetListAsync(x => !x.IsCancelled && x.EffectiveMonth <= month))
			.OrderByDescending(x => x.EffectiveMonth).FirstOrDefault();
		var wasMember = employeeId.HasValue && await revisions.AnyAsync(x => x.EmployeeId == employeeId &&
			x.EffectiveMonth <= month && x.PfRuleId != null &&
			(x.Status == CompensationRevisionStatus.Approved || x.Status == CompensationRevisionStatus.Superseded));
		return PfEligibilityCalculator.Calculate(grossSalary, contributionWages, configuration?.IsCovered, declaration, wasMember, enrolled);
	}

	public async Task<IReadOnlyDictionary<Guid, bool>> GetMemberHistoryAsync(IReadOnlyCollection<Guid> ids, DateTime month)
	{
		var members = await revisions.GetListAsync(x => ids.Contains(x.EmployeeId) && x.EffectiveMonth <= month &&
			x.PfRuleId != null && (x.Status == CompensationRevisionStatus.Approved || x.Status == CompensationRevisionStatus.Superseded));
		return members.Select(x => x.EmployeeId).Distinct().ToDictionary(x => x, _ => true);
	}
	public async Task<bool?> GetCoverageAsync(DateTime month) => (await coverage.GetListAsync(x => !x.IsCancelled && x.EffectiveMonth <= month))
		.OrderByDescending(x => x.EffectiveMonth).FirstOrDefault()?.IsCovered;
	public static PfEnrollmentDeclaration Declaration(EmployeeCompensationRevision? revision)
	{
		if (revision == null) return new();
		try { return JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(revision.CalculationSnapshotJson)?.PfEnrollment ?? new(); }
		catch (JsonException) { return new(); }
	}
}