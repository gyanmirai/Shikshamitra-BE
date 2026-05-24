using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// Application service for managing employee salary structure.
/// </summary>
[RemoteService(false)]
public class EmployeeSalaryStructureAppService : ApplicationService, IEmployeeSalaryStructureAppService
{
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly IEarningComponentRepository _earningComponentRepository;
	private readonly IDeductionComponentRepository _deductionComponentRepository;
	private readonly IGuidGenerator _guidGenerator;

	public EmployeeSalaryStructureAppService(
		IEmployeeRepository employeeRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		IEarningComponentRepository earningComponentRepository,
		IDeductionComponentRepository deductionComponentRepository,
		IGuidGenerator guidGenerator)
	{
		_employeeRepository = employeeRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_earningComponentRepository = earningComponentRepository;
		_deductionComponentRepository = deductionComponentRepository;
		_guidGenerator = guidGenerator;
	}

	/// <inheritdoc />
	public async Task<SalaryStructureResponseDto> GetAsync(Guid employeeId)
	{
		await EnsureEmployeeExistsAsync(employeeId);

		var earnings = await _payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await _payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		return new SalaryStructureResponseDto
		{
			EarningComponents = MapToDto(earnings),
			DeductionComponents = MapToDto(deductions)
		};
	}

	/// <inheritdoc />
	public async Task UpdateAsync(Guid employeeId, UpdateSalaryStructureDto input)
	{
		await EnsureEmployeeExistsAsync(employeeId);

		var existingEarnings = await _payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var existingDeductions = await _payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);
		var existingEarningById = existingEarnings.ToDictionary(x => x.Id);
		var existingDeductionById = existingDeductions.ToDictionary(x => x.Id);

		EnsureCustomComponentIdsAreValid(
			input.EarningComponents,
			existingEarningById,
			HRErrorCodes.EmployeePayrollInvalidEarningComponent);
		EnsureCustomComponentIdsAreValid(
			input.DeductionComponents,
			existingDeductionById,
			HRErrorCodes.EmployeePayrollInvalidDeductionComponent);

		var earningMasterById = await GetEarningMasterByIdAsync(
			input.EarningComponents.Where(x => ShouldResolveMasterComponent(x)));
		var deductionMasterById = await GetDeductionMasterByIdAsync(
			input.DeductionComponents.Where(x => ShouldResolveMasterComponent(x)));
		EnsureAllMasterComponentsExist(
			input.EarningComponents.Where(ShouldResolveMasterComponent).Select(x => x.ComponentId!.Value),
			earningMasterById.Keys,
			HRErrorCodes.EmployeePayrollInvalidEarningComponent);
		EnsureAllMasterComponentsExist(
			input.DeductionComponents.Where(ShouldResolveMasterComponent).Select(x => x.ComponentId!.Value),
			deductionMasterById.Keys,
			HRErrorCodes.EmployeePayrollInvalidDeductionComponent);

		var resolvedEarnings = input.EarningComponents
			.Select(earning =>
			{
				var master = ShouldResolveMasterComponent(earning)
					&& earningMasterById.TryGetValue(earning.ComponentId!.Value, out var earningMaster)
					? earningMaster
					: null;
				return new ResolvedSalaryComponent<EarningComponent>(
					earning,
					FindExistingComponent(earning, existingEarningById),
					master);
			})
			.ToList();
		var resolvedDeductions = input.DeductionComponents
			.Select(deduction =>
			{
				var master = ShouldResolveMasterComponent(deduction)
					&& deductionMasterById.TryGetValue(deduction.ComponentId!.Value, out var deductionMaster)
					? deductionMaster
					: null;
				return new ResolvedSalaryComponent<DeductionComponent>(
					deduction,
					FindExistingComponent(deduction, existingDeductionById),
					master);
			})
			.ToList();

