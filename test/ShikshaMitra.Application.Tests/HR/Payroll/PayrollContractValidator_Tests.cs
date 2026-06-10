using System;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollContractValidator_Tests
{
	private readonly PayrollContractValidator _validator = new();

	[Fact]
	public void Should_Reject_Empty_PolicyOverride_AssignmentId_When_Upserting()
	{
		var result = _validator.Validate(new PayrollContract
		{
			PolicyOverrideAction = "upsert",
			PolicyOverride = new EmployeePayrollPolicyOverrideContract
			{
				AcademicYearId = Guid.NewGuid(),
				AssignmentId = Guid.Empty,
				PayrollPolicyId = Guid.NewGuid(),
				AttendancePolicyId = Guid.NewGuid(),
				EffectiveFrom = DateTime.Today,
			},
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.Any(x => x.PropertyName == "PolicyOverride.AssignmentId").ShouldBeTrue();
	}

	[Fact]
	public void Should_Reject_Empty_PolicyOverride_AssignmentId_When_Disabling()
	{
		var result = _validator.Validate(new PayrollContract
		{
			PolicyOverrideAction = "disable",
			PolicyOverride = new EmployeePayrollPolicyOverrideContract
			{
				AcademicYearId = Guid.NewGuid(),
				AssignmentId = Guid.Empty,
				EffectiveFrom = DateTime.Today,
			},
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.Any(x => x.PropertyName == "PolicyOverride.AssignmentId").ShouldBeTrue();
	}
}
