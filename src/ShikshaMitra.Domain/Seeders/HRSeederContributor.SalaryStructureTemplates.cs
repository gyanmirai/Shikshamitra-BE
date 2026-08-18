using ShikshaMitra.HR.Payroll.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	internal async Task SeedSalaryStructureTemplatesAsync()
	{
		const string templateName = "Gujarat Standard Salary";
		if (await _salaryStructureTemplateRepository.FindAsync(x => x.Name == templateName) is not null)
		{
			return;
		}

		var earnings = await _earningComponentRepository.GetListAsync();
		var deductions = await _deductionComponentRepository.GetListAsync();
		var basic = earnings.Single(x => x.Name == "Basic Salary");
		var specialAllowance = earnings.Single(x => x.Name == "Special Allowance");
		var providentFund = deductions.Single(x => x.Name == "Provident Fund (PF)");
		var professionalTax = deductions.Single(x => x.Name == "Professional Tax");

		var template = new SalaryStructureTemplate(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			templateName,
			"Basic at 60%, Special Allowance as the balance, standard PF, and Gujarat professional tax.");

		var rules = new List<SalaryTemplateRule>
		{
			new("BASIC", basic.Id, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.PercentageOfGross, PayrollComponentSemanticType.Basic, 60m, null, 1),
			new("SPECIAL_ALLOWANCE", specialAllowance.Id, SalaryTemplateLineKind.Earning, SalaryTemplateFormulaType.RemainderOfGross, PayrollComponentSemanticType.BalancingEarning, null, null, 2),
			new("EMPLOYEE_PF", providentFund.Id, SalaryTemplateLineKind.Deduction, SalaryTemplateFormulaType.EmployeeProvidentFund, PayrollComponentSemanticType.EmployeeProvidentFund, null, "BASIC", 10),
			new("PROFESSIONAL_TAX", professionalTax.Id, SalaryTemplateLineKind.Deduction, SalaryTemplateFormulaType.ProfessionalTax, PayrollComponentSemanticType.ProfessionalTax, null, null, 11)
		};

		var version = new SalaryStructureTemplateVersion(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			template.Id,
			1,
			rules);

		version.Publish(new DateTime(2022, 4, 1));
		template.MarkPublished(1);
		template.SetDefault(true);

		await _salaryStructureTemplateRepository.InsertAsync(template, autoSave: false);
		await _salaryStructureTemplateVersionRepository.InsertAsync(version, autoSave: false);
		await CurrentUnitOfWork.SaveChangesAsync();
	}
}
