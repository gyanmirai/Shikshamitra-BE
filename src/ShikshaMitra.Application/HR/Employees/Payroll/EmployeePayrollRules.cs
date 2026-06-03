using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.Payroll;

internal static class EmployeePayrollRules
{
	public static bool IsCustomPayrollRow(EmployeePayrollComponent component) =>
		component.IsTemp || component.ComponentId is null || component.ComponentId == Guid.Empty;

	public static bool ShouldInferMasterComponentByName(EmployeePayrollComponent component) =>
		!component.IsTemp && (component.ComponentId is null || component.ComponentId == Guid.Empty);

	public static void ValidatePayrollContract(PayrollContract input)
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
				!Regex.IsMatch(input.BankDetails.AccountNumber, @"^\d{8,20}$"))
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidAccountNumber)
					.WithData("accountNumber", input.BankDetails.AccountNumber);
			}

			if (!string.IsNullOrWhiteSpace(input.BankDetails.IfscCode) &&
				!Regex.IsMatch(input.BankDetails.IfscCode, @"^[A-Z]{4}0[A-Z0-9]{6}$"))
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidIfscCode)
					.WithData("ifscCode", input.BankDetails.IfscCode);
			}
		}

		ValidateSalaryLines(input.EarningComponents, "earning", isEarning: true);
		ValidateSalaryLines(input.DeductionComponents, "deduction", isEarning: false);
	}

	public static void EnsureActiveMaster(EarningComponent master, string errorCode, Guid componentId)
	{
		if (master.IsActive)
		{
			return;
		}

		throw new BusinessException(errorCode).WithData("componentId", componentId);
	}

	public static void EnsureActiveMaster(DeductionComponent master, string errorCode, Guid componentId)
	{
		if (master.IsActive)
		{
			return;
		}

		throw new BusinessException(errorCode).WithData("componentId", componentId);
	}

	private static void ValidateSalaryLines(List<SalaryComponentContract>? components, string listName, bool isEarning)
	{
		if (components == null || components.Count == 0)
		{
			return;
		}

		foreach (var component in components)
		{
			if (string.IsNullOrWhiteSpace(component.Name))
			{
				throw new BusinessException(
						isEarning
							? HRErrorCodes.EmployeePayrollInvalidEarningComponent
							: HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
					.WithData("list", listName);
			}

			if (component.ComponentId is Guid componentId && componentId == Guid.Empty)
			{
				throw new BusinessException(HRErrorCodes.EmployeePayrollEmptyComponentId)
					.WithData("list", listName);
			}
		}
	}
}
