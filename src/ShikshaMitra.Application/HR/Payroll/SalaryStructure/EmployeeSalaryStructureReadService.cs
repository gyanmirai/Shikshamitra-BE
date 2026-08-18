using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class EmployeeSalaryStructureReadService(
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IEmployeePayrollDetailsRepository payrollDetailsRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<SalaryStructureResponseDto> GetAsync(Guid employeeId)
	{
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);
		var details = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);

		return new SalaryStructureResponseDto
		{
			EarningComponents = ObjectMapper.Map<List<EmployeePayrollComponent>, List<SalaryComponentDto>>(earnings.ToList()),
			DeductionComponents = ObjectMapper.Map<List<EmployeePayrollComponent>, List<SalaryComponentDto>>(deductions.ToList()),
			PfRuleId = Guid.TryParse(details?.PfRule, out var pfRuleId) ? pfRuleId : null,
			QuotedSalaryBasis = details?.QuotedSalaryBasis,
			QuotedMonthlyAmount = details?.QuotedMonthlyAmount,
			SourceSalaryTemplateId = details?.SourceSalaryTemplateId,
			SourceSalaryTemplateVersionId = details?.SourceSalaryTemplateVersionId,
			SourceSalaryTemplateVersion = details?.SourceSalaryTemplateVersion,
			SalaryGeneratedAt = details?.SalaryGeneratedAt,
			WasModifiedAfterGeneration = details?.WasModifiedAfterGeneration ?? false
		};
	}
}