		var earningAmountLines = resolvedEarnings.Select(ToAmountLine).ToList();
		var deductionAmountLines = resolvedDeductions.Select(ToAmountLine).ToList();
		var earningAmountsByComponentId = PayrollComponentAmountResolver.ResolveEarningAmounts(earningAmountLines);
		var grossAmount = earningAmountsByComponentId.Values.Sum();
		var deductionAmountsByComponentId = PayrollComponentAmountResolver.ResolveDeductionAmounts(
			deductionAmountLines,
			earningAmountLines,
			earningAmountsByComponentId,
			grossAmount);

		// Delete existing components
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		// Build all components for batch insert
		var componentsToInsert = new List<EmployeePayrollComponent>();

		// Build earning components
		foreach (var resolved in resolvedEarnings)
		{
			var earning = resolved.Line;
			var master = resolved.Master;
			var existingComponent = resolved.Existing;
			var componentType = master is not null
				? MapCalculationType(master.CalculationType)
				: existingComponent?.ComponentType ?? SalaryComponentType.Fixed;
			var dependOn = master is not null
				? MapDependency(master.DependOn)
				: existingComponent?.DependOn;
			var description = master?.Description ?? existingComponent?.Description;
			var amount = master?.Id is Guid masterId && earningAmountsByComponentId.TryGetValue(masterId, out var resolvedAmount)
				? resolvedAmount
				: ResolveAmountComponentFallback(earning, master?.Amount);

			componentsToInsert.Add(new EmployeePayrollComponent(
				existingComponent?.Id ?? _guidGenerator.Create(),
				employeeId,
				earning.Name,
				master?.Id,
				description,
				amount,
				earning.IsFixed,
				earning.IsTemp,
				earning.ApplicableMonth ?? existingComponent?.ApplicableMonth,
				componentType,
				dependOn,
				isEarning: true,
				usesMasterValue: ShouldUseMasterValue(earning, master)
			));
		}

		// Build deduction components
		foreach (var resolved in resolvedDeductions)
		{
			var deduction = resolved.Line;
			var master = resolved.Master;
			var existingComponent = resolved.Existing;
			var componentType = master is not null
				? MapCalculationType(master.CalculationType)
				: existingComponent?.ComponentType ?? SalaryComponentType.Fixed;
			var dependOn = master is not null
				? MapDependency(master.DependOn)
				: existingComponent?.DependOn;
			var description = master?.Description ?? existingComponent?.Description;
			var amount = master?.Id is Guid masterId && deductionAmountsByComponentId.TryGetValue(masterId, out var resolvedAmount)
				? resolvedAmount
				: ResolveAmountComponentFallback(deduction, master?.Amount);

			componentsToInsert.Add(new EmployeePayrollComponent(
				existingComponent?.Id ?? _guidGenerator.Create(),
				employeeId,
				deduction.Name,
				master?.Id,
				description,
				amount,
				deduction.IsFixed,
				deduction.IsTemp,
				deduction.ApplicableMonth ?? existingComponent?.ApplicableMonth,
				componentType,
				dependOn,
				isEarning: false,
				usesMasterValue: ShouldUseMasterValue(deduction, master)
			));
		}

