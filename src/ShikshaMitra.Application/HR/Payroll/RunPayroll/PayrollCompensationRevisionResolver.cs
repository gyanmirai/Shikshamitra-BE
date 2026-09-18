using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public sealed record PayrollCompensationRevisionResolution(
	EmployeeCompensationRevision Revision,
	List<EmployeePayrollComponent> Components);

public class PayrollCompensationRevisionResolver(
	IRepository<EmployeeCompensationRevision, Guid> revisionRepository,
	IRepository<EmployeeCompensationRevisionLine, Guid> lineRepository)
	: ITransientDependency
{
	public async Task<IReadOnlyDictionary<Guid, PayrollCompensationRevisionResolution>> ResolveAsync(
		IReadOnlyCollection<Guid> employeeIds,
		int year,
		int month,
		ILookup<Guid, EmployeePayrollComponent> existingComponentsByEmployee)
	{
		var effectiveMonth = DateTime.SpecifyKind(new DateTime(year, month, 1), DateTimeKind.Utc);
		var employeeIdSet = employeeIds.Distinct().ToHashSet();
		var revisions = await revisionRepository.GetListAsync(x =>
			employeeIdSet.Contains(x.EmployeeId) &&
			x.Status == CompensationRevisionStatus.Approved &&
			x.EffectiveMonth <= effectiveMonth);
		var effectiveByEmployee = revisions
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => x.OrderByDescending(item => item.EffectiveMonth)
					.ThenByDescending(item => item.RevisionNumber)
					.First());
		if (effectiveByEmployee.Count == 0)
		{
			return new Dictionary<Guid, PayrollCompensationRevisionResolution>();
		}

		var revisionIds = effectiveByEmployee.Values.Select(x => x.Id).ToHashSet();
		var lines = await lineRepository.GetListAsync(x => revisionIds.Contains(x.RevisionId));
		var linesByRevision = lines.ToLookup(x => x.RevisionId);
		var result = new Dictionary<Guid, PayrollCompensationRevisionResolution>();
		foreach (var (employeeId, revision) in effectiveByEmployee)
		{
			var components = linesByRevision[revision.Id]
				.OrderBy(x => x.DisplayOrder)
				.Select(x => new EmployeePayrollComponent(
					x.Id,
					employeeId,
					x.Name,
					x.ComponentId,
					x.Description,
					x.Amount,
					isFixed: true,
					isTemp: false,
					applicableMonth: null,
					x.ComponentType,
					x.DependOn,
					x.IsEarning,
					usesMasterValue: false,
					x.SemanticType))
				.ToList();
			ApplySnapshotFormulas(revision, components);
			components.AddRange(existingComponentsByEmployee[employeeId]
				.Where(x => x.IsTemp && PayrollComponentMonthScope.AppliesToMonth(x, year, month)));
			AddStatutorySourceComponents(revision, components);
			result[employeeId] = new PayrollCompensationRevisionResolution(revision, components);
		}

		return result;
	}

	internal static void ApplySnapshotFormulas(EmployeeCompensationRevision revision, List<EmployeePayrollComponent> components)
	{
		if (components.Where(x => x.IsEarning).Sum(x => x.Amount) != revision.GrossSalary)
			throw new BusinessException(HRErrorCodes.SalaryTemplateAllocationMismatch).WithData("revisionId", revision.Id);
		SalaryTemplateCalculationDto snapshot;
		try
		{
			snapshot = JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(revision.CalculationSnapshotJson)
				?? throw new JsonException("Compensation snapshot is empty.");
		}
		catch (JsonException ex)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionSnapshotInvalid", innerException: ex);
		}
		var lines = snapshot.Earnings.Concat(snapshot.Deductions).ToDictionary(x => x.ComponentId);
		foreach (var component in components)
		{
			if (!component.ComponentId.HasValue || !lines.TryGetValue(component.ComponentId.Value, out var line) || line.IsStatutory) continue;
			if (!Enum.TryParse<SalaryTemplateFormulaType>(line.FormulaType.Replace("_", string.Empty), true, out var type))
				throw new BusinessException("HR:Payroll:CompensationRevisionSnapshotInvalid");
			component.Formula = new PayrollComponentFormula(type, line.FormulaValue, line.BasisComponentId);
		}
	}

	private static void AddStatutorySourceComponents(
		EmployeeCompensationRevision revision,
		ICollection<EmployeePayrollComponent> components)
	{
		SalaryTemplateCalculationDto calculation;
		try
		{
			calculation = JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(revision.CalculationSnapshotJson)
				?? throw new JsonException("Compensation calculation snapshot is empty.");
		}
		catch (JsonException exception)
		{
			throw new BusinessException("HR:Payroll:CompensationRevisionSnapshotInvalid", innerException: exception)
				.WithData("revisionId", revision.Id);
		}

		foreach (var line in calculation.Deductions.Where(x => x.IsStatutory &&
			x.SemanticType is "EMPLOYEE_PROVIDENT_FUND" or "PROFESSIONAL_TAX"))
		{
			if (!PayrollComponentSemanticTypeCodes.TryParse(line.SemanticType, out var semanticType) ||
				components.Any(x => !x.IsEarning && x.SemanticType == semanticType))
			{
				continue;
			}

			components.Add(new EmployeePayrollComponent(
				Guid.NewGuid(),
				revision.EmployeeId,
				line.Name,
				line.ComponentId == Guid.Empty ? null : line.ComponentId,
				"System-calculated statutory deduction",
				0m,
				isFixed: true,
				isTemp: false,
				applicableMonth: null,
				SalaryComponentType.Fixed,
				SalaryComponentDependency.None,
				isEarning: false,
				usesMasterValue: false,
				semanticType,
				line.SemanticType == "EMPLOYEE_PROVIDENT_FUND" ? calculation.PfRuleId : null));
		}
	}
}
