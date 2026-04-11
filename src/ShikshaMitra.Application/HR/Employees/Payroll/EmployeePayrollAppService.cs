using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Employees.Payroll;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class EmployeePayrollAppService(
	IEmployeeRepository employeeRepository,
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository,
	IEmployeeCacheManager employeeCacheManager,
	IGuidGenerator guidGenerator)
	: ApplicationService, IEmployeePayrollAppService
{
	public async Task<EmployeePayrollDetailsDto> GetPayrollDetailsAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		return new EmployeePayrollDetailsDto
		{
			EmployeeId = employee.EmployeeCode,
			PayrollDetails = payrollDetails != null ? new PayrollDetailsDto
			{
				BankName = payrollDetails.BankName,
				BranchName = payrollDetails.BranchName,
				AccountHolderName = payrollDetails.AccountHolderName,
				AccountNumber = payrollDetails.AccountNumber,
				IfscCode = payrollDetails.IfscCode,
				PfRule = payrollDetails.PfRule,
				EpfNumber = payrollDetails.EpfNumber,
				UanNumber = payrollDetails.UanNumber,
				PunchId = payrollDetails.PunchId,
				BasicSalary = payrollDetails.BasicSalary,
				TdsPercentage = payrollDetails.TdsPercentage,
				TdsAmount = payrollDetails.TdsAmount
			} : new PayrollDetailsDto(),
			Earnings = (earnings ?? Enumerable.Empty<EmployeePayrollComponent>()).Select(e => new PayrollComponentDto
			{
				Name = e.Name,
				Description = e.Description,
				Amount = e.Amount,
				IsFixed = e.IsFixed,
				IsTemp = e.IsTemp,
				ComponentType = e.ComponentType,
				DependOn = e.DependOn
			}).ToList(),
			Deductions = (deductions ?? Enumerable.Empty<EmployeePayrollComponent>()).Select(d => new PayrollComponentDto
			{
				Name = d.Name,
				Description = d.Description,
				Amount = d.Amount,
				IsFixed = d.IsFixed,
				IsTemp = d.IsTemp,
				ComponentType = d.ComponentType,
				DependOn = d.DependOn
			}).ToList()
		};
	}

	public async Task UpdatePayrollDetailsAsync(Guid employeeId, UpdateEmployeePayrollDetailsDto input)
	{
		if (input.PayrollDetails == null)
		{
			throw new BusinessException(HRErrorCodes.EmployeePayrollDetailsRequired);
		}

		await employeeRepository.GetAsync(employeeId);

		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);

		if (payrollDetails == null)
		{
			payrollDetails = new EmployeePayrollDetails(guidGenerator.Create(), employeeId);
			payrollDetails.UpdateDetails(
				input.PayrollDetails.BankName, input.PayrollDetails.BranchName,
				input.PayrollDetails.AccountHolderName, input.PayrollDetails.AccountNumber,
				input.PayrollDetails.IfscCode, input.PayrollDetails.PfRule,
				input.PayrollDetails.EpfNumber, input.PayrollDetails.UanNumber,
				input.PayrollDetails.PunchId, input.PayrollDetails.BasicSalary,
				input.PayrollDetails.TdsPercentage, input.PayrollDetails.TdsAmount);
			await payrollDetailsRepository.InsertAsync(payrollDetails);
		}
		else
		{
			payrollDetails.UpdateDetails(
				input.PayrollDetails.BankName, input.PayrollDetails.BranchName,
				input.PayrollDetails.AccountHolderName, input.PayrollDetails.AccountNumber,
				input.PayrollDetails.IfscCode, input.PayrollDetails.PfRule,
				input.PayrollDetails.EpfNumber, input.PayrollDetails.UanNumber,
				input.PayrollDetails.PunchId, input.PayrollDetails.BasicSalary,
				input.PayrollDetails.TdsPercentage, input.PayrollDetails.TdsAmount);
			await payrollDetailsRepository.UpdateAsync(payrollDetails);
		}

		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		foreach (var earning in input.Earnings ?? Enumerable.Empty<PayrollComponentDto>())
		{
			var component = new EmployeePayrollComponent(
				guidGenerator.Create(), employeeId,
				earning.Name, componentId: null, earning.Description, earning.Amount,
				earning.IsFixed, earning.IsTemp, applicableMonth: null, earning.ComponentType,
				earning.DependOn, isEarning: true);
			await payrollComponentRepository.InsertAsync(component);
		}

		foreach (var deduction in input.Deductions ?? Enumerable.Empty<PayrollComponentDto>())
		{
			var component = new EmployeePayrollComponent(
				guidGenerator.Create(), employeeId,
				deduction.Name, componentId: null, deduction.Description, deduction.Amount,
				deduction.IsFixed, deduction.IsTemp, applicableMonth: null, deduction.ComponentType,
				deduction.DependOn, isEarning: false);
			await payrollComponentRepository.InsertAsync(component);
		}

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
	}

	public async Task<PayrollContract> GetPayrollContractAsync(Guid employeeId)
	{
		await employeeRepository.GetAsync(employeeId);

		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);
		var earningComponentIdByName = await GetEarningComponentIdsByNameAsync(earnings);
		var deductionComponentIdByName = await GetDeductionComponentIdsByNameAsync(deductions);

		return new PayrollContract
		{
			BankDetails = payrollDetails != null ? new BankDetailsContract
			{
				BankName = payrollDetails.BankName,
				BranchName = payrollDetails.BranchName,
				AccountHolderName = payrollDetails.AccountHolderName,
				AccountNumber = payrollDetails.AccountNumber,
				IfscCode = payrollDetails.IfscCode
			} : null,
			SalaryDetails = payrollDetails != null ? new SalaryDetailsContract
			{
				PfRuleId = !string.IsNullOrEmpty(payrollDetails.PfRule)
					&& Guid.TryParse(payrollDetails.PfRule, out var pfRuleId) ? pfRuleId : null,
				EpfNumber = payrollDetails.EpfNumber,
				UanNumber = payrollDetails.UanNumber,
				BasicSalary = payrollDetails.BasicSalary,
				PunchId = payrollDetails.PunchId,
				TdsPercentage = payrollDetails.TdsPercentage,
				TdsAmount = payrollDetails.TdsAmount
			} : null,
			EarningComponents = earnings?.Select(e => new ComponentAmountContract
			{
				// Prefer persisted master component ID, then name-based lookup, then employee component row ID.
				ComponentId = e.ComponentId
					?? (earningComponentIdByName.TryGetValue(e.Name, out var masterId) ? masterId : e.Id),
				Amount = e.Amount
			}).ToList() ?? new List<ComponentAmountContract>(),
			DeductionComponents = deductions?.Select(d => new ComponentAmountContract
			{
				// Prefer persisted master component ID, then name-based lookup, then employee component row ID.
				ComponentId = d.ComponentId
					?? (deductionComponentIdByName.TryGetValue(d.Name, out var masterId) ? masterId : d.Id),
				Amount = d.Amount
			}).ToList() ?? new List<ComponentAmountContract>()
		};
	}

	public async Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input)
	{
		await employeeRepository.GetAsync(employeeId);

		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);

		if (payrollDetails == null)
		{
			payrollDetails = new EmployeePayrollDetails(guidGenerator.Create(), employeeId);
			payrollDetails.UpdateDetails(
				input.BankDetails?.BankName, input.BankDetails?.BranchName,
				input.BankDetails?.AccountHolderName, input.BankDetails?.AccountNumber,
				input.BankDetails?.IfscCode, input.SalaryDetails?.PfRuleId?.ToString(),
				input.SalaryDetails?.EpfNumber, input.SalaryDetails?.UanNumber,
				input.SalaryDetails?.PunchId, input.SalaryDetails?.BasicSalary,
				input.SalaryDetails?.TdsPercentage, input.SalaryDetails?.TdsAmount);
			await payrollDetailsRepository.InsertAsync(payrollDetails);
		}
		else
		{
			payrollDetails.UpdateDetails(
				input.BankDetails?.BankName, input.BankDetails?.BranchName,
				input.BankDetails?.AccountHolderName, input.BankDetails?.AccountNumber,
				input.BankDetails?.IfscCode, input.SalaryDetails?.PfRuleId?.ToString(),
				input.SalaryDetails?.EpfNumber, input.SalaryDetails?.UanNumber,
				input.SalaryDetails?.PunchId, input.SalaryDetails?.BasicSalary,
				input.SalaryDetails?.TdsPercentage, input.SalaryDetails?.TdsAmount);
			await payrollDetailsRepository.UpdateAsync(payrollDetails);
		}

		var existingEarnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var existingDeductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);
		var existingEarningByRowId = existingEarnings.ToDictionary(x => x.Id);
		var existingDeductionByRowId = existingDeductions.ToDictionary(x => x.Id);

		var inputEarnings = input.EarningComponents ?? [];
		var earningIds = inputEarnings
			.Select(x => x.ComponentId)
			.Where(id => !existingEarningByRowId.ContainsKey(id))
			.Distinct()
			.ToList();
		var earningMasters = earningIds.Count == 0
			? new List<EarningComponent>()
			: await AsyncExecuter.ToListAsync(
				(await earningComponentRepository.GetQueryableAsync())
				.Where(x => earningIds.Contains(x.Id)));
		EnsureAllMasterComponentsExist(
			earningIds,
			earningMasters.Select(x => x.Id),
			HRErrorCodes.EmployeePayrollInvalidEarningComponent);
		var earningById = earningMasters.ToDictionary(x => x.Id);

		var inputDeductions = input.DeductionComponents ?? [];
		var deductionIds = inputDeductions
			.Select(x => x.ComponentId)
			.Where(id => !existingDeductionByRowId.ContainsKey(id))
			.Distinct()
			.ToList();
		var deductionMasters = deductionIds.Count == 0
			? new List<DeductionComponent>()
			: await AsyncExecuter.ToListAsync(
				(await deductionComponentRepository.GetQueryableAsync())
				.Where(x => deductionIds.Contains(x.Id)));
		EnsureAllMasterComponentsExist(
			deductionIds,
			deductionMasters.Select(x => x.Id),
			HRErrorCodes.EmployeePayrollInvalidDeductionComponent);
		var deductionById = deductionMasters.ToDictionary(x => x.Id);

		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		if (inputEarnings.Count > 0)
		{
			foreach (var earningInput in inputEarnings)
			{
				if (existingEarningByRowId.TryGetValue(earningInput.ComponentId, out var existingEarningComponent))
				{
					var earningComponentFromExisting = new EmployeePayrollComponent(
						guidGenerator.Create(),
						employeeId,
						existingEarningComponent.Name,
						existingEarningComponent.ComponentId,
						existingEarningComponent.Description,
						earningInput.Amount,
						existingEarningComponent.IsFixed,
						existingEarningComponent.IsTemp,
						existingEarningComponent.ApplicableMonth,
						existingEarningComponent.ComponentType,
						existingEarningComponent.DependOn,
						isEarning: true);

					await payrollComponentRepository.InsertAsync(earningComponentFromExisting);
					continue;
				}

				if (!earningById.TryGetValue(earningInput.ComponentId, out var earningMaster))
				{
					throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidEarningComponent)
						.WithData("componentId", earningInput.ComponentId);
				}

				var earningComponent = new EmployeePayrollComponent(
					guidGenerator.Create(),
					employeeId,
					earningMaster.Name,
					earningInput.ComponentId,
					earningMaster.Description,
					earningInput.Amount,
					earningMaster.CalculationType == CalculationType.Amount,
					isTemp: false,
					applicableMonth: null,
					earningMaster.CalculationType == CalculationType.Amount
						? SalaryComponentType.Fixed
						: SalaryComponentType.Percentage,
					MapDependency(earningMaster.DependOn),
					isEarning: true);

				await payrollComponentRepository.InsertAsync(earningComponent);
			}
		}

		if (inputDeductions.Count > 0)
		{
			foreach (var deductionInput in inputDeductions)
			{
				if (existingDeductionByRowId.TryGetValue(deductionInput.ComponentId, out var existingDeductionComponent))
				{
					var deductionComponentFromExisting = new EmployeePayrollComponent(
						guidGenerator.Create(),
						employeeId,
						existingDeductionComponent.Name,
						existingDeductionComponent.ComponentId,
						existingDeductionComponent.Description,
						deductionInput.Amount,
						existingDeductionComponent.IsFixed,
						existingDeductionComponent.IsTemp,
						existingDeductionComponent.ApplicableMonth,
						existingDeductionComponent.ComponentType,
						existingDeductionComponent.DependOn,
						isEarning: false);

					await payrollComponentRepository.InsertAsync(deductionComponentFromExisting);
					continue;
				}

				if (!deductionById.TryGetValue(deductionInput.ComponentId, out var deductionMaster))
				{
					throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
						.WithData("componentId", deductionInput.ComponentId);
				}

				var deductionComponent = new EmployeePayrollComponent(
					guidGenerator.Create(),
					employeeId,
					deductionMaster.Name,
					deductionInput.ComponentId,
					deductionMaster.Description,
					deductionInput.Amount,
					deductionMaster.CalculationType == CalculationType.Amount,
					isTemp: false,
					applicableMonth: null,
					deductionMaster.CalculationType == CalculationType.Amount
						? SalaryComponentType.Fixed
						: SalaryComponentType.Percentage,
					MapDependency(deductionMaster.DependOn),
					isEarning: false);

				await payrollComponentRepository.InsertAsync(deductionComponent);
			}
		}

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
	}

	private static void EnsureAllMasterComponentsExist(
		IReadOnlyCollection<Guid> requestedIds,
		IEnumerable<Guid> foundIds,
		string errorCode)
	{
		if (requestedIds.Count == 0)
		{
			return;
		}

		var foundIdSet = foundIds.ToHashSet();
		var missingIds = requestedIds.Where(id => !foundIdSet.Contains(id)).ToList();
		if (missingIds.Count == 0)
		{
			return;
		}

		throw new BusinessException(errorCode)
			.WithData("missingComponentIds", string.Join(",", missingIds));
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

	private async Task<Dictionary<string, Guid>> GetEarningComponentIdsByNameAsync(
		IEnumerable<EmployeePayrollComponent>? earnings)
	{
		var names = (earnings ?? Enumerable.Empty<EmployeePayrollComponent>())
			.Select(x => x.Name)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (names.Count == 0)
		{
			return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
		}

		var masters = await AsyncExecuter.ToListAsync(
			(await earningComponentRepository.GetQueryableAsync()).Where(x => names.Contains(x.Name)));

		return masters
			.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
	}

	private async Task<Dictionary<string, Guid>> GetDeductionComponentIdsByNameAsync(
		IEnumerable<EmployeePayrollComponent>? deductions)
	{
		var names = (deductions ?? Enumerable.Empty<EmployeePayrollComponent>())
			.Select(x => x.Name)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (names.Count == 0)
		{
			return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
		}

		var masters = await AsyncExecuter.ToListAsync(
			(await deductionComponentRepository.GetQueryableAsync()).Where(x => names.Contains(x.Name)));

		return masters
			.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
	}

}
