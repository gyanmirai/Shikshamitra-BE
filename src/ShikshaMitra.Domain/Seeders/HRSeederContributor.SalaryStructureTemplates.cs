using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Services;
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
			await BackfillSalaryTemplatePfDefaultsAsync();
			return;
		}

		var earnings = await _earningComponentRepository.GetListAsync();
		var deductions = await _deductionComponentRepository.GetListAsync();
		var basic = earnings.Single(x => x.Name == "Basic Salary");
		var specialAllowance = earnings.Single(x => x.Name == "Special Allowance");
		var providentFund = deductions.Single(x => x.Name == "Provident Fund (PF)");
		var professionalTax = deductions.Single(x => x.Name == "Professional Tax");
		var today = _clock.Now.Date;
		var effectivePfRules = (await _providentFundRepository.GetListAsync())
			.Where(x => x.IsActive && x.EffectiveFrom <= today && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= today))
			.ToList();
		var defaultPfRule = effectivePfRules.Count == 1 ? effectivePfRules[0] : null;

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

		// Trusted demo bootstrap does not use a user preview token, but must obey the same allocation invariant.
		var earningRules = rules.Where(x => x.LineKind == SalaryTemplateLineKind.Earning)
			.Select(x => new GrossEarningRule(x.ComponentId, x.SemanticType == PayrollComponentSemanticType.Basic,
				x.FormulaType, x.Value)).ToList();
		GrossEarningAllocation.Calculate(20000.01m, earningRules);

		var version = new SalaryStructureTemplateVersion(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			template.Id,
			1,
			rules,
			SalaryInputBasis.EmployerCost,
			CompensationProtectionPolicy.ProtectEmployerCost,
			defaultPfApplicable: defaultPfRule != null,
			defaultPfRuleId: defaultPfRule?.Id);

		version.SetEsiAssignment("IN_PLATFORM", (await _esiPolicyRepository.GetAsync(x => x.IsDefault)).Id, null);
		version.Publish(new DateTime(2022, 4, 1));
		template.MarkPublished(1);
		template.SetDefault(true);

		await _salaryStructureTemplateRepository.InsertAsync(template, autoSave: false);
		await _salaryStructureTemplateVersionRepository.InsertAsync(version, autoSave: false);
		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task BackfillSalaryTemplatePfDefaultsAsync()
	{
		var today = _clock.Now.Date;
		var effectivePfRules = (await _providentFundRepository.GetListAsync())
			.Where(x => x.IsActive && x.EffectiveFrom <= today && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= today))
			.ToList();
		var versions = await _salaryStructureTemplateVersionRepository.GetListAsync();
		var unresolved = versions
			.Where(x => !x.DefaultPfRuleId.HasValue &&
				x.GetRules().Any(rule => rule.FormulaType == SalaryTemplateFormulaType.EmployeeProvidentFund))
			.ToList();
		if (unresolved.Count == 0)
		{
			return;
		}

		if (effectivePfRules.Count != 1)
		{
			_logger.LogWarning(
				"[SalaryTemplateMigrationException] TenantId={TenantId} unresolvedTemplates={Count} effectivePfRules={RuleCount}. Admin resolution is required.",
				CurrentTenant.Id,
				unresolved.Select(x => x.TemplateId).Distinct().Count(),
				effectivePfRules.Count);
			return;
		}

		foreach (var version in unresolved)
		{
			version.MigratePfDefaults(effectivePfRules[0].Id);
			await _salaryStructureTemplateVersionRepository.UpdateAsync(version, autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}
}
