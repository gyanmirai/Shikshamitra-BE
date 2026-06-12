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

		RuleFor(x => x.PolicyOverrideAction)
			.Must(action => string.IsNullOrWhiteSpace(action) ||
			                string.Equals(action, "upsert", StringComparison.OrdinalIgnoreCase) ||
			                string.Equals(action, "disable", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Policy override action must be either 'upsert' or 'disable'.");

		RuleFor(x => x.PolicyOverride)
			.NotNull()
			.When(x => string.Equals(x.PolicyOverrideAction, "upsert", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Policy override details are required when action is upsert.");

		RuleFor(x => x.PolicyOverride)
			.NotNull()
			.When(x => string.Equals(x.PolicyOverrideAction, "disable", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Policy override academic year is required when action is disable.");

		RuleFor(x => x.PolicyOverride!.AcademicYearId)
			.NotEmpty()
			.When(x => string.Equals(x.PolicyOverrideAction, "disable", StringComparison.OrdinalIgnoreCase) &&
			           x.PolicyOverride != null)
			.WithMessage("Academic year is required when disabling policy override.");

		RuleFor(x => x.PolicyOverride!.EffectiveFrom)
			.NotEmpty()
			.When(x => string.Equals(x.PolicyOverrideAction, "disable", StringComparison.OrdinalIgnoreCase) &&
			           x.PolicyOverride != null)
			.WithMessage("Effective date is required when disabling policy override.");

		RuleFor(x => x.PolicyOverride!.AssignmentId)
			.Must(id => !id.HasValue || id.Value != Guid.Empty)
			.When(x => string.Equals(x.PolicyOverrideAction, "disable", StringComparison.OrdinalIgnoreCase) &&
			           x.PolicyOverride != null)
			.WithMessage("Policy override assignment id must not be empty when provided.");

		RuleFor(x => x.PolicyOverride)
			.SetValidator(new EmployeePayrollPolicyOverrideContractValidator()!)
			.When(x => x.PolicyOverride != null &&
			           !string.Equals(x.PolicyOverrideAction, "disable", StringComparison.OrdinalIgnoreCase));
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
		RuleFor(x => x.AssignmentId)
			.Must(id => !id.HasValue || id.Value != Guid.Empty)
			.WithMessage("Policy override assignment id must not be empty when provided.");
		RuleFor(x => x.PayrollPolicyId).NotEmpty();
		RuleFor(x => x.AttendancePolicyId).NotEmpty();
		RuleFor(x => x.LeavePolicyId).NotEmpty();
		RuleFor(x => x.EffectiveFrom).NotEmpty();
		RuleFor(x => x.EffectiveTo)
			.Must((x, effectiveTo) => !effectiveTo.HasValue || effectiveTo.Value.Date >= x.EffectiveFrom.Date)
			.WithMessage("Policy override effective-to must be on or after effective-from.");
	}
}
