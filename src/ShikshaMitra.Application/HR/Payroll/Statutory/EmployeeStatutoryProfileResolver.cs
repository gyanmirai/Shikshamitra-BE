using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EmployeeStatutoryProfileResolver(IRepository<EmployeeStatutoryProfileVersion, Guid> versions,
	IRepository<EmployeePayrollDetails, Guid> details, IRepository<EmployeeStatutoryEnrollment, Guid> enrollments,
	IRepository<EmployeeCompensationRevision, Guid> compensation, ICurrentTenant tenant) : ITransientDependency
{
	public async Task<Dictionary<Guid, PayrollStatutoryProfile>> ResolveAsync(DateTime month, IReadOnlyCollection<Guid> employeeIds)
	{
		month = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var ids = employeeIds.ToArray();
		var rows = await versions.GetListAsync(x => x.TenantId == tenant.Id && Enumerable.Contains(ids, x.EmployeeId) && !x.IsSuperseded && x.EffectiveMonth <= month);
		var result = rows.GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.EffectiveMonth).First().ToProfile());
		var missing = ids.Where(x => !result.ContainsKey(x)).ToArray();
		if (missing.Length == 0) return result;
		// Read legacy facts until the migration backfill, without inventing registration numbers.
		var legacyDetails = (await details.GetListAsync(x => Enumerable.Contains(missing, x.EmployeeId))).ToDictionary(x => x.EmployeeId);
		var legacyEnrollment = (await enrollments.GetListAsync(x => x.TenantId == tenant.Id && x.SchemeCode == "ESI" && Enumerable.Contains(missing, x.EmployeeId))).ToDictionary(x => x.EmployeeId);
		var revisions = (await compensation.GetListAsync(x => x.TenantId == tenant.Id && Enumerable.Contains(missing, x.EmployeeId) &&
			x.EffectiveMonth <= month && x.Status == CompensationRevisionStatus.Approved)).GroupBy(x => x.EmployeeId)
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.EffectiveMonth).ThenByDescending(y => y.RevisionNumber).First());
		foreach (var id in missing)
		{
			var bank = legacyDetails.GetValueOrDefault(id);
			var revision = revisions.GetValueOrDefault(id);
			var applyPf = revision == null || revision.PfRuleId.HasValue;
			result[id] = new PayrollStatutoryProfile
			{
				ApplyPf = applyPf, PfNumber = EmployeeStatutoryProfileVersion.NormalizeNumber(bank?.EpfNumber),
				UanNumber = bank?.UanNumber, PfDisabledReason = applyPf ? null : "Existing salary setup has PF turned off.",
				ApplyEsic = true, EsicInsuranceNumber = EmployeeStatutoryProfileVersion.NormalizeNumber(legacyEnrollment.GetValueOrDefault(id)?.InsuranceNumber)
			};
		}
		return result;
	}
	public async Task<PayrollStatutoryProfile> ResolveAsync(Guid employeeId, DateTime month) =>
		(await ResolveAsync(month, [employeeId]))[employeeId];
}
