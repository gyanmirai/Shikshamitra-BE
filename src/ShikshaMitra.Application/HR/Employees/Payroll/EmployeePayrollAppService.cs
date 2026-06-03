using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

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
	IGuidGenerator guidGenerator,
	EmployeePayrollContractWorkflow payrollContractWorkflow)
	: ApplicationService, IEmployeePayrollAppService
{
	public async Task<EmployeePayrollDetailsDto> GetPayrollDetailsAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		return EmployeePayrollProjection.ToDetailsDto(employee, payrollDetails, earnings, deductions);
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

		return EmployeePayrollProjection.ToContractResponse(
			employeeId,
			payrollDetails,
			earnings,
			deductions,
			earningComponentIdByName,
			deductionComponentIdByName);
	}

	[UnitOfWork]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input)
	{
		await payrollContractWorkflow.UpdateAsync(employeeId, input);
		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
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
			(await earningComponentRepository.GetQueryableAsync()).Where(x => x.IsActive && names.Contains(x.Name)));

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
			(await deductionComponentRepository.GetQueryableAsync()).Where(x => x.IsActive && names.Contains(x.Name)));

		return masters
			.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
	}

}
