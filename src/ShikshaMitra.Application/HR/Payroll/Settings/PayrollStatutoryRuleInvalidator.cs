using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Templates;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.Settings;

public class PayrollStatutoryRuleInvalidator(
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	PayrollSnapshotReviewInvalidator snapshotInvalidator) : ITransientDependency
{
	public async Task InvalidateProvidentFundAsync(Guid ruleId)
	{
		var ruleIdText = ruleId.ToString();
		var details = await payrollDetailsRepository.GetListAsync(x => x.PfRule == ruleIdText);
		await snapshotInvalidator.InvalidateUnlockedForEmployeesAsync(details.Select(x => x.EmployeeId));
	}

	public async Task InvalidateProfessionalTaxAsync()
	{
		var components = await payrollComponentRepository.GetListAsync(x =>
			x.SemanticType == PayrollComponentSemanticType.ProfessionalTax);
		await snapshotInvalidator.InvalidateUnlockedForEmployeesAsync(components.Select(x => x.EmployeeId));
	}
}
