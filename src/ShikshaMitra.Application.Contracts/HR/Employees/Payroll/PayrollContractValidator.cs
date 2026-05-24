using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Validates the top-level PayrollContract used by PUT /api/employees/{id}/payroll.
/// </summary>
public class PayrollContractValidator : AbstractValidator<PayrollContract>
{
	public PayrollContractValidator()
	{
		RuleFor(x => x.BankDetails)
			.SetValidator(new BankDetailsContractValidator()!)
			.When(x => x.BankDetails != null);

		RuleFor(x => x.SalaryDetails)
			.SetValidator(new SalaryDetailsContractValidator()!)
			.When(x => x.SalaryDetails != null);

		RuleForEach(x => x.EarningComponents)
			.SetValidator(new SalaryComponentContractValidator("earningComponents"))
			.When(x => x.EarningComponents is { Count: > 0 });

		RuleFor(x => x.EarningComponents)
			.Must(HasNoDuplicateResolutionKeys)
			.When(x => x.EarningComponents is { Count: > 0 })
			.WithMessage("Duplicate earning components (same component id or same name when id is omitted) are not allowed.");

		RuleFor(x => x.EarningComponents)
			.Must(list => list == null || list.Count <= 50)
			.WithMessage("A maximum of 50 earning components are allowed.");

		RuleForEach(x => x.DeductionComponents)
			.SetValidator(new SalaryComponentContractValidator("deductionComponents"))
			.When(x => x.DeductionComponents is { Count: > 0 });

		RuleFor(x => x.DeductionComponents)
			.Must(HasNoDuplicateResolutionKeys)
			.When(x => x.DeductionComponents is { Count: > 0 })
			.WithMessage("Duplicate deduction components (same component id or same name when id is omitted) are not allowed.");

		RuleFor(x => x.DeductionComponents)
			.Must(list => list == null || list.Count <= 50)
			.WithMessage("A maximum of 50 deduction components are allowed.");

		RuleFor(x => x)
			.Must(x => !HasOverlappingMasterIds(x.EarningComponents, x.DeductionComponents))
			.WithMessage("A component id cannot appear in both earning and deduction lists.");

		RuleFor(x => x.PolicyOverride)
			.SetValidator(new EmployeePayrollPolicyOverrideContractValidator()!)
			.When(x => x.PolicyOverride != null);
	}

	private static bool HasNoDuplicateResolutionKeys(
		System.Collections.Generic.List<SalaryComponentContract>? list)
	{
		if (list == null || list.Count <= 1)
		{
			return true;
		}

		var keys = list.Select(c =>
			c.ComponentId.HasValue && c.ComponentId.Value != Guid.Empty
				? "id:" + c.ComponentId.Value.ToString("N")
				: "name:" + (c.Name ?? string.Empty).Trim().ToUpperInvariant());
		return keys.Distinct().Count() == list.Count;
	}

	private static bool HasOverlappingMasterIds(
		System.Collections.Generic.List<SalaryComponentContract>? earnings,
		System.Collections.Generic.List<SalaryComponentContract>? deductions)
	{
		var earningIds = (earnings ?? [])
			.Where(c => c.ComponentId is { } id && id != Guid.Empty)
			.Select(c => c.ComponentId!.Value)
			.ToHashSet();
		return (deductions ?? []).Any(c =>
			c.ComponentId is { } id && id != Guid.Empty && earningIds.Contains(id));
	}
}

public class EmployeePayrollPolicyOverrideContractValidator : AbstractValidator<EmployeePayrollPolicyOverrideContract>
{
	public EmployeePayrollPolicyOverrideContractValidator()
	{
		RuleFor(x => x.AcademicYearId).NotEmpty();
		RuleFor(x => x.PayrollPolicyId).NotEmpty();
		RuleFor(x => x.AttendancePolicyId).NotEmpty();
		RuleFor(x => x.EffectiveFrom).NotEmpty();
		RuleFor(x => x.EffectiveTo)
			.Must((x, effectiveTo) => !effectiveTo.HasValue || effectiveTo.Value.Date >= x.EffectiveFrom.Date)
			.WithMessage("Policy override effective-to must be on or after effective-from.");
	}
}

/// <summary>
/// Validates bank details when provided.
/// </summary>
public class BankDetailsContractValidator : AbstractValidator<BankDetailsContract>
{
	private const int MaxBankNameLength = 100;
	private const int MaxBranchNameLength = 100;
	private const int MaxAccountHolderNameLength = 150;
	private const int MinAccountNumberLength = 8;
	private const int MaxAccountNumberLength = 20;
	private const string IfscCodePattern = @"^[A-Z]{4}0[A-Z0-9]{6}$";

