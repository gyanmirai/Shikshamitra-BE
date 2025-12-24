using ShikshaMitra.HR.Employees.Payroll;
using System;
using System.Linq;

namespace ShikshaMitra.HR.Employees.PayrollInfo;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class PayrollContractMapper
{
	/// <summary>
	/// Converts API request to internal update DTO
	/// </summary>
	public static UpdateEmployeePayrollDetailsDto ToUpdateDto(PayrollContract request)
	{
		var payrollDetails = new PayrollDetailsDto();

		if (request.BankDetails != null)
		{
			payrollDetails.BankName = request.BankDetails.BankName;
			payrollDetails.BranchName = request.BankDetails.BranchName;
			payrollDetails.AccountHolderName = request.BankDetails.AccountHolderName;
			payrollDetails.AccountNumber = request.BankDetails.AccountNumber;
			payrollDetails.IfscCode = request.BankDetails.IfscCode;
		}

		if (request.SalaryDetails != null)
		{
			payrollDetails.PfRule = request.SalaryDetails.PfRuleId?.ToString();
			payrollDetails.EpfNumber = request.SalaryDetails.EpfNumber;
			payrollDetails.UanNumber = request.SalaryDetails.UanNumber;
			payrollDetails.BasicSalary = request.SalaryDetails.BasicSalary;
			payrollDetails.PunchId = request.SalaryDetails.PunchId;
			payrollDetails.TdsPercentage = request.SalaryDetails.TdsPercentage;
			payrollDetails.TdsAmount = request.SalaryDetails.TdsAmount;
		}

		return new UpdateEmployeePayrollDetailsDto
		{
			PayrollDetails = payrollDetails,
			Earnings = request.EarningComponents.Select(ToPayrollComponentDto).ToList(),
			Deductions = request.DeductionComponents.Select(ToPayrollComponentDto).ToList()
		};
	}

	/// <summary>
	/// Converts internal DTO to API response
	/// </summary>
	public static PayrollContract ToResponse(EmployeePayrollDetailsDto dto)
	{
		var bankDetails = new BankDetailsInfo
		{
			BankName = dto.PayrollDetails.BankName,
			BranchName = dto.PayrollDetails.BranchName,
			AccountHolderName = dto.PayrollDetails.AccountHolderName,
			AccountNumber = dto.PayrollDetails.AccountNumber,
			IfscCode = dto.PayrollDetails.IfscCode
		};

		var salaryDetails = new SalaryDetailsInfo
		{
			PfRuleId = Guid.TryParse(dto.PayrollDetails.PfRule, out var pfRuleId) ? pfRuleId : null,
			EpfNumber = dto.PayrollDetails.EpfNumber,
			UanNumber = dto.PayrollDetails.UanNumber,
			BasicSalary = dto.PayrollDetails.BasicSalary,
			PunchId = dto.PayrollDetails.PunchId,
			TdsPercentage = dto.PayrollDetails.TdsPercentage,
			TdsAmount = dto.PayrollDetails.TdsAmount
		};

		return new PayrollContract
		{
			BankDetails = bankDetails,
			SalaryDetails = salaryDetails,
			EarningComponents = dto.Earnings.Select(ToComponentAmountInfo).ToList(),
			DeductionComponents = dto.Deductions.Select(ToComponentAmountInfo).ToList()
		};
	}

	private static PayrollComponentDto ToPayrollComponentDto(ComponentAmountInfo info)
	{
		return new PayrollComponentDto
		{
			Name = info.ComponentId.ToString(),
			Amount = info.Amount,
			ComponentType = SalaryComponentType.Fixed,
			IsFixed = true,
			IsTemp = false
		};
	}

	private static ComponentAmountInfo ToComponentAmountInfo(PayrollComponentDto dto)
	{
		return new ComponentAmountInfo
		{
			ComponentId = Guid.TryParse(dto.Name, out var id) ? id : Guid.Empty,
			Amount = dto.Amount
		};
	}
}
