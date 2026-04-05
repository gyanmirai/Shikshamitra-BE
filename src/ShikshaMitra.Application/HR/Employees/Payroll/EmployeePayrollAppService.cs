using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Payroll;
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
				earning.Name, earning.Description, earning.Amount,
				earning.IsFixed, earning.IsTemp, earning.ComponentType,
				earning.DependOn, isEarning: true);
			await payrollComponentRepository.InsertAsync(component);
		}

		foreach (var deduction in input.Deductions ?? Enumerable.Empty<PayrollComponentDto>())
		{
			var component = new EmployeePayrollComponent(
				guidGenerator.Create(), employeeId,
				deduction.Name, deduction.Description, deduction.Amount,
				deduction.IsFixed, deduction.IsTemp, deduction.ComponentType,
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
				ComponentId = e.Id,
				Amount = e.Amount
			}).ToList() ?? new List<ComponentAmountContract>(),
			DeductionComponents = deductions?.Select(d => new ComponentAmountContract
			{
				ComponentId = d.Id,
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

		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
	}

	}
