using System;
using FluentValidation;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollAdjustmentDto
{
	public Guid Id { get; set; }
	public string MonthYear { get; set; } = string.Empty;
	public Guid EmployeeId { get; set; }
	public string Name { get; set; } = string.Empty;
	public double Amount { get; set; }
	public bool IsEarning { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? SourceType { get; set; }
	public Guid? SourceId { get; set; }
	public bool IsSystemGenerated { get; set; }
	public DateTime? CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
}

public class PayrollAdjustmentRequest
{
	public string Month { get; set; } = string.Empty;
	public Guid? AcademicYearId { get; set; }
	public Guid EmployeeId { get; set; }
	public string Name { get; set; } = string.Empty;
	public double Amount { get; set; }
	public bool IsEarning { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? Description { get; set; }
}

public class PayrollAdjustmentRequestValidator : AbstractValidator<PayrollAdjustmentRequest>
{
	public PayrollAdjustmentRequestValidator()
	{
		RuleFor(x => x.Month).NotEmpty().MaximumLength(30);
		RuleFor(x => x.EmployeeId).NotEmpty();
		RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
		RuleFor(x => x.Description).MaximumLength(1000);
	}
}
