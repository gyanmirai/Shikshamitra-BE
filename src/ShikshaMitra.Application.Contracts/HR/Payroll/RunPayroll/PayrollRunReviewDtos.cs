using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Request to mark or unmark monthly payroll review rows.
/// </summary>
public class PayrollRunReviewRequest
{
	public Guid PreviewRunId { get; set; }
	public string Month { get; set; } = string.Empty;
	public Guid? AcademicYearId { get; set; }
	public List<Guid> EmployeeIds { get; set; } = new();
}

public class PayrollRunReviewRequestValidator : AbstractValidator<PayrollRunReviewRequest>
{
	public PayrollRunReviewRequestValidator()
	{
		RuleFor(x => x.PreviewRunId).NotEmpty();
		RuleFor(x => x.Month).NotEmpty().MaximumLength(30);
		RuleFor(x => x.EmployeeIds).NotNull().Must(x => x.Count > 0);
		RuleForEach(x => x.EmployeeIds).NotEmpty();
	}
}
