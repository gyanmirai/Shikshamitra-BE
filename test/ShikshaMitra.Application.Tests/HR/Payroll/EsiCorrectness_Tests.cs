using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiCorrectness_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task New_continuity_evidence_must_invalidate_later_payroll_preview(bool lockedSnapshot)
	{
		var employeeId = Guid.NewGuid();
		var revision = Guid.Empty;
		var runId = Guid.NewGuid();
		var snapshotId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var run = new PayrollPreviewRun(runId, null, Guid.NewGuid(), "2026-09", null, "test", 1, DateTime.UtcNow);
			run.Start(Guid.Empty, DateTime.UtcNow);
			run.Complete("{}", DateTime.UtcNow);
			await GetRequiredService<IRepository<PayrollPreviewRun, Guid>>().InsertAsync(run);
			await GetRequiredService<IRepository<PayrollPreviewEmployee, Guid>>().InsertAsync(
				new PayrollPreviewEmployee(Guid.NewGuid(), null, runId, employeeId, "Test", "E1", "{}"));
			await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(
				new PayrollRunItemSnapshot(snapshotId, null, "2026-09", employeeId));
		});
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<EmployeeStatutoryEnrollment, Guid>>()
			.InsertAsync(new EmployeeStatutoryEnrollment(Guid.NewGuid(), null, Guid.NewGuid(), "ESI")));
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(new(2026, 9, 1), [employeeId])).ShouldBeEmpty();
			revision = await GetRequiredService<IPayrollInputRevisionStore>().ReadAsync();
			revision.ShouldNotBe(Guid.Empty);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			if (lockedSnapshot)
			{
				var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-08", employeeId)
				{
					EmployeeStateInsuranceRuleId = Guid.NewGuid(),
					EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
					EmployeeStateInsuranceNumber = "1234567890"
				};
				snapshot.MarkLocked();
				await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot);
			}
			else
			{
				var slip = new SalarySlip(Guid.NewGuid(), null, employeeId, "2026-08", [], [], DateTime.UtcNow);
				slip.SetStatutorySnapshot([], null, null, null, null, Guid.NewGuid(), 1, 20000m, 0m,
					"WITHIN_WAGE_CEILING", "1234567890", new(2026, 4, 1), new(2026, 9, 30));
				slip.FinalizeSlip();
				await GetRequiredService<IRepository<SalarySlip, Guid>>().InsertAsync(slip);
			}
		});
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(new(2026, 9, 1), [employeeId]))
				.ShouldContainKey(employeeId);
			await GetRequiredService<EsiContinuityInvalidator>().InvalidateAsync("2026-08", [employeeId]);
			(await GetRequiredService<IRepository<PayrollPreviewRun, Guid>>().GetAsync(runId)).Status.ShouldBe("SUPERSEDED");
			(await GetRequiredService<IPayrollInputRevisionStore>().ReadAsync()).ShouldBe(revision);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var draft = new SalarySlip(Guid.NewGuid(), null, employeeId, "2026-09", [], [], DateTime.UtcNow);
			var error = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
				GetRequiredService<SalarySlipDraftFreshnessService>().EnsureFreshAsync(draft));
			error.Code.ShouldBe(HRErrorCodes.PayrollSnapshotRequiredForSalarySlip);
		});
	}

	[Fact]
	public void Registration_pending_without_number_must_block_final_payroll()
	{
		var employeeId = Guid.NewGuid();
		var componentId = Guid.NewGuid();
		var enrollment = new EmployeeStatutoryEnrollment(Guid.NewGuid(), null, employeeId, "ESI");
		enrollment.Update(EmployeeStatutoryEnrollmentStatus.RegistrationPending,
			StatutoryWageCeilingCategory.Standard, null, null, null, null, null);
		var basic = new EmployeePayrollComponent(Guid.NewGuid(), employeeId, "Basic", componentId, null, 10000m,
			true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None, true, true, PayrollComponentSemanticType.Basic);
		var deduction = new EmployeePayrollComponent(Guid.NewGuid(), employeeId, "ESI", Guid.NewGuid(), null, 0m,
			true, false, null, SalaryComponentType.Fixed, SalaryComponentDependency.None, false, true, PayrollComponentSemanticType.EmployeeStateInsurance);
		var context = new PayrollEsiStatutoryContext(new(2026, 8, 1), Rule(), Establishment(), enrollment,
			new Dictionary<Guid, StatutoryWageTreatment> { [componentId] = StatutoryWageTreatment.Included }, [], deduction);
		var result = PayrollComputation.Calculate(30, 0, 30m, 30m, [basic, deduction],
			new PayrollStatutoryRuleSet(null, [], EmployeeStateInsurance: context));
		result.EmployeeStateInsuranceNumber.ShouldBeNull();
		result.EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
		PayrollLockPreconditionGuard.HasBlockingLockIssue(JsonSerializer.Serialize(result.StatutoryIssueCodes))
			.ShouldBeTrue("Covered payroll has no insurance number but may pass the statutory lock check.");
	}

	[Fact]
	public void Midmonth_exception_is_rejected_by_contract_and_domain()
	{
		var input = new CreateStatutoryOverrideDto
		{
			OverrideType = "ForceNotCovered",
			Reason = "Supported test exemption",
			EffectiveFrom = new(2026, 8, 15),
			EffectiveTo = new(2026, 8, 31)
		};
		new CreateStatutoryOverrideDtoValidator().Validate(input).IsValid.ShouldBeFalse();
		Should.Throw<Volo.Abp.BusinessException>(() => new StatutoryOverride(Guid.NewGuid(), null,
			Guid.NewGuid(), "ESI", StatutoryOverrideType.ForceNotCovered, null, input.Reason, null,
			input.EffectiveFrom, input.EffectiveTo, Guid.NewGuid()));
	}

	private static StatutoryRuleVersion Rule() => new(Guid.NewGuid(), null, "ESI", "IN-GJ", 1,
		new(2025, 11, 21), null, 10, 21000m, 25000m, 0.75m, 3.25m, 50m, 15m,
		StatutoryRoundingPolicy.NextHigherRupee, "https://www.labour.gov.in/", new(2026, 8, 18));
	private static StatutoryEstablishment Establishment() =>
		new EsiPayrollConfiguration(Guid.NewGuid(), null, new(2026, 1, 1), true, null, null, "review").ToCalculationEstablishment()!;

	[Theory]
	[InlineData(2026, 2, 28, true)]
	[InlineData(2028, 2, 29, true)]
	[InlineData(2028, 2, 28, false)]
	[InlineData(2026, 8, 15, false)]
	public void Exception_contract_requires_whole_calendar_months(int year, int month, int lastDay, bool valid)
	{
		var dto = new CreateStatutoryOverrideDto
		{
			OverrideType = "ForceNotCovered",
			Reason = "Supported exemption",
			EffectiveFrom = new(year, month, 1),
			EffectiveTo = new(year, month, lastDay)
		};
		new CreateStatutoryOverrideDtoValidator().Validate(dto).IsValid.ShouldBe(valid);
		if (valid)
		{
			var item = new StatutoryOverride(Guid.NewGuid(), null, Guid.NewGuid(), "ESI",
				StatutoryOverrideType.ForceNotCovered, null, dto.Reason, null, dto.EffectiveFrom, dto.EffectiveTo, null);
			item.AppliesOn(dto.EffectiveFrom).ShouldBeTrue();
			item.AppliesOn(dto.EffectiveTo.AddDays(1)).ShouldBeFalse();
		}
	}

	[Theory]
	[InlineData("WITHIN_WAGE_CEILING", true)]
	[InlineData("CONTRIBUTION_PERIOD_CONTINUATION", true)]
	[InlineData("FORCE_COVERED_OVERRIDE", true)]
	[InlineData("ABOVE_WAGE_CEILING", false)]
	[InlineData("FORCE_NOT_COVERED_OVERRIDE", false)]
	public void Lock_checks_coverage_facts_even_when_issue_codes_and_contributions_are_empty(string decision, bool blocked)
	{
		var id = Guid.NewGuid();
		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-08", id)
		{ EmployeeStateInsuranceRuleId = Guid.NewGuid(), EmployeeStateInsuranceCoverageDecision = decision };
		var action = () => PayrollLockPreconditionGuard.EnsureNoBlockingSetupIssues([id],
			new Dictionary<Guid, PayrollRunItemSnapshot> { [id] = snapshot }, "2026-08");
		if (blocked) Should.Throw<Volo.Abp.BusinessException>(action);
		else Should.NotThrow(action);
		var slip = new SalarySlip(Guid.NewGuid(), null, id, "2026-08", [], [], DateTime.UtcNow);
		slip.SetStatutorySnapshot([], null, null, null, null, snapshot.EmployeeStateInsuranceRuleId, 1, 0m, 0m,
			decision, null, new(2026, 4, 1), new(2026, 9, 30));
		if (blocked)
		{
			Should.Throw<Volo.Abp.BusinessException>(() => slip.FinalizeSlip());
			Should.Throw<Volo.Abp.BusinessException>(() => snapshot.MarkLocked());
		}
		else Should.NotThrow(() => slip.FinalizeSlip());
		snapshot.EmployeeStateInsuranceNumber = "1234567890";
		Should.NotThrow(action);
	}

	[Fact]
	public async Task Invalidation_preserves_other_periods_and_immutable_employee_months_and_is_repeatable()
	{
		var employee = Guid.NewGuid();
		var other = Guid.NewGuid();
		var protectedEmployee = Guid.NewGuid();
		var running = Guid.NewGuid();
		var nextPeriod = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var snapshots = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
			foreach (var (id, month, locked) in new[] {
				(employee, "2026-07", true), (employee, "2026-06", false),
				(employee, "2026-08", false), (employee, "2026-09", true), (employee, "2026-10", false),
				(other, "2026-08", false), (protectedEmployee, "2026-08", false) })
			{
				var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, month, id)
				{
					EmployeeStateInsuranceRuleId = Guid.NewGuid(),
					EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
					EmployeeStateInsuranceNumber = "1234567890"
				};
				if (locked) snapshot.MarkLocked();
				await snapshots.InsertAsync(snapshot);
			}
			var source = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-07", protectedEmployee)
			{ IsLocked = true, EmployeeStateInsuranceRuleId = Guid.NewGuid() };
			await snapshots.InsertAsync(source);
			var slip = new SalarySlip(Guid.NewGuid(), null, protectedEmployee, "2026-08", [], [], DateTime.UtcNow);
			slip.FinalizeSlip();
			await GetRequiredService<IRepository<SalarySlip, Guid>>().InsertAsync(slip);
			foreach (var (id, month) in new[] { (running, "2026-08"), (nextPeriod, "2026-10") })
			{
				var run = new PayrollPreviewRun(id, null, Guid.NewGuid(), month, null, "scope", 2, DateTime.UtcNow);
				run.Start(Guid.Empty, DateTime.UtcNow);
				await GetRequiredService<IRepository<PayrollPreviewRun, Guid>>().InsertAsync(run);
			}
			await GetRequiredService<IRepository<PayrollRunReview, Guid>>().InsertAsync(
				new PayrollRunReview(Guid.NewGuid(), null, "2026-08", employee, Guid.NewGuid(), DateTime.UtcNow));
		});
		for (var attempt = 0; attempt < 2; attempt++)
			await WithUnitOfWorkAsync(() => GetRequiredService<EsiContinuityInvalidator>()
				.InvalidateAsync("2026-07", [employee, protectedEmployee]));
		await WithUnitOfWorkAsync(async () =>
		{
			var remaining = await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>()
				.GetListAsync(x => x.EmployeeId == employee);
			remaining.Select(x => x.MonthYear).OrderBy(x => x).ShouldBe(["2026-06", "2026-07", "2026-09", "2026-10"]);
			(await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>()
				.CountAsync(x => x.EmployeeId == protectedEmployee && x.MonthYear == "2026-08")).ShouldBe(1);
			(await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>()
				.CountAsync(x => x.EmployeeId == other)).ShouldBe(1);
			(await GetRequiredService<IRepository<PayrollRunReview, Guid>>().CountAsync(x => x.EmployeeId == employee)).ShouldBe(0);
			var runs = GetRequiredService<IRepository<PayrollPreviewRun, Guid>>();
			(await runs.GetAsync(running)).Status.ShouldBe("SUPERSEDED");
			(await runs.GetAsync(nextPeriod)).Status.ShouldBe("RUNNING");
		});
	}
}
