using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class RunPayrollRequestValidator_Tests
{
	private readonly RunPayrollRequestValidator _validator = new();

	[Fact]
	public void Should_Allow_Request_Without_EmployeeIds()
	{
		var result = _validator.Validate(new RunPayrollRequest
		{
			Month = "2026-04",
			EmployeeIds = null
		});

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Should_Reject_Request_With_Empty_EmployeeId()
	{
		var result = _validator.Validate(new RunPayrollRequest
		{
			Month = "2026-04",
			EmployeeIds = new List<Guid> { Guid.Empty }
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Should_Reject_Request_With_Too_Many_EmployeeIds()
	{
		var employeeIds = new List<Guid>();
		for (var i = 0; i < 501; i++)
		{
			employeeIds.Add(Guid.NewGuid());
		}

		var result = _validator.Validate(new RunPayrollRequest
		{
			Month = "2026-04",
			EmployeeIds = employeeIds
		});

		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThan(0);
	}
}
