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
				Id = e.Id,
				ComponentId = e.ComponentId
					?? (ShouldInferMasterComponentByName(e) && earningComponentIdByName.TryGetValue(e.Name, out var masterEarningId) ? masterEarningId : null),
				Name = e.Name,
				Amount = e.Amount,
				IsFixed = e.IsFixed,
				IsTemp = e.IsTemp
			}).ToList() ?? [],
			DeductionComponents = deductions?.Select(d => new SalaryComponentContract
			{
				Id = d.Id,
				ComponentId = d.ComponentId
					?? (ShouldInferMasterComponentByName(d) && deductionComponentIdByName.TryGetValue(d.Name, out var masterDedId) ? masterDedId : null),
				Name = d.Name,
				Amount = d.Amount,
				IsFixed = d.IsFixed,
				IsTemp = d.IsTemp
			}).ToList() ?? []
		};
	}

	public async Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input)
	{
		ValidatePayrollContract(input);

		await employeeRepository.GetAsync(employeeId);
		var existingEarnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var existingDeductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		var earningLines = new List<ResolvedPayrollLine<EarningComponent>>();
		foreach (var line in input.EarningComponents ?? [])
		{
			earningLines.Add(await ResolveEarningLineAsync(line, existingEarnings));
		}

		var deductionLines = new List<ResolvedPayrollLine<DeductionComponent>>();
		foreach (var line in input.DeductionComponents ?? [])
		{
			deductionLines.Add(await ResolveDeductionLineAsync(line, existingDeductions));
		}

		var earningIds = earningLines.Where(x => x.Master is not null).Select(x => x.Master!.Id).ToHashSet();
		var overlap = deductionLines
			.Where(x => x.Master is not null)
			.Select(x => x.Master!.Id)
			.Where(earningIds.Contains)
			.ToList();
		if (overlap.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.EmployeePayrollComponentInBothLists)
				.WithData("overlappingComponentIds", string.Join(",", overlap));
		}

		foreach (var resolved in earningLines)
		{
			var amount = ResolveEarningLineAmount(resolved);
			if (!amount.HasValue || amount <= 0)
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollComponentAmountInvalid)
					.WithData("componentId", resolved.Master?.Id)
					.WithData("amount", amount)
					.WithData("list", "earning");
			}
		}

		foreach (var resolved in deductionLines)
		{
			var amount = ResolveDeductionLineAmount(resolved);
			if (!amount.HasValue || amount <= 0)
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollComponentAmountInvalid)
					.WithData("componentId", resolved.Master?.Id)
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

		foreach (var resolved in earningLines)
		{
			var line = resolved.Line;
			var master = resolved.Master;
			var amount = ResolveEarningLineAmount(resolved)!.Value;
			var isFixed = line.IsFixed
				?? resolved.ExistingPayrollComponent?.IsFixed
				?? (master?.CalculationType == CalculationType.Amount);

			var earningComponent = new EmployeePayrollComponent(
				guidGenerator.Create(),
				employeeId,
				master?.Name ?? line.Name.Trim(),
				master?.Id,
				master?.Description ?? resolved.ExistingPayrollComponent?.Description,
				amount,
				isFixed,
				isTemp: line.IsTemp ?? resolved.ExistingPayrollComponent?.IsTemp ?? master is null,
				applicableMonth: null,
				master is null
					? resolved.ExistingPayrollComponent?.ComponentType ?? SalaryComponentType.Fixed
					: master.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				master is null ? resolved.ExistingPayrollComponent?.DependOn : MapDependency(master.DependOn),
				isEarning: true);

			await payrollComponentRepository.InsertAsync(earningComponent);
		}

		foreach (var resolved in deductionLines)
		{
			var line = resolved.Line;
			var master = resolved.Master;
			var amount = ResolveDeductionLineAmount(resolved)!.Value;
			var isFixed = line.IsFixed
				?? resolved.ExistingPayrollComponent?.IsFixed
				?? (master?.CalculationType == CalculationType.Amount);

			var deductionComponent = new EmployeePayrollComponent(
				guidGenerator.Create(),
				employeeId,
				master?.Name ?? line.Name.Trim(),
				master?.Id,
				master?.Description ?? resolved.ExistingPayrollComponent?.Description,
				amount,
				isFixed,
				isTemp: line.IsTemp ?? resolved.ExistingPayrollComponent?.IsTemp ?? master is null,
				applicableMonth: null,
				master is null
					? resolved.ExistingPayrollComponent?.ComponentType ?? SalaryComponentType.Fixed
					: master.CalculationType == CalculationType.Amount
					? SalaryComponentType.Fixed
					: SalaryComponentType.Percentage,
				master is null ? resolved.ExistingPayrollComponent?.DependOn : MapDependency(master.DependOn),
				isEarning: false);

			await payrollComponentRepository.InsertAsync(deductionComponent);
		}

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
	}

	private async Task<ResolvedPayrollLine<EarningComponent>> ResolveEarningLineAsync(
		SalaryComponentContract line,
		IEnumerable<EmployeePayrollComponent> existingEarnings)
	{
		var existingPayrollRow = FindExistingPayrollRow(line, existingEarnings);
		if (line.ComponentId is { } cid && cid != Guid.Empty)
		{
			try
			{
				return new ResolvedPayrollLine<EarningComponent>(
					line,
					await earningComponentRepository.GetAsync(cid),
					existingPayrollRow);
			}
			catch (Volo.Abp.Domain.Entities.EntityNotFoundException)
			{
				if (existingPayrollRow?.ComponentId is { } masterId && masterId != Guid.Empty)
				{
					try
					{
						return new ResolvedPayrollLine<EarningComponent>(
							line,
							await earningComponentRepository.GetAsync(masterId),
							existingPayrollRow);
					}
					catch (Volo.Abp.Domain.Entities.EntityNotFoundException)
					{
						// Fall through to the existing business error with the submitted id.
					}
				}

				if (existingPayrollRow is not null && IsCustomPayrollRow(existingPayrollRow) && cid == existingPayrollRow.Id)
				{
					return new ResolvedPayrollLine<EarningComponent>(line, null, existingPayrollRow);
				}

				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidEarningComponent)
					.WithData("componentId", cid);
			}
		}

		if (line.IsTemp == true || (existingPayrollRow is not null && IsCustomPayrollRow(existingPayrollRow)))
		{
			return new ResolvedPayrollLine<EarningComponent>(line, null, existingPayrollRow);
		}

		var name = (line.Name ?? string.Empty).Trim();
		var matches = await AsyncExecuter.ToListAsync(
			(await earningComponentRepository.GetQueryableAsync())
				.Where(x => x.Name.ToLower() == name.ToLower()));
		if (matches.Count != 1)
		{
			return new ResolvedPayrollLine<EarningComponent>(line, null, existingPayrollRow);
		}

		return new ResolvedPayrollLine<EarningComponent>(line, matches[0], existingPayrollRow);
	}

	private async Task<ResolvedPayrollLine<DeductionComponent>> ResolveDeductionLineAsync(
		SalaryComponentContract line,
		IEnumerable<EmployeePayrollComponent> existingDeductions)
	{
		var existingPayrollRow = FindExistingPayrollRow(line, existingDeductions);
		if (line.ComponentId is { } cid && cid != Guid.Empty)
		{
			try
			{
				return new ResolvedPayrollLine<DeductionComponent>(
					line,
					await deductionComponentRepository.GetAsync(cid),
					existingPayrollRow);
			}
			catch (Volo.Abp.Domain.Entities.EntityNotFoundException)
			{
				if (existingPayrollRow?.ComponentId is { } masterId && masterId != Guid.Empty)
				{
					try
					{
						return new ResolvedPayrollLine<DeductionComponent>(
							line,
							await deductionComponentRepository.GetAsync(masterId),
							existingPayrollRow);
					}
					catch (Volo.Abp.Domain.Entities.EntityNotFoundException)
					{
						// Fall through to the existing business error with the submitted id.
					}
				}

				if (existingPayrollRow is not null && IsCustomPayrollRow(existingPayrollRow) && cid == existingPayrollRow.Id)
				{
					return new ResolvedPayrollLine<DeductionComponent>(line, null, existingPayrollRow);
				}

				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
					.WithData("componentId", cid);
			}
		}

		if (line.IsTemp == true || (existingPayrollRow is not null && IsCustomPayrollRow(existingPayrollRow)))
		{
			return new ResolvedPayrollLine<DeductionComponent>(line, null, existingPayrollRow);
		}

		var name = (line.Name ?? string.Empty).Trim();
		var matches = await AsyncExecuter.ToListAsync(
			(await deductionComponentRepository.GetQueryableAsync())
				.Where(x => x.Name.ToLower() == name.ToLower()));
		if (matches.Count != 1)
		{
			return new ResolvedPayrollLine<DeductionComponent>(line, null, existingPayrollRow);
		}

		return new ResolvedPayrollLine<DeductionComponent>(line, matches[0], existingPayrollRow);
	}

	private static EmployeePayrollComponent? FindExistingPayrollRow(
		SalaryComponentContract line,
		IEnumerable<EmployeePayrollComponent> existingComponents)
	{
		if (line.Id is { } id && id != Guid.Empty)
		{
			var byId = existingComponents.FirstOrDefault(x => x.Id == id);
			if (byId is not null)
			{
				return byId;
			}
		}

		if (line.ComponentId is { } componentId && componentId != Guid.Empty)
		{
			return existingComponents.FirstOrDefault(x => x.Id == componentId);
		}

		return null;
	}

	private static bool IsCustomPayrollRow(EmployeePayrollComponent component)
	{
		return component.IsTemp || component.ComponentId is null || component.ComponentId == Guid.Empty;
	}

	private static bool ShouldInferMasterComponentByName(EmployeePayrollComponent component)
	{
		return !component.IsTemp && (component.ComponentId is null || component.ComponentId == Guid.Empty);
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

	private static decimal ResolveEarningComponentAmount(EarningComponent master)
	{
		return master.CalculationType == CalculationType.Amount
			? master.Amount ?? 0m
			: master.Percentage ?? 0m;
	}

	private static decimal ResolveDeductionComponentAmount(DeductionComponent master)
	{
		return master.CalculationType == CalculationType.Amount
			? master.Amount ?? 0m
			: master.Percentage ?? 0m;
	}

	private static decimal? ResolveEarningLineAmount(ResolvedPayrollLine<EarningComponent> resolved)
	{
		return resolved.Line.Amount
			?? (resolved.Master is not null
				? ResolveEarningComponentAmount(resolved.Master)
				: resolved.ExistingPayrollComponent?.Amount);
	}

	private static decimal? ResolveDeductionLineAmount(ResolvedPayrollLine<DeductionComponent> resolved)
	{
		return resolved.Line.Amount
			?? (resolved.Master is not null
				? ResolveDeductionComponentAmount(resolved.Master)
				: resolved.ExistingPayrollComponent?.Amount);
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

	private sealed record ResolvedPayrollLine<TMaster>(
		SalaryComponentContract Line,
		TMaster? Master,
		EmployeePayrollComponent? ExistingPayrollComponent)
		where TMaster : class;
}
