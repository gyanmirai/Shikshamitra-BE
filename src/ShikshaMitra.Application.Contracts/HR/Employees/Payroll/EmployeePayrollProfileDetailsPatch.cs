using FluentValidation;
using System.Text.Json.Serialization;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>Updates supplied profile groups only. Salary and policy writes have separate contracts.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EmployeePayrollProfileDetailsPatch
{
	public BankDetailsContract? BankDetails { get; set; }
	public EmployeeStatutoryIdentifiers? StatutoryIdentifiers { get; set; }
	public EmployeeAttendanceIntegration? AttendanceIntegration { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EmployeeStatutoryIdentifiers
{
	public string? EpfNumber { get; set; }
	public string? UanNumber { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EmployeeAttendanceIntegration
{
	public string? PunchId { get; set; }
	public decimal? TotalWorkingHourPerMonth { get; set; }
}

public class EmployeePayrollProfileDetailsPatchValidator : AbstractValidator<EmployeePayrollProfileDetailsPatch>
{
	public EmployeePayrollProfileDetailsPatchValidator()
	{
		RuleFor(x => x).Must(x => x.BankDetails != null || x.StatutoryIdentifiers != null || x.AttendanceIntegration != null)
			.WithMessage("Supply at least one profile details group.");
		RuleFor(x => x.BankDetails).SetValidator(new BankDetailsContractValidator()!).When(x => x.BankDetails != null);
		When(x => x.StatutoryIdentifiers != null, () =>
		{
			RuleFor(x => x.StatutoryIdentifiers!.EpfNumber).Matches(@"^[A-Z]{2}/\d{5}/\d{7}$").When(x => !string.IsNullOrEmpty(x.StatutoryIdentifiers!.EpfNumber));
			RuleFor(x => x.StatutoryIdentifiers!.UanNumber).Matches(@"^\d{12}$")
				.When(x => !string.IsNullOrWhiteSpace(x.StatutoryIdentifiers!.UanNumber))
				.WithMessage("UAN must contain 12 digits.");
		});
		When(x => x.AttendanceIntegration != null, () =>
		{
			RuleFor(x => x.AttendanceIntegration!.PunchId).MaximumLength(20).Matches(@"^[a-zA-Z0-9\-_]+$").When(x => !string.IsNullOrEmpty(x.AttendanceIntegration!.PunchId));
			RuleFor(x => x.AttendanceIntegration!.TotalWorkingHourPerMonth).InclusiveBetween(0m, 744m);
		});
	}
}
