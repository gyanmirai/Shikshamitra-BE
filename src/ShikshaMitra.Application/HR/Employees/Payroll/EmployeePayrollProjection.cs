using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Employees.Payroll;

internal static class EmployeePayrollProjection
{
	public static EmployeePayrollDetailsDto ToDetailsDto(
		Employee employee,
		EmployeePayrollDetails? payrollDetails,
		IEnumerable<EmployeePayrollComponent>? earnings,
		IEnumerable<EmployeePayrollComponent>? deductions)
	{
		return new EmployeePayrollDetailsDto
		{
			EmployeeId = employee.EmployeeCode,
			PayrollDetails = payrollDetails != null ? ToPayrollDetailsDto(payrollDetails) : new PayrollDetailsDto(),
			Earnings = (earnings ?? Enumerable.Empty<EmployeePayrollComponent>()).Select(ToComponentDto).ToList(),
			Deductions = (deductions ?? Enumerable.Empty<EmployeePayrollComponent>()).Select(ToComponentDto).ToList()
		};
	}

	public static EmployeePayrollResponse ToContractResponse(
		Guid employeeId,
		EmployeePayrollDetails? payrollDetails,
		IEnumerable<EmployeePayrollComponent>? earnings,
		IEnumerable<EmployeePayrollComponent>? deductions,
		IReadOnlyDictionary<string, Guid> earningComponentIdByName,
		IReadOnlyDictionary<string, Guid> deductionComponentIdByName)
	{
		return new EmployeePayrollResponse
		{
			Id = employeeId,
			BankDetails = new BankDetailsContract
			{
				BankName = payrollDetails?.BankName,
				BranchName = payrollDetails?.BranchName,
				AccountHolderName = payrollDetails?.AccountHolderName,
				AccountNumber = payrollDetails?.AccountNumber,
				IfscCode = payrollDetails?.IfscCode
			},
			SalaryDetails = new SalaryDetailsContract
			{
				PfRuleId = payrollDetails != null
					&& !string.IsNullOrEmpty(payrollDetails.PfRule)
					&& Guid.TryParse(payrollDetails.PfRule, out var pfRuleId)
					? pfRuleId
					: null,
				EpfNumber = payrollDetails?.EpfNumber,
				UanNumber = payrollDetails?.UanNumber,
				PunchId = payrollDetails?.PunchId,
				TotalWorkingHourPerMonth = payrollDetails?.TotalWorkingHourPerMonth,
				QuotedSalaryBasis = payrollDetails?.QuotedSalaryBasis,
				QuotedMonthlyAmount = payrollDetails?.QuotedMonthlyAmount,
				SourceSalaryTemplateId = payrollDetails?.SourceSalaryTemplateId,
				SourceSalaryTemplateVersionId = payrollDetails?.SourceSalaryTemplateVersionId,
				SourceSalaryTemplateVersion = payrollDetails?.SourceSalaryTemplateVersion,
				SalaryGeneratedAt = payrollDetails?.SalaryGeneratedAt,
				WasModifiedAfterGeneration = payrollDetails?.WasModifiedAfterGeneration ?? false
			},
			EarningComponents = (earnings ?? Enumerable.Empty<EmployeePayrollComponent>())
				.Select(e => ToSalaryComponentContract(e, earningComponentIdByName))
				.ToList(),
			DeductionComponents = (deductions ?? Enumerable.Empty<EmployeePayrollComponent>())
				.Select(d => ToSalaryComponentContract(d, deductionComponentIdByName))
				.ToList()
		};
	}

	private static PayrollDetailsDto ToPayrollDetailsDto(EmployeePayrollDetails payrollDetails)
	{
		return new PayrollDetailsDto
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
		};
	}

	private static PayrollComponentDto ToComponentDto(EmployeePayrollComponent component)
	{
		return new PayrollComponentDto
		{
			Name = component.Name,
			Description = component.Description,
			Amount = component.Amount,
			IsFixed = component.IsFixed,
			IsTemp = component.IsTemp,
			ComponentType = component.ComponentType,
			DependOn = component.DependOn
		};
	}

	private static SalaryComponentContract ToSalaryComponentContract(
		EmployeePayrollComponent component,
		IReadOnlyDictionary<string, Guid> componentIdByName)
	{
		return new SalaryComponentContract
		{
			Id = component.Id,
			ComponentId = component.ComponentId
				?? (EmployeePayrollRules.ShouldInferMasterComponentByName(component) &&
				    componentIdByName.TryGetValue(component.Name, out var masterId)
					? masterId
					: null),
			Name = component.Name,
			Amount = component.Amount,
			IsFixed = component.IsFixed,
			IsTemp = component.IsTemp,
			UsesMasterValue = component.UsesMasterValue,
			ApplicableMonth = component.ApplicableMonth,
			SemanticType = component.SemanticType,
			StatutoryRuleId = component.StatutoryRuleId
		};
	}
}
