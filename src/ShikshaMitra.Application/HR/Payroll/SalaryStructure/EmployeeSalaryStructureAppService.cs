using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
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

		// Delete existing components
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		// Build all components for batch insert
		var componentsToInsert = new List<EmployeePayrollComponent>();

		// Build earning components
		foreach (var earning in input.EarningComponents)
		{
			var existingComponent = FindExistingComponent(earning, existingEarningById);
			var master = ShouldResolveMasterComponent(earning)
				&& earningMasterById.TryGetValue(earning.ComponentId!.Value, out var earningMaster)
				? earningMaster
				: null;
			var componentType = master is not null
				? MapCalculationType(master.CalculationType)
				: existingComponent?.ComponentType ?? SalaryComponentType.Fixed;
			var dependOn = master is not null
				? MapDependency(master.DependOn)
				: existingComponent?.DependOn;
			var description = master?.Description ?? existingComponent?.Description;

			componentsToInsert.Add(new EmployeePayrollComponent(
				existingComponent?.Id ?? _guidGenerator.Create(),
				employeeId,
				earning.Name,
				master?.Id,
				description,
				earning.Amount ?? 0m,
				earning.IsFixed,
				earning.IsTemp,
				earning.ApplicableMonth ?? existingComponent?.ApplicableMonth,
				componentType,
				dependOn,
				isEarning: true
			));
		}

		// Build deduction components
		foreach (var deduction in input.DeductionComponents)
		{
			var existingComponent = FindExistingComponent(deduction, existingDeductionById);
			var master = ShouldResolveMasterComponent(deduction)
				&& deductionMasterById.TryGetValue(deduction.ComponentId!.Value, out var deductionMaster)
				? deductionMaster
				: null;
			var componentType = master is not null
				? MapCalculationType(master.CalculationType)
				: existingComponent?.ComponentType ?? SalaryComponentType.Fixed;
			var dependOn = master is not null
				? MapDependency(master.DependOn)
				: existingComponent?.DependOn;
			var description = master?.Description ?? existingComponent?.Description;

			componentsToInsert.Add(new EmployeePayrollComponent(
				existingComponent?.Id ?? _guidGenerator.Create(),
				employeeId,
				deduction.Name,
				master?.Id,
				description,
				deduction.Amount ?? 0m,
				deduction.IsFixed,
				deduction.IsTemp,
				deduction.ApplicableMonth ?? existingComponent?.ApplicableMonth,
				componentType,
				dependOn,
				isEarning: false
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
			(await _earningComponentRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
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
			(await _deductionComponentRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
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
}

