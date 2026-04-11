using System;
using System.Linq;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Validates the top-level PayrollContract used by the PUT /api/employees/{id}/payroll endpoint.
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

        // ── Earning components ──────────────────────────────────
        RuleForEach(x => x.EarningComponents)
            .SetValidator(new ComponentAmountContractValidator("earningComponents"))
            .When(x => x.EarningComponents != null && x.EarningComponents.Count > 0);

        RuleFor(x => x.EarningComponents)
            .Must(list => list == null || list.Select(c => c.ComponentId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate earning component IDs are not allowed.");

        RuleFor(x => x.EarningComponents)
            .Must(list => list == null || list.Count <= 50)
            .WithMessage("A maximum of 50 earning components are allowed.");

        // ── Deduction components ────────────────────────────────
        RuleForEach(x => x.DeductionComponents)
            .SetValidator(new ComponentAmountContractValidator("deductionComponents"))
            .When(x => x.DeductionComponents != null && x.DeductionComponents.Count > 0);

        RuleFor(x => x.DeductionComponents)
            .Must(list => list == null || list.Select(c => c.ComponentId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate deduction component IDs are not allowed.");

        RuleFor(x => x.DeductionComponents)
            .Must(list => list == null || list.Count <= 50)
            .WithMessage("A maximum of 50 deduction components are allowed.");

        // ── Cross-field: no component ID appears in both earning and deduction lists
        RuleFor(x => x)
            .Must(x =>
            {
                var earningIds = (x.EarningComponents ?? []).Select(c => c.ComponentId).ToHashSet();
                var deductionIds = (x.DeductionComponents ?? []).Select(c => c.ComponentId);
                return !deductionIds.Any(earningIds.Contains);
            })
            .WithMessage("A component ID cannot appear in both earning and deduction lists.");
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
        // When bank details are provided, enforce completeness
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
    private const decimal MaxSalaryAmount = 99_999_999.99m;
    private const int MaxPunchIdLength = 20;
    private const string EpfNumberPattern = @"^[A-Z]{2}/\d{5}/\d{7}$";
    private const string UanNumberPattern = @"^\d{12}$";

    public SalaryDetailsContractValidator()
    {
        // ── Basic salary ────────────────────────────────────────
        RuleFor(x => x.BasicSalary)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Basic salary cannot be negative.")
            .LessThanOrEqualTo(MaxSalaryAmount)
            .WithMessage($"Basic salary must not exceed {MaxSalaryAmount}.")
            .When(x => x.BasicSalary.HasValue);

        // ── TDS percentage ──────────────────────────────────────
        RuleFor(x => x.TdsPercentage)
            .InclusiveBetween(0, 100)
            .WithMessage("TDS percentage must be between 0 and 100.")
            .When(x => x.TdsPercentage.HasValue);

        // ── TDS amount ──────────────────────────────────────────
        RuleFor(x => x.TdsAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("TDS amount cannot be negative.")
            .LessThanOrEqualTo(MaxSalaryAmount)
            .WithMessage($"TDS amount must not exceed {MaxSalaryAmount}.")
            .When(x => x.TdsAmount.HasValue);

        // ── Punch ID ────────────────────────────────────────────
        RuleFor(x => x.PunchId)
            .MaximumLength(MaxPunchIdLength)
            .WithMessage($"Punch ID must not exceed {MaxPunchIdLength} characters.")
            .Matches(@"^[a-zA-Z0-9\-_]+$")
            .WithMessage("Punch ID may only contain letters, digits, hyphens, and underscores.")
            .When(x => !string.IsNullOrEmpty(x.PunchId));

        // ── EPF number format ───────────────────────────────────
        RuleFor(x => x.EpfNumber)
            .Matches(EpfNumberPattern)
            .WithMessage("EPF number must follow the format: XX/XXXXX/XXXXXXX (e.g., AP/12345/1234567).")
            .When(x => !string.IsNullOrEmpty(x.EpfNumber));

        // ── UAN number format ───────────────────────────────────
        RuleFor(x => x.UanNumber)
            .Matches(UanNumberPattern)
            .WithMessage("UAN number must be exactly 12 digits.")
            .When(x => !string.IsNullOrEmpty(x.UanNumber));

        // ── PF Rule ID must not be empty Guid when supplied ─────
        RuleFor(x => x.PfRuleId)
            .Must(id => id != Guid.Empty)
            .WithMessage("PF Rule ID must be a valid non-empty GUID.")
            .When(x => x.PfRuleId.HasValue);
    }
}

/// <summary>
/// Validates a single earning or deduction component line item.
/// </summary>
public class ComponentAmountContractValidator : AbstractValidator<ComponentAmountContract>
{
    private const decimal MaxComponentAmount = 99_999_999.99m;

    public ComponentAmountContractValidator(string listName = "components")
    {
        RuleFor(x => x.ComponentId)
            .NotEqual(Guid.Empty)
            .WithMessage($"Component ID in {listName} must be a valid non-empty GUID.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage($"Component amount in {listName} must be greater than zero.")
            .LessThanOrEqualTo(MaxComponentAmount)
            .WithMessage($"Component amount in {listName} must not exceed {MaxComponentAmount}.");
    }
}
