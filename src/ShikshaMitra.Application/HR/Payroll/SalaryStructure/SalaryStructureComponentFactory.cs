using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

public class SalaryStructureComponentFactory(IGuidGenerator guidGenerator) : ITransientDependency
{
	public EmployeePayrollComponent BuildEarningComponent(
		Guid employeeId,
		ResolvedSalaryComponent<EarningComponent> resolved,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId)
	{
		var earning = resolved.Line;
		var master = resolved.Master;
		var existingComponent = resolved.Existing;
		var componentType = earning.UsesMasterValue == false
			? SalaryComponentType.Fixed
			: master is not null
			? SalaryStructureComponentRules.MapCalculationType(master.CalculationType)
			: existingComponent?.ComponentType ?? SalaryComponentType.Fixed;
		var dependOn = earning.UsesMasterValue == false
			? SalaryComponentDependency.None
			: master is not null
			? PayrollComponentImpactProjection.MapDependency(master.DependOn)
			: existingComponent?.DependOn;
		var description = master?.Description ?? existingComponent?.Description;
		var amount = master?.Id is Guid masterId && earningAmountsByComponentId.TryGetValue(masterId, out var resolvedAmount)
			? resolvedAmount
			: SalaryStructureComponentRules.ResolveAmountComponentFallback(earning, master?.Amount);

		return new EmployeePayrollComponent(
			existingComponent?.Id ?? guidGenerator.Create(),
			employeeId,
			earning.Name,
			master?.Id,
			description,
			amount,
			earning.UsesMasterValue == false || earning.IsFixed,
			earning.IsTemp,
			earning.ApplicableMonth ?? existingComponent?.ApplicableMonth,
			componentType,
			dependOn,
			isEarning: true,
			usesMasterValue: SalaryStructureComponentRules.ShouldUseMasterValue(earning, master),
			semanticType: SalaryStructureComponentRules.ParseSemanticType(earning.SemanticType),
			statutoryRuleId: earning.StatutoryRuleId);
	}

	public EmployeePayrollComponent BuildDeductionComponent(
		Guid employeeId,
		ResolvedSalaryComponent<DeductionComponent> resolved,
		IReadOnlyDictionary<Guid, decimal> deductionAmountsByComponentId)
	{
		var deduction = resolved.Line;
		var master = resolved.Master;
		var existingComponent = resolved.Existing;
		var componentType = deduction.UsesMasterValue == false
			? SalaryComponentType.Fixed
			: master is not null
			? SalaryStructureComponentRules.MapCalculationType(master.CalculationType)
			: existingComponent?.ComponentType ?? SalaryComponentType.Fixed;
		var dependOn = deduction.UsesMasterValue == false
			? SalaryComponentDependency.None
			: master is not null
			? PayrollComponentImpactProjection.MapDependency(master.DependOn)
			: existingComponent?.DependOn;
		var description = master?.Description ?? existingComponent?.Description;
		var amount = master?.Id is Guid masterId && deductionAmountsByComponentId.TryGetValue(masterId, out var resolvedAmount)
			? resolvedAmount
			: SalaryStructureComponentRules.ResolveAmountComponentFallback(deduction, master?.Amount);

		return new EmployeePayrollComponent(
			existingComponent?.Id ?? guidGenerator.Create(),
			employeeId,
			deduction.Name,
			master?.Id,
			description,
			amount,
			deduction.UsesMasterValue == false || deduction.IsFixed,
			deduction.IsTemp,
			deduction.ApplicableMonth ?? existingComponent?.ApplicableMonth,
			componentType,
			dependOn,
			isEarning: false,
			usesMasterValue: SalaryStructureComponentRules.ShouldUseMasterValue(deduction, master),
			semanticType: SalaryStructureComponentRules.ParseSemanticType(deduction.SemanticType),
			statutoryRuleId: deduction.StatutoryRuleId);
	}
}
