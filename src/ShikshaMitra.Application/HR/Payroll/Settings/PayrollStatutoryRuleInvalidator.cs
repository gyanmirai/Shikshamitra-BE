using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Compensation;
using Volo.Abp.Domain.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayrollStatutoryRuleInvalidator(
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IRepository<EmployeeCompensationRevision, Guid> compensationRevisionRepository,
	PayrollSnapshotReviewInvalidator snapshotInvalidator) : ITransientDependency
{
	public async Task InvalidateProvidentFundAsync(Guid ruleId)
	{
		var ruleIdText = ruleId.ToString();
		var details = await payrollDetailsRepository.GetListAsync(x => x.PfRule == ruleIdText);
		var revisions = await compensationRevisionRepository.GetListAsync(x =>
			x.Status == CompensationRevisionStatus.Approved && x.PfRuleId == ruleId);
		await snapshotInvalidator.InvalidateUnlockedForEmployeesAsync(details.Select(x => x.EmployeeId).Concat(revisions.Select(x => x.EmployeeId)));
	}

	public async Task InvalidateProfessionalTaxAsync()
	{
		var components = await payrollComponentRepository.GetListAsync(x =>
			x.SemanticType == PayrollComponentSemanticType.ProfessionalTax);
		var revisions = await compensationRevisionRepository.GetListAsync(x =>
			x.Status == CompensationRevisionStatus.Approved);
		// Snapshot storage is jsonb on PostgreSQL; inspect the loaded contract instead of SQL LIKE.
		revisions = revisions.Where(x => x.CalculationSnapshotJson.Contains("PROFESSIONAL_TAX", StringComparison.OrdinalIgnoreCase)).ToList();
		await snapshotInvalidator.InvalidateUnlockedForEmployeesAsync(components.Select(x => x.EmployeeId).Concat(revisions.Select(x => x.EmployeeId)));
	}
}
