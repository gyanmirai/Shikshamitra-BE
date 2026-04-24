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
				TotalWorkingHourPerMonth = payrollDetails.TotalWorkingHourPerMonth
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
				input.PayrollDetails.PunchId, input.PayrollDetails.TotalWorkingHourPerMonth);
			await payrollDetailsRepository.InsertAsync(payrollDetails);
		}
		else
		{
			payrollDetails.UpdateDetails(
				input.PayrollDetails.BankName, input.PayrollDetails.BranchName,
				input.PayrollDetails.AccountHolderName, input.PayrollDetails.AccountNumber,
				input.PayrollDetails.IfscCode, input.PayrollDetails.PfRule,
				input.PayrollDetails.EpfNumber, input.PayrollDetails.UanNumber,
				input.PayrollDetails.PunchId, input.PayrollDetails.TotalWorkingHourPerMonth);
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

	public async Task<EmployeePayrollResponse> GetPayrollContractAsync(Guid employeeId)
	{
		await employeeRepository.GetAsync(employeeId);

		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);
		var earningComponentIdByName = await GetEarningComponentIdsByNameAsync(earnings);
		var deductionComponentIdByName = await GetDeductionComponentIdsByNameAsync(deductions);

		var bank = new BankDetailsContract
		{
			BankName = payrollDetails?.BankName,
			BranchName = payrollDetails?.BranchName,
			AccountHolderName = payrollDetails?.AccountHolderName,
			AccountNumber = payrollDetails?.AccountNumber,
			IfscCode = payrollDetails?.IfscCode
		};

		var salary = new SalaryDetailsContract
		{
			PfRuleId = payrollDetails != null
				&& !string.IsNullOrEmpty(payrollDetails.PfRule)
				&& Guid.TryParse(payrollDetails.PfRule, out var pfRuleId)
				? pfRuleId
				: null,
			EpfNumber = payrollDetails?.EpfNumber,
			UanNumber = payrollDetails?.UanNumber,
			PunchId = payrollDetails?.PunchId,
			TotalWorkingHourPerMonth = payrollDetails?.TotalWorkingHourPerMonth
		};

		return new EmployeePayrollResponse
		{
			Id = employeeId,
			BankDetails = bank,
			SalaryDetails = salary,
			EarningComponents = earnings?.Select(e => new SalaryComponentContract
			{
				ComponentId = e.ComponentId
					?? (earningComponentIdByName.TryGetValue(e.Name, out var masterEarningId) ? masterEarningId : null),
				Name = e.Name,
				Amount = e.Amount,
				IsFixed = e.IsFixed
			}).ToList() ?? [],
			DeductionComponents = deductions?.Select(d => new SalaryComponentContract
			{
				ComponentId = d.ComponentId
					?? (deductionComponentIdByName.TryGetValue(d.Name, out var masterDedId) ? masterDedId : null),
				Name = d.Name,
				Amount = d.Amount,
				IsFixed = d.IsFixed
			}).ToList() ?? []
		};
	}

	public async Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input)
	{
		ValidatePayrollContract(input);

		await employeeRepository.GetAsync(employeeId);

		var earningMasters = new List<(EarningComponent Master, SalaryComponentContract Line)>();
		foreach (var line in input.EarningComponents ?? [])
		{
			earningMasters.Add((await ResolveEarningMasterAsync(line), line));
		}

		var deductionMasters = new List<(DeductionComponent Master, SalaryComponentContract Line)>();
		foreach (var line in input.DeductionComponents ?? [])
		{
			deductionMasters.Add((await ResolveDeductionMasterAsync(line), line));
		}

		var earningIds = earningMasters.Select(x => x.Master.Id).ToHashSet();
		var overlap = deductionMasters.Select(x => x.Master.Id).Where(earningIds.Contains).ToList();
		if (overlap.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeePayrollComponentInBothLists)
				.WithData("overlappingComponentIds", string.Join(",", overlap));
		}

		foreach (var (master, line) in earningMasters)
		{
			var amount = line.Amount ?? master.Amount ?? 0m;
			if (amount <= 0)
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollComponentAmountInvalid)
					.WithData("componentId", master.Id)
					.WithData("amount", amount)
					.WithData("list", "earning");
			}
		}

		foreach (var (master, line) in deductionMasters)
		{
			var amount = line.Amount ?? master.Amount ?? 0m;
			if (amount <= 0)
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollComponentAmountInvalid)
					.WithData("componentId", master.Id)
					.WithData("amount", amount)
					.WithData("list", "deduction");
			}
		}

		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);

		if (payrollDetails == null)
		{
			payrollDetails = new EmployeePayrollDetails(guidGenerator.Create(), employeeId);
			payrollDetails.UpdateDetails(
				input.BankDetails?.BankName,
				input.BankDetails?.BranchName,
				input.BankDetails?.AccountHolderName,
				input.BankDetails?.AccountNumber,
				input.BankDetails?.IfscCode,
				input.SalaryDetails?.PfRuleId?.ToString(),
				input.SalaryDetails?.EpfNumber,
				input.SalaryDetails?.UanNumber,
				input.SalaryDetails?.PunchId,
				input.SalaryDetails?.TotalWorkingHourPerMonth);
			await payrollDetailsRepository.InsertAsync(payrollDetails);
		}
		else
		{
			var b = input.BankDetails;
			var s = input.SalaryDetails;
			payrollDetails.UpdateDetails(
				b != null ? b.BankName : payrollDetails.BankName,
				b != null ? b.BranchName : payrollDetails.BranchName,
				b != null ? b.AccountHolderName : payrollDetails.AccountHolderName,
				b != null ? b.AccountNumber : payrollDetails.AccountNumber,
				b != null ? b.IfscCode : payrollDetails.IfscCode,
				s != null ? s.PfRuleId?.ToString() : payrollDetails.PfRule,
				s != null ? s.EpfNumber : payrollDetails.EpfNumber,
				s != null ? s.UanNumber : payrollDetails.UanNumber,
				s != null ? s.PunchId : payrollDetails.PunchId,
				s != null ? s.TotalWorkingHourPerMonth : payrollDetails.TotalWorkingHourPerMonth);
			await payrollDetailsRepository.UpdateAsync(payrollDetails);
		}

		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		foreach (var (master, line) in earningMasters)
		{
			var amount = line.Amount ?? master.Amount ?? 0m;
			var isFixed = line.IsFixed ?? (master.CalculationType == CalculationType.Amount);

			var earningComponent = new EmployeePayrollComponent(
				guidGenerator.Create(),
				employeeId,
				master.Name,
				master.Id,
				master.Description,
				amount,
				isFixed,
				isTemp: false,
				applicableMonth: null,
				master.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapDependency(master.DependOn),
				isEarning: true);

			await payrollComponentRepository.InsertAsync(earningComponent);
		}

		foreach (var (master, line) in deductionMasters)
		{
			var amount = line.Amount ?? master.Amount ?? 0m;
			var isFixed = line.IsFixed ?? (master.CalculationType == CalculationType.Amount);

			var deductionComponent = new EmployeePayrollComponent(
				guidGenerator.Create(),
				employeeId,
				master.Name,
				master.Id,
				master.Description,
				amount,
				isFixed,
				isTemp: false,
				applicableMonth: null,
				master.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				MapDependency(master.DependOn),
				isEarning: false);

			await payrollComponentRepository.InsertAsync(deductionComponent);
		}

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
	}

	private async Task<EarningComponent> ResolveEarningMasterAsync(SalaryComponentContract line)
	{
		if (line.ComponentId is { } cid && cid != Guid.Empty)
		{
			try
			{
				return await earningComponentRepository.GetAsync(cid);
			}
			catch (Volo.Abp.Domain.Entities.EntityNotFoundException)
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidEarningComponent)
					.WithData("componentId", cid);
			}
		}

		var name = (line.Name ?? string.Empty).Trim();
		var matches = await AsyncExecuter.ToListAsync(
			(await earningComponentRepository.GetQueryableAsync())
				.Where(x => x.Name.ToLower() == name.ToLower()));
		if (matches.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidEarningComponent)
				.WithData("name", name);
		}

		return matches[0];
	}

	private async Task<DeductionComponent> ResolveDeductionMasterAsync(SalaryComponentContract line)
	{
		if (line.ComponentId is { } cid && cid != Guid.Empty)
		{
			try
			{
				return await deductionComponentRepository.GetAsync(cid);
			}
			catch (Volo.Abp.Domain.Entities.EntityNotFoundException)
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
					.WithData("componentId", cid);
			}
		}

		var name = (line.Name ?? string.Empty).Trim();
		var matches = await AsyncExecuter.ToListAsync(
			(await deductionComponentRepository.GetQueryableAsync())
				.Where(x => x.Name.ToLower() == name.ToLower()));
		if (matches.Count != 1)
		{
			throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
				.WithData("name", name);
		}

		return matches[0];
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

	private static void ValidatePayrollContract(PayrollContract input)
	{
		if (input.BankDetails != null)
		{
			if (string.IsNullOrWhiteSpace(input.BankDetails.BankName))
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollBankNameRequired);
			}

			if (string.IsNullOrWhiteSpace(input.BankDetails.AccountHolderName))
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollAccountHolderRequired);
			}

			if (!string.IsNullOrWhiteSpace(input.BankDetails.AccountNumber) &&
				!System.Text.RegularExpressions.Regex.IsMatch(input.BankDetails.AccountNumber, @"^\d{8,20}$"))
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidAccountNumber)
					.WithData("accountNumber", input.BankDetails.AccountNumber);
			}

			if (!string.IsNullOrWhiteSpace(input.BankDetails.IfscCode) &&
				!System.Text.RegularExpressions.Regex.IsMatch(input.BankDetails.IfscCode, @"^[A-Z]{4}0[A-Z0-9]{6}$"))
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidIfscCode)
					.WithData("ifscCode", input.BankDetails.IfscCode);
			}
		}

		ValidateSalaryLines(input.EarningComponents, "earning", isEarning: true);
		ValidateSalaryLines(input.DeductionComponents, "deduction", isEarning: false);
	}

	private static void ValidateSalaryLines(List<SalaryComponentContract>? components, string listName, bool isEarning)
	{
		if (components == null || components.Count == 0)
		{
			return;
		}

		foreach (var c in components)
		{
			if (string.IsNullOrWhiteSpace(c.Name))
			{
				throw new BusinessException(
						isEarning
							? HRErrorCodes.EmployeePayrollInvalidEarningComponent
							: HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
					.WithData("list", listName);
			}

			if (c.ComponentId is Guid gid && gid == Guid.Empty)
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollEmptyComponentId)
					.WithData("list", listName);
			}
		}
	}
}