		// Batch insert all components (single DB round-trip)
		if (componentsToInsert.Count > 0)
		{
			await _payrollComponentRepository.InsertManyAsync(componentsToInsert);
		}
	}

	private async Task EnsureEmployeeExistsAsync(Guid employeeId)
	{
		// Salary structure endpoints only need existence check; avoid loading full employee graph.
		var employee = await _employeeRepository.FindAsync(employeeId, includeDetails: false);
		if (employee == null)
		{
			// Surface a proper 404 response instead of generic 500 when employee does not exist.
			throw new EntityNotFoundException(typeof(Employee), employeeId);
		}
	}

	private static List<SalaryComponentDto> MapToDto(IEnumerable<EmployeePayrollComponent> components)
	{
		return components.Select(c => new SalaryComponentDto
		{
			Id = c.Id,
			ComponentId = c.ComponentId,
			Name = c.Name,
			Amount = c.Amount,
			IsFixed = c.IsFixed,
			IsTemp = c.IsTemp,
			ApplicableMonth = c.ApplicableMonth
		}).ToList();
	}

	private static EmployeePayrollComponent? FindExistingComponent(
		SalaryComponentDto component,
		IReadOnlyDictionary<Guid, EmployeePayrollComponent> existingById)
	{
		if (component.Id.HasValue && existingById.TryGetValue(component.Id.Value, out var byId))
		{
			return byId;
		}

		if (component.ComponentId.HasValue && existingById.TryGetValue(component.ComponentId.Value, out var byComponentId))
		{
			return byComponentId;
		}

		return null;
	}

	private static bool ShouldResolveMasterComponent(SalaryComponentDto component)
	{
		return component.ComponentId.HasValue
			&& component.ComponentId.Value != Guid.Empty
			&& !component.IsTemp
			&& component.ComponentId != component.Id;
	}

	private static void EnsureCustomComponentIdsAreValid(
		IEnumerable<SalaryComponentDto> components,
		IReadOnlyDictionary<Guid, EmployeePayrollComponent> existingById,
		string errorCode)
	{
		foreach (var component in components)
		{
			if (!component.ComponentId.HasValue || component.ComponentId.Value == Guid.Empty ||
			    ShouldResolveMasterComponent(component))
			{
				continue;
			}

			var matchesExistingCustomRow = existingById.TryGetValue(component.ComponentId.Value, out var existing) &&
				(existing.IsTemp || existing.ComponentId is null || existing.ComponentId == Guid.Empty);
			var matchesSubmittedExistingCustomRow = component.Id.HasValue &&
				component.ComponentId.Value == component.Id.Value &&
				existingById.TryGetValue(component.Id.Value, out existing) &&
				(existing.IsTemp || existing.ComponentId is null || existing.ComponentId == Guid.Empty);

			if (!matchesExistingCustomRow && !matchesSubmittedExistingCustomRow)
			{
				throw new BusinessException(errorCode)
					.WithData("componentId", component.ComponentId.Value);
			}
		}
	}

	private async Task<Dictionary<Guid, EarningComponent>> GetEarningMasterByIdAsync(IEnumerable<SalaryComponentDto> components)
	{
		var ids = components
			.Where(x => x.ComponentId.HasValue)
			.Select(x => x.ComponentId!.Value)
			.Distinct()
			.ToList();

		if (ids.Count == 0)
		{
			return new Dictionary<Guid, EarningComponent>();
		}

		var masters = await AsyncExecuter.ToListAsync(
			(await _earningComponentRepository.GetQueryableAsync()).Where(x => x.IsActive && ids.Contains(x.Id)));
		return masters.ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<Guid, DeductionComponent>> GetDeductionMasterByIdAsync(IEnumerable<SalaryComponentDto> components)
	{
		var ids = components
			.Where(x => x.ComponentId.HasValue)
			.Select(x => x.ComponentId!.Value)
			.Distinct()
			.ToList();

		if (ids.Count == 0)
		{
			return new Dictionary<Guid, DeductionComponent>();
		}

		var masters = await AsyncExecuter.ToListAsync(
			(await _deductionComponentRepository.GetQueryableAsync()).Where(x => x.IsActive && ids.Contains(x.Id)));
		return masters.ToDictionary(x => x.Id);
	}

	private static SalaryComponentType MapCalculationType(CalculationType calculationType)
	{
		return calculationType == CalculationType.Amount
			? SalaryComponentType.Fixed
			: SalaryComponentType.Percentage;
	}

	private static SalaryComponentDependency? MapDependency(ComponentDependency dependOn)
	{
		return dependOn switch
		{
			ComponentDependency.None => SalaryComponentDependency.None,
			ComponentDependency.Basic => SalaryComponentDependency.Basic,
			ComponentDependency.Gross => SalaryComponentDependency.Gross,
			_ => null
		};
	}

	private static Dictionary<Guid, decimal> ResolveEarningAmounts(
		IReadOnlyList<ResolvedSalaryComponent<EarningComponent>> earnings)
	{
		var amountsByComponentId = new Dictionary<Guid, decimal>();

		foreach (var earning in earnings.Where(x => x.Master?.CalculationType != CalculationType.Percentage))
		{
			if (earning.Master?.Id is Guid masterId)
			{
				amountsByComponentId[masterId] = ResolveAmountComponentFallback(earning.Line, earning.Master.Amount);
			}
		}

		var pendingPercentageEarnings = earnings
			.Where(x => x.Master?.CalculationType == CalculationType.Percentage)
			.ToList();
		while (pendingPercentageEarnings.Count > 0)
		{
			var resolvedAny = false;
			foreach (var earning in pendingPercentageEarnings.ToList())
			{
				var master = earning.Master!;
				if (!TryResolvePercentageBaseAmount(
					    master.DependOn,
					    master.SelectedEarningComponentId,
					    earnings,
					    amountsByComponentId,
					    new Dictionary<Guid, decimal>(),
					    out var baseAmount))
				{
					continue;
				}

				amountsByComponentId[master.Id] = CalculatePercentageAmount(baseAmount, master.Percentage);
				pendingPercentageEarnings.Remove(earning);
				resolvedAny = true;
			}

			if (!resolvedAny)
			{
				var unresolved = pendingPercentageEarnings[0].Master!;
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidEarningComponent)
					.WithData("componentId", unresolved.SelectedEarningComponentId?.ToString() ?? string.Empty)
					.WithData("dependency", unresolved.DependOn.ToString());
			}
		}

		return amountsByComponentId;
	}

	private static Dictionary<Guid, decimal> ResolveDeductionAmounts(
		IReadOnlyList<ResolvedSalaryComponent<DeductionComponent>> deductions,
		IReadOnlyList<ResolvedSalaryComponent<EarningComponent>> earnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId,
		decimal grossAmount)
	{
		var amountsByComponentId = new Dictionary<Guid, decimal>();

		foreach (var deduction in deductions.Where(x => x.Master?.CalculationType != CalculationType.Percentage))
		{
			if (deduction.Master?.Id is Guid masterId)
			{
				amountsByComponentId[masterId] = ResolveAmountComponentFallback(deduction.Line, deduction.Master.Amount);
			}
		}

		var pendingPercentageDeductions = deductions
			.Where(x => x.Master?.CalculationType == CalculationType.Percentage)
			.ToList();
		while (pendingPercentageDeductions.Count > 0)
		{
			var resolvedAny = false;
			foreach (var deduction in pendingPercentageDeductions.ToList())
			{
				var master = deduction.Master!;
				if (!TryResolvePercentageBaseAmount(
					    master.DependOn,
					    master.SelectedDeductionComponentId,
					    earnings,
					    earningAmountsByComponentId,
					    amountsByComponentId,
					    out var baseAmount,
					    grossAmount))
				{
					continue;
				}

				amountsByComponentId[master.Id] = CalculatePercentageAmount(baseAmount, master.Percentage);
				pendingPercentageDeductions.Remove(deduction);
				resolvedAny = true;
			}

			if (!resolvedAny)
			{
				var unresolved = pendingPercentageDeductions[0].Master!;
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
					.WithData("componentId", unresolved.SelectedDeductionComponentId?.ToString() ?? string.Empty)
					.WithData("dependency", unresolved.DependOn.ToString());
			}
		}

		return amountsByComponentId;
	}

	private static bool TryResolvePercentageBaseAmount(
		ComponentDependency dependOn,
		Guid? selectedComponentId,
		IReadOnlyList<ResolvedSalaryComponent<EarningComponent>>? earnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId,
		IReadOnlyDictionary<Guid, decimal> deductionAmountsByComponentId,
		out decimal amount,
		decimal? grossAmount = null)
	{
		switch (dependOn)
		{
			case ComponentDependency.Basic:
				return TryResolveBasicAmount(earnings, earningAmountsByComponentId, out amount);
			case ComponentDependency.Gross:
				amount = grossAmount ?? earningAmountsByComponentId.Values.Sum();
				return true;
			case ComponentDependency.CustomComponent when selectedComponentId.HasValue &&
				earningAmountsByComponentId.TryGetValue(selectedComponentId.Value, out var earningAmount):
				amount = earningAmount;
				return true;
			case ComponentDependency.CustomComponent when selectedComponentId.HasValue &&
				deductionAmountsByComponentId.TryGetValue(selectedComponentId.Value, out var deductionAmount):
				amount = deductionAmount;
				return true;
			default:
				amount = 0m;
				return false;
		}
	}

	private static bool TryResolveBasicAmount(
		IReadOnlyList<ResolvedSalaryComponent<EarningComponent>>? earnings,
		IReadOnlyDictionary<Guid, decimal> earningAmountsByComponentId,
		out decimal amount)
	{
		if (earnings != null)
		{
			var basic = earnings.FirstOrDefault(x =>
				x.Master?.Id is Guid masterId &&
				earningAmountsByComponentId.ContainsKey(masterId) &&
				IsBasicSalaryComponentName(x.Line.Name));
			if (basic?.Master?.Id is Guid masterId)
			{
				amount = earningAmountsByComponentId[masterId];
				return true;
			}
		}

		if (earningAmountsByComponentId.Count == 1)
		{
			amount = earningAmountsByComponentId.Values.Single();
			return true;
		}

		amount = 0m;
		return false;
	}

	private static decimal ResolveAmountComponentFallback(SalaryComponentDto line, decimal? masterAmount) =>
		PayrollComponentAmountResolver.ResolveAmountComponentFallback(line.Amount, masterAmount);

	private static PayrollComponentAmountLine<EarningComponent> ToAmountLine(
		ResolvedSalaryComponent<EarningComponent> resolved)
	{
		var line = resolved.Line;
		return new PayrollComponentAmountLine<EarningComponent>(
			line.Id,
			line.ComponentId,
			resolved.Master?.Name ?? line.Name,
			line.Amount,
			line.IsTemp,
			resolved.Master);
	}

	private static PayrollComponentAmountLine<DeductionComponent> ToAmountLine(
		ResolvedSalaryComponent<DeductionComponent> resolved)
	{
		var line = resolved.Line;
		return new PayrollComponentAmountLine<DeductionComponent>(
			line.Id,
			line.ComponentId,
			resolved.Master?.Name ?? line.Name,
			line.Amount,
			line.IsTemp,
			resolved.Master);
	}

	private static decimal CalculatePercentageAmount(decimal baseAmount, decimal? percentage) =>
		Math.Round(baseAmount * (percentage ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero);

	private static bool IsBasicSalaryComponentName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		return string.Equals(name, "Basic", StringComparison.OrdinalIgnoreCase)
		       || name.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);
	}

	private static bool ShouldUseMasterValue(SalaryComponentDto line, EarningComponent? master)
	{
		if (master is null || line.IsTemp)
		{
			return false;
		}

		return master.CalculationType == CalculationType.Percentage ||
		       (line.Amount ?? 0m) == (master.Amount ?? 0m);
	}

	private static bool ShouldUseMasterValue(SalaryComponentDto line, DeductionComponent? master)
	{
		if (master is null || line.IsTemp)
		{
			return false;
		}

		return master.CalculationType == CalculationType.Percentage ||
		       (line.Amount ?? 0m) == (master.Amount ?? 0m);
	}

	private static void EnsureAllMasterComponentsExist(
		IEnumerable<Guid> requestedIds,
		IEnumerable<Guid> foundIds,
		string errorCode)
	{
		var requested = requestedIds.Distinct().ToList();
		if (requested.Count == 0)
		{
			return;
		}

		var found = foundIds.ToHashSet();
		var missing = requested.Where(id => !found.Contains(id)).ToList();
		if (missing.Count == 0)
		{
			return;
		}

		throw new BusinessException(errorCode)
			.WithData("missingComponentIds", string.Join(",", missing));
	}

	private sealed record ResolvedSalaryComponent<TMaster>(
		SalaryComponentDto Line,
		EmployeePayrollComponent? Existing,
		TMaster? Master);
}

