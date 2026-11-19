using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiContributionContinuity_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(SalarySlipStatus.Finalized, false, true)]
	[InlineData(SalarySlipStatus.Paid, false, true)]
	[InlineData(SalarySlipStatus.Finalized, true, true)]
	[InlineData(SalarySlipStatus.Paid, true, true)]
	[InlineData(SalarySlipStatus.Draft, true, false)]
	public async Task Immutable_slips_supply_coverage_without_requiring_a_locked_snapshot(
		SalarySlipStatus status, bool addUnlockedSnapshot, bool continues)
	{
		var employeeId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<SalarySlip, Guid>>().InsertAsync(Slip(employeeId, "2026-08", status));
			if (addUnlockedSnapshot)
				await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(
					Snapshot(employeeId, "2026-08", locked: false));
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var resolved = await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(new(2026, 9, 1), [employeeId]);
			resolved.ContainsKey(employeeId).ShouldBe(continues);
			if (continues)
			{
				resolved[employeeId].ContributionPeriodEnd.ShouldBe(new DateTime(2026, 9, 30));
				resolved[employeeId].InsuranceNumber.ShouldBe("SLIP-ESI");
			}
			(await GetRequiredService<IRepository<EmployeeStatutoryEnrollment, Guid>>()
				.AnyAsync(x => x.EmployeeId == employeeId)).ShouldBeFalse();
		});
	}

	[Theory]
	[InlineData("2026-03", 2026, 4, false)]
	[InlineData("2026-09", 2026, 10, false)]
	[InlineData("2026-10", 2027, 3, true)]
	[InlineData("2026-09", 2026, 9, false)]
	[InlineData("2026-10", 2026, 9, false)]
	public async Task Only_prior_months_in_the_same_contribution_period_count(string priorMonth, int year, int month, bool continues)
	{
		var employeeId = Guid.NewGuid();
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<SalarySlip, Guid>>()
			.InsertAsync(Slip(employeeId, priorMonth, SalarySlipStatus.Finalized)));
		await WithUnitOfWorkAsync(async () =>
		{
			var resolved = await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(new(year, month, 1), [employeeId]);
			resolved.ContainsKey(employeeId).ShouldBe(continues);
		});
	}

	[Fact]
	public async Task Current_month_slip_is_not_prior_evidence_when_request_has_a_day_component()
	{
		var employeeId = Guid.NewGuid();
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<SalarySlip, Guid>>()
			.InsertAsync(Slip(employeeId, "2026-09", SalarySlipStatus.Finalized)));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(new(2026, 9, 15), [employeeId])).ShouldBeEmpty());
	}

	[Theory]
	[InlineData("ABOVE_WAGE_CEILING", true)]
	[InlineData("WITHIN_WAGE_CEILING", false)]
	public async Task Finalized_slip_without_coverage_evidence_does_not_inherit_a_conflicting_locked_snapshot(string decision, bool hasRule)
	{
		var employeeId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<SalarySlip, Guid>>().InsertAsync(
				Slip(employeeId, "2026-08", SalarySlipStatus.Finalized, decision: decision, hasRule: hasRule));
			await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(Snapshot(employeeId, "2026-08", true));
		});
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(new(2026, 9, 1), [employeeId])).ShouldBeEmpty());
	}

	[Fact]
	public async Task History_is_isolated_by_tenant_and_requested_employee()
	{
		var employeeId = Guid.NewGuid();
		var tenant = GetRequiredService<ICurrentTenant>();
		var otherTenant = Guid.NewGuid();
		using (tenant.Change(otherTenant))
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<SalarySlip, Guid>>()
				.InsertAsync(Slip(employeeId, "2026-08", SalarySlipStatus.Paid, otherTenant)));
		using (tenant.Change(Guid.NewGuid()))
		{
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<SalarySlip, Guid>>()
				.InsertAsync(Slip(Guid.NewGuid(), "2026-08", SalarySlipStatus.Finalized, tenant.Id)));
			await WithUnitOfWorkAsync(async () =>
				(await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(new(2026, 9, 1), [employeeId])).ShouldBeEmpty());
		}
	}

	private static SalarySlip Slip(Guid employeeId, string month, SalarySlipStatus status, Guid? tenantId = null,
		string decision = "WITHIN_WAGE_CEILING", bool hasRule = true)
	{
		var slip = new SalarySlip(Guid.NewGuid(), tenantId, employeeId, month, [], [], DateTime.UtcNow);
		slip.SetStatutorySnapshot([], null, null, null, null, hasRule ? Guid.NewGuid() : null, 1,
			20000m, 0m, decision, "SLIP-ESI", null, null);
		if (status != SalarySlipStatus.Draft) slip.FinalizeSlip();
		if (status == SalarySlipStatus.Paid)
			typeof(SalarySlip).GetProperty(nameof(SalarySlip.Status))!.SetValue(slip, status);
		return slip;
	}

	private static PayrollRunItemSnapshot Snapshot(Guid employeeId, string month, bool locked)
	{
		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, month, employeeId)
		{
			EmployeeStateInsuranceRuleId = Guid.NewGuid(),
			EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
			EmployeeStateInsuranceNumber = "SNAPSHOT-ESI"
		};
		if (locked) snapshot.MarkLocked();
		return snapshot;
	}
}