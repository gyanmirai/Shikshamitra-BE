using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollEsiPolicyCompensationRecalculator(IRepository<SalaryStructureTemplateVersion, Guid> templates,
	IProvidentFundRepository providentFund, IProfessionalTaxRepository professionalTax) : ITransientDependency
{
	public async Task RecalculateAsync(IReadOnlyDictionary<Guid, PayrollCompensationRevisionResolution> compensation,
		IReadOnlyDictionary<Guid, PayrollEsiStatutoryContext> contexts,
		IReadOnlyDictionary<Guid, PayrollStatutoryProfile> profiles, DateTime month)
	{
		var changed = compensation.Values.Where(x => x.Revision.SalaryBasis == SalaryInputBasis.EmployerCost &&
			x.Revision.SourceSalaryTemplateVersionId.HasValue && contexts.ContainsKey(x.Revision.EmployeeId) &&
			HasPolicyVersionChange(x.Revision, contexts[x.Revision.EmployeeId].Rule.Id)).ToList();
		if (changed.Count == 0) return;
		var templateIds = changed.Select(x => x.Revision.SourceSalaryTemplateVersionId!.Value).Distinct().ToArray();
		var sources = (await templates.GetListAsync(x => Enumerable.Contains(templateIds, x.Id))).ToDictionary(x => x.Id);
		var pfIds = changed.Where(x => x.Revision.PfRuleId.HasValue).Select(x => x.Revision.PfRuleId!.Value).Distinct().ToArray();
		var pfRules = (await providentFund.GetListAsync(x => Enumerable.Contains(pfIds, x.Id))).ToDictionary(x => x.Id);
		var monthEnd = month.AddMonths(1).AddDays(-1);
		var tax = await professionalTax.GetListAsync(x => x.IsActive && x.JurisdictionCode == "IN-GJ" &&
			x.EffectiveFrom <= monthEnd && (!x.EffectiveTo.HasValue || x.EffectiveTo >= month));
		foreach (var item in changed)
		{
			var revision = item.Revision;
			if (!sources.TryGetValue(revision.SourceSalaryTemplateVersionId!.Value, out var source))
				throw new Volo.Abp.BusinessException("HR:Payroll:CompensationRevisionSnapshotInvalid");
			var rules = SalaryTemplateAppService.ApplyOverrides(source.GetRules(), JsonSerializer.Deserialize<List<SalaryRuleOverrideDto>>(revision.OverridesJson) ?? []);
			var pf = revision.PfRuleId is { } pfId ? pfRules.GetValueOrDefault(pfId) : null;
			Recalculate(revision, item.Components, rules, contexts[revision.EmployeeId], pf, tax, profiles.GetValueOrDefault(revision.EmployeeId));
		}
	}
	internal static bool HasPolicyVersionChange(EmployeeCompensationRevision revision, Guid currentRuleId)
	{
		var snapshot = JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(revision.CalculationSnapshotJson);
		return snapshot?.EmployeeStateInsurance.RuleId is { } savedRuleId && savedRuleId != currentRuleId;
	}
	internal static void Recalculate(EmployeeCompensationRevision revision, List<EmployeePayrollComponent> components,
		IReadOnlyList<SalaryTemplateRule> rules, PayrollEsiStatutoryContext context, ProvidentFund? pf,
		IReadOnlyList<ProfessionalTax> tax, PayrollStatutoryProfile? profile)
	{
		var catalog = components.Where(x => x.ComponentId.HasValue).GroupBy(x => x.ComponentId!.Value)
			.ToDictionary(x => x.Key, x => new SalaryTemplateComponent(x.Key, x.First().Name));
		foreach (var rule in rules.Where(x => !catalog.ContainsKey(x.ComponentId)))
		{
			var name = rule.FormulaType switch
			{
				SalaryTemplateFormulaType.EmployeeProvidentFund => "Provident Fund (PF)",
				SalaryTemplateFormulaType.ProfessionalTax => "Professional Tax",
				_ => throw new Volo.Abp.BusinessException("HR:Payroll:CompensationRevisionSnapshotInvalid")
			};
			catalog.Add(rule.ComponentId, new SalaryTemplateComponent(rule.ComponentId, name));
		}
		var esi = new SalaryTemplateEsiEvaluationContext(context.PayrollMonth, context.Rule, context.Establishment,
			context.Enrollment, context.ComponentTreatments, context.Overrides, context.DeductionSource.ComponentId ?? context.DeductionSource.Id,
			context.DeductionSource.Name) { CalculationSettings = context.CalculationSettings, PayrollProfile = profile };
		var calculated = SalaryTemplateCalculator.Calculate(SalaryInputBasis.EmployerCost, revision.QuotedMonthlyAmount,
			rules, catalog, profile is { ApplyPf: false } ? null : pf, tax, profile is { ApplyEsic: false } ? null : esi);
		var amounts = calculated.Earnings.Concat(calculated.Deductions.Where(x => !x.IsStatutory)).ToDictionary(x => x.ComponentId, x => x.Amount);
		foreach (var component in components.Where(x => !x.IsTemp && x.ComponentId.HasValue))
			if (amounts.TryGetValue(component.ComponentId!.Value, out var amount)) component.Amount = amount;
	}
}