	public BankDetailsContractValidator()
	{
		RuleFor(x => x.BankName)
			.NotEmpty().WithMessage("Bank name is required when bank details are provided.")
			.MaximumLength(MaxBankNameLength).WithMessage($"Bank name must not exceed {MaxBankNameLength} characters.");

		RuleFor(x => x.BranchName)
			.MaximumLength(MaxBranchNameLength).WithMessage($"Branch name must not exceed {MaxBranchNameLength} characters.")
			.When(x => x.BranchName != null);

		RuleFor(x => x.AccountHolderName)
			.NotEmpty().WithMessage("Account holder name is required when bank details are provided.")
			.MaximumLength(MaxAccountHolderNameLength).WithMessage($"Account holder name must not exceed {MaxAccountHolderNameLength} characters.");

		RuleFor(x => x.AccountNumber)
			.NotEmpty().WithMessage("Account number is required when bank details are provided.")
			.MinimumLength(MinAccountNumberLength).WithMessage($"Account number must be at least {MinAccountNumberLength} characters.")
			.MaximumLength(MaxAccountNumberLength).WithMessage($"Account number must not exceed {MaxAccountNumberLength} characters.")
			.Matches(@"^\d+$").WithMessage("Account number must contain only digits.");

		RuleFor(x => x.IfscCode)
			.NotEmpty().WithMessage("IFSC code is required when bank details are provided.")
			.Matches(IfscCodePattern).WithMessage("IFSC code must follow the format: 4 uppercase letters, a zero, then 6 alphanumeric characters (e.g., SBIN0001234).");
	}
}

/// <summary>
/// Validates salary/statutory details when provided.
/// </summary>
public class SalaryDetailsContractValidator : AbstractValidator<SalaryDetailsContract>
{
	private const decimal MaxHours = 744m;
	private const int MaxPunchIdLength = 20;
	private const string EpfNumberPattern = @"^[A-Z]{2}/\d{5}/\d{7}$";
	private const string UanNumberPattern = @"^\d{12}$";

	public SalaryDetailsContractValidator()
	{
		RuleFor(x => x.TotalWorkingHourPerMonth)
			.GreaterThanOrEqualTo(0)
			.WithMessage("Total working hours per month cannot be negative.")
			.LessThanOrEqualTo(MaxHours)
			.WithMessage($"Total working hours per month must not exceed {MaxHours}.")
			.When(x => x.TotalWorkingHourPerMonth.HasValue);

		RuleFor(x => x.PfRuleId)
			.Must(id => id != Guid.Empty)
			.WithMessage("PF Rule ID must be a valid non-empty GUID.")
			.When(x => x.PfRuleId.HasValue);

		RuleFor(x => x.PunchId)
			.MaximumLength(MaxPunchIdLength)
			.WithMessage($"Punch ID must not exceed {MaxPunchIdLength} characters.")
			.Matches(@"^[a-zA-Z0-9\-_]+$")
			.WithMessage("Punch ID may only contain letters, digits, hyphens, and underscores.")
			.When(x => !string.IsNullOrEmpty(x.PunchId));

		RuleFor(x => x.EpfNumber)
			.Matches(EpfNumberPattern)
			.WithMessage("EPF number must follow the format: XX/XXXXX/XXXXXXX (e.g., AP/12345/1234567).")
			.When(x => !string.IsNullOrEmpty(x.EpfNumber));

		RuleFor(x => x.UanNumber)
			.Matches(UanNumberPattern)
			.WithMessage("UAN number must be exactly 12 digits.")
			.When(x => !string.IsNullOrEmpty(x.UanNumber));
	}
}

/// <summary>
/// Validates a single earning or deduction line on the payroll contract.
/// </summary>
public class SalaryComponentContractValidator : AbstractValidator<SalaryComponentContract>
{
	private const int MaxNameLength = 100;
	private const decimal MaxComponentAmount = 99_999_999.99m;

	public SalaryComponentContractValidator(string listName)
	{
		RuleFor(x => x.Name)
			.NotEmpty().WithMessage($"Component name in {listName} is required.")
			.MaximumLength(MaxNameLength).WithMessage($"Component name in {listName} must not exceed {MaxNameLength} characters.");

		RuleFor(x => x.ComponentId)
			.Must(id => !id.HasValue || id.Value != Guid.Empty)
			.WithMessage($"Component ID in {listName} must be a valid non-empty GUID when supplied.");

		RuleFor(x => x.Amount)
			.GreaterThanOrEqualTo(0)
			.WithMessage($"Component amount in {listName} cannot be negative when supplied.")
			.LessThanOrEqualTo(MaxComponentAmount)
			.WithMessage($"Component amount in {listName} must not exceed {MaxComponentAmount}.")
			.When(x => x.Amount.HasValue);
	}
}
