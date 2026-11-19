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
	[Fact]
	public void Pending_registration_does_not_block_lock_or_finalization_and_keeps_immutable_history()
	{
		var employee = Guid.NewGuid();
		var rule = Guid.NewGuid();
		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Guid.NewGuid(), "2026-10", employee)
		{
			EmployeeStateInsuranceRuleId = rule,
			EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
			EmployeeStateInsuranceNumber = null,
			IssueCodesJson = "[\"ESI_NUMBER_MISSING\"]"
		};
		PayrollLockPreconditionGuard.EnsureNoBlockingSetupIssues([employee], new Dictionary<Guid, PayrollRunItemSnapshot> { [employee] = snapshot }, "2026-10");
		snapshot.MarkLocked();
		snapshot.IsLocked.ShouldBeTrue();
		var slip = new SalarySlip(Guid.NewGuid(), snapshot.TenantId, employee, "2026-10", [], [], DateTime.UtcNow);
		slip.SetStatutorySnapshot([], null, null, null, null, rule, 1, 10000m, 0m, "WITHIN_WAGE_CEILING", null, new(2026, 10, 1), new(2027, 3, 31));
		slip.FinalizeSlip();
		slip.Status.ShouldBe(SalarySlipStatus.Finalized);
		Should.Throw<InvalidOperationException>(() => slip.SetStatutorySnapshot([], null, null, null, null, rule, 1, 10000m, 0m, "WITHIN_WAGE_CEILING", "LATER", new(2026, 10, 1), new(2027, 3, 31)));
	}

	[Fact]
	public void Esi_readiness_issues_should_block_payroll_lock()
	{
		PayrollLockPreconditionGuard.HasBlockingLockIssue("[\"ESI_WAGE_CLASSIFICATION_REQUIRED\"]")
			.ShouldBeTrue();
		PayrollLockPreconditionGuard.HasBlockingLockIssue("[\"ESI_ENROLLMENT_REVIEW_REQUIRED\"]")
			.ShouldBeTrue();
	}

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
