using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using H = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR;

namespace ShikshaMitra.HR.Setup;

/// <summary>Configuration-only checks. Never invoke calculation resolvers that initialize defaults.</summary>
public class HrSetupPayrollReadinessReader(
	IRepository<EarningComponent, Guid> earnings, IRepository<DeductionComponent, Guid> deductions,
	IRepository<SalaryStructureTemplate, Guid> templates, IRepository<SalaryStructureTemplateVersion, Guid> versions,
	IRepository<ProvidentFund, Guid> pf, IRepository<ProfessionalTax, Guid> professionalTax,
	IRepository<EsiPayrollConfiguration, Guid> esiConfigurations, EsiPolicyResolver esiPolicies,
	IPermissionChecker permissions) : ITransientDependency
{
	public async Task<List<HrSetupCheckDto>> ReadAsync(Guid tenantId, DateTime now)
	{
		var rows = await earnings.GetListAsync(x => x.TenantId == tenantId);
		var byId = rows.ToDictionary(x => x.Id);
		var active = rows.Where(x => x.IsActive).ToList();
		var valid = active.Where(x => EarningUsable(x, byId)).Select(x => x.Id).ToHashSet();
		var result = new List<HrSetupCheckDto>
		{
			new() { Code = "earnings", Step = "earnings", State = rows.Count == 0 ? "missing" : active.Count == 0 ? "inactive" : valid.Count != active.Count ? "invalid" : "ready",
				Reason = valid.Count > 0 && valid.Count == active.Count ? "Active earning components are usable." : "Create usable earning components and resolve invalid percentage dependencies.",
				CanConfigure = await permissions.IsGrantedAsync(H.PayrollManagement_SettingsEarningComponents.View) && await permissions.IsGrantedAsync(H.PayrollManagement_SettingsEarningComponents.Add) }
		};
		var templateRows = await templates.GetListAsync(x => x.TenantId == tenantId);
		var versionRows = await versions.GetListAsync(x => x.TenantId == tenantId);
		var deductionIds = (await deductions.GetListAsync(x => x.TenantId == tenantId && x.IsActive)).Select(x => x.Id).ToHashSet();
		var month = new DateTime(now.Year, now.Month, 1);
		var monthEnd = month.AddMonths(1).AddDays(-1);
		var pfRules = await pf.GetListAsync(x => x.TenantId == tenantId && x.IsActive && x.EffectiveFrom <= monthEnd && (!x.EffectiveTo.HasValue || x.EffectiveTo >= month));
		var taxRules = await professionalTax.GetListAsync(x => x.TenantId == tenantId && x.IsActive && x.EffectiveFrom <= monthEnd && (!x.EffectiveTo.HasValue || x.EffectiveTo >= month));
		var esiRules = await esiPolicies.ResolveAllAsync(month);
		var configurations = await esiConfigurations.GetListAsync(x => x.TenantId == tenantId && !x.IsCancelled && x.EffectiveMonth <= month);
		var dependencies = new HashSet<string>();
		var usable = false;
		foreach (var template in templateRows.Where(x => x.Status == SalaryTemplateStatus.Published))
		{
			var version = versionRows.SingleOrDefault(x => x.TemplateId == template.Id && x.VersionNumber == template.PublishedVersion && x.Status == SalaryTemplateStatus.Published);
			if (version == null) continue;
			try
			{
				var rules = version.GetRules();
				SalaryTemplateCalculator.ValidateRules(rules);
				var missing = new HashSet<string>();
				if (rules.Any(x => x.LineKind == SalaryTemplateLineKind.Earning && !valid.Contains(x.ComponentId))) missing.Add("earnings");
				if (rules.Any(x => x.LineKind == SalaryTemplateLineKind.Deduction && !deductionIds.Contains(x.ComponentId))) missing.Add("deductions");
				if ((version.DefaultPfApplicable || rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.EmployeeProvidentFund)) &&
					(!version.DefaultPfRuleId.HasValue || !pfRules.Any(x => x.Id == version.DefaultPfRuleId))) missing.Add("pf");
				if (rules.Any(x => x.FormulaType == SalaryTemplateFormulaType.ProfessionalTax) && !taxRules.Any(x => x.JurisdictionCode == "IN-GJ")) missing.Add("professional-tax");
				if (version.DefaultEsiManagementMode == EsiManagement.InPlatform &&
					(!version.DefaultEsiPolicyId.HasValue || !esiRules.TryGetValue(version.DefaultEsiPolicyId.Value, out var esi) || esi.Policy.IsArchived || configurations.Count == 0)) missing.Add("esi");
				dependencies.UnionWith(missing);
				if (missing.Count == 0 && !SalaryTemplateAppService.RequiresSetup(version)) { usable = true; break; }
			}
			catch (BusinessException) { /* Persisted invalid rules require template repair. */ }
			catch (JsonException) { /* Persisted malformed rules must not be reported as usable. */ }
			catch (ArgumentException) { /* Persisted invalid fields require template repair. */ }
		}
		result.Add(new() { Code = "salary-template", Step = "salary", State = usable ? "ready" : templateRows.Count == 0 ? "missing" : templateRows.All(x => x.Status == SalaryTemplateStatus.Archived) ? "inactive" : "invalid",
			Reason = usable ? "An active salary template and its required dependencies are usable." : "Create or repair an active salary template, preview it, and resolve its required dependencies.",
			CanConfigure = await permissions.IsGrantedAsync(H.PayrollManagement_SettingsSalaryTemplates.View) && (await permissions.IsGrantedAsync(H.PayrollManagement_SettingsSalaryTemplates.Add) || await permissions.IsGrantedAsync(H.PayrollManagement_SettingsSalaryTemplates.Edit)) });
		if (!usable)
			foreach (var dependency in dependencies.Where(x => x != "earnings").OrderBy(x => x))
				result.Add(new() { Code = dependency, Step = "salary", State = "missing", Reason = $"Resolve {dependency} configuration required by the salary template, then preview and save the template again.",
					CanConfigure = await CanConfigureDependencyAsync(dependency) });
		return result;
	}

	private async Task<bool> CanConfigureDependencyAsync(string dependency) => dependency switch
	{
		"deductions" => await permissions.IsGrantedAsync(H.PayrollManagement_SettingsDeductionComponents.View) &&
			await permissions.IsGrantedAsync(H.PayrollManagement_SettingsDeductionComponents.Add),
		"pf" => await permissions.IsGrantedAsync(H.PayrollManagement_SettingsPfRule.Add),
		"professional-tax" => await permissions.IsGrantedAsync(H.PayrollManagement_SettingsProfessionalTax.Add),
		"esi" => await permissions.IsGrantedAsync(H.PayrollManagement_SettingsStatutorySchemes.View) &&
			await permissions.IsGrantedAsync(H.PayrollManagement_SettingsStatutorySchemes.Edit) &&
			await permissions.IsGrantedAsync(H.PayrollManagement_SettingsStatutorySchemes.Activate) &&
			await permissions.IsGrantedAsync(H.PayrollManagement_SettingsStatutorySchemes.ConfigureCalculation),
		_ => false
	};

	private static bool EarningUsable(EarningComponent x, IReadOnlyDictionary<Guid, EarningComponent> rows)
	{
		try
		{
			_ = new EarningComponent(x.Id, x.TenantId, x.Name, x.Description, x.ComponentType, x.CalculationType, x.Amount,
				x.Percentage, x.SelectedEarningComponentId, x.DependOn, x.VisibleInPayslip, x.DisplayOrder);
			EarningComponentManager.ValidateDependency(x.Id, x.CalculationType, x.SelectedEarningComponentId, x.DependOn, rows);
			return true;
		}
		catch (BusinessException) { return false; }
		catch (ArgumentException) { return false; }
	}
}
