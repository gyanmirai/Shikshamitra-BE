using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
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
[Authorize]
public class EmployeePayrollAppService(
	IEmployeeRepository employeeRepository,
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository,
	IEmployeeCacheManager employeeCacheManager,
	IGuidGenerator guidGenerator,
	EmployeePayrollContractWorkflow payrollContractWorkflow,
	IEmployeeAuthorizationService employeeAuthorizationService,
	PayrollSnapshotReviewInvalidator payrollSnapshotReviewInvalidator, ShikshaMitra.HR.Payroll.Statutory.EmployeeStatutoryProfileAppService statutoryProfiles)
	: ApplicationService, IEmployeePayrollAppService
{
	[UnitOfWork]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add)]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
	public async Task UpdateProfileDetailsAsync(Guid employeeId, EmployeePayrollProfileDetailsPatch input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		var validation = await new EmployeePayrollProfileDetailsPatchValidator().ValidateAsync(input);
		if (!validation.IsValid)
			throw new Volo.Abp.Validation.AbpValidationException("Invalid payroll profile details.",
				validation.Errors.Select(x => new System.ComponentModel.DataAnnotations.ValidationResult(x.ErrorMessage, new[] { x.PropertyName })).ToList());

		var details = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var isNew = details == null;
		details ??= new EmployeePayrollDetails(guidGenerator.Create(), employeeId);
		if (input.BankDetails is { } bank)
		{
			details.BankName = bank.BankName;
			details.BranchName = bank.BranchName;
			details.AccountHolderName = bank.AccountHolderName;
			details.AccountNumber = bank.AccountNumber;
			details.IfscCode = bank.IfscCode;
		}
		if (input.StatutoryIdentifiers != null) throw new BusinessException("HR:Payroll:UseStatutoryProfile");
		if (input.AttendanceIntegration is { } attendance)
		{
			details.PunchId = attendance.PunchId;
			details.TotalWorkingHourPerMonth = attendance.TotalWorkingHourPerMonth;
		}
		if (isNew) await payrollDetailsRepository.InsertAsync(details);
		else await payrollDetailsRepository.UpdateAsync(details);
		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
		await payrollSnapshotReviewInvalidator.InvalidateUnlockedForEmployeesAsync(new[] { employeeId });
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<EmployeePayrollDetailsDto> GetPayrollDetailsAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(employeeId);
		var employee = await employeeRepository.GetAsync(employeeId);
		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		var dto = EmployeePayrollProjection.ToDetailsDto(employee, payrollDetails, earnings, deductions);
		var statutory = await statutoryProfiles.GetProjectionAsync(employeeId);
		dto.PayrollDetails.EpfNumber = statutory.Profile.PfNumber;
		dto.PayrollDetails.UanNumber = statutory.Profile.UanNumber;
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
	public async Task UpdatePayrollDetailsAsync(Guid employeeId, UpdateEmployeePayrollDetailsDto input)
	{
		if (input.PayrollDetails == null)
		{
			throw new BusinessException(HRErrorCodes.EmployeePayrollDetailsRequired);
		}

		await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(employeeId);
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		await employeeRepository.GetAsync(employeeId);

		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var identifiers = (await statutoryProfiles.GetProjectionAsync(employeeId)).Profile;
		if (ShikshaMitra.HR.Payroll.Statutory.EmployeeStatutoryProfileVersion.NormalizeNumber(input.PayrollDetails.EpfNumber) != identifiers.PfNumber ||
			ShikshaMitra.HR.Payroll.Statutory.EmployeeStatutoryProfileVersion.NormalizeNumber(input.PayrollDetails.UanNumber) != identifiers.UanNumber)
			throw new BusinessException("HR:Payroll:UseStatutoryProfile");

		if (payrollDetails == null)
		{
			payrollDetails = new EmployeePayrollDetails(guidGenerator.Create(), employeeId);
			payrollDetails.UpdateDetails(
				input.PayrollDetails.BankName, input.PayrollDetails.BranchName,
				input.PayrollDetails.AccountHolderName, input.PayrollDetails.AccountNumber,
				input.PayrollDetails.IfscCode, input.PayrollDetails.PfRule,
				payrollDetails.EpfNumber, payrollDetails.UanNumber,
				input.PayrollDetails.PunchId, input.PayrollDetails.TotalWorkingHourPerMonth);
			await payrollDetailsRepository.InsertAsync(payrollDetails);
		}
		else
		{
			payrollDetails.UpdateDetails(
				input.PayrollDetails.BankName, input.PayrollDetails.BranchName,
				input.PayrollDetails.AccountHolderName, input.PayrollDetails.AccountNumber,
				input.PayrollDetails.IfscCode, input.PayrollDetails.PfRule,
				payrollDetails.EpfNumber, payrollDetails.UanNumber,
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
		await payrollSnapshotReviewInvalidator.InvalidateUnlockedForEmployeesAsync(new[] { employeeId });
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<EmployeePayrollResponse> GetPayrollContractAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeScopeAccessAsync(employeeId);
		await employeeRepository.GetAsync(employeeId);

		var payrollDetails = await payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		var earnings = await payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);
		var earningComponentIdByName = await GetEarningComponentIdsByNameAsync(earnings);
		var deductionComponentIdByName = await GetDeductionComponentIdsByNameAsync(deductions);

		var response = EmployeePayrollProjection.ToContractResponse(
			employeeId,
			payrollDetails,
			earnings,
			deductions,
			earningComponentIdByName,
			deductionComponentIdByName);
		var statutory = await statutoryProfiles.GetProjectionAsync(employeeId);
		response.SalaryDetails!.EpfNumber = statutory.Profile.PfNumber;
		response.SalaryDetails.UanNumber = statutory.Profile.UanNumber;
		return response;
	}

	[UnitOfWork]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add)]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
	public async Task UpdatePayrollContractAsync(Guid employeeId, PayrollContract input)
	{
		await payrollContractWorkflow.UpdateAsync(employeeId, input);
		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);
		await payrollSnapshotReviewInvalidator.InvalidateUnlockedForEmployeesAsync(new[] { employeeId });
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
