using System;
using System.Collections.Generic;
using System.Reflection;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollLockPreconditionGuard_Tests
{
	[Theory]
	[InlineData(SalarySlipStatus.Finalized)]
	[InlineData(SalarySlipStatus.Paid)]
	public void EnsureNoFinalizedSlips_Should_Reject_Every_Immutable_Status(
		SalarySlipStatus status)
	{
		var employeeId = Guid.NewGuid();
		var slip = new SalarySlip(
			Guid.NewGuid(),
			Guid.NewGuid(),
			employeeId,
			"2026-07",
			new List<SalarySlipLineItem>(),
			new List<SalarySlipLineItem>(),
			DateTime.UtcNow);
		if (status == SalarySlipStatus.Finalized)
		{
			slip.FinalizeSlip();
		}
		else
		{
			typeof(SalarySlip)
				.GetProperty(nameof(SalarySlip.Status), BindingFlags.Instance | BindingFlags.Public)!
				.SetValue(slip, status);
		}

		var exception = Should.Throw<BusinessException>(() =>
			PayrollLockPreconditionGuard.EnsureNoFinalizedSlips(
				[employeeId],
				[slip],
				"JULY-2026"));

		exception.Code.ShouldBe(HRErrorCodes.PayrollAlreadyLocked);
	}
}
