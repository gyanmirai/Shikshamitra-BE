using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipCorrectionContinuity_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData("2026-02", "2026-03", "2026-04", true)]
	[InlineData("2026-03", "2026-04", "2026-04", false)]
	[InlineData("2026-08", "2026-09", "2026-10", true)]
	[InlineData("2026-09", "2026-10", "2026-10", false)]
	[InlineData("2026-10", "2027-03", "2027-04", true)]
	public async Task Applied_paid_correction_uses_source_period_not_settlement_period_for_continuity(
		string sourceMonth, string resolvedMonth, string settlementMonth, bool continues)
	{
		var employee = Guid.NewGuid();
		await WithUnitOfWorkAsync(() => InsertCorrectionAsync(employee, sourceMonth, settlementMonth,
			"ABOVE_WAGE_CEILING", "WITHIN_WAGE_CEILING", DateTime.UtcNow));
		await WithUnitOfWorkAsync(async () =>
		{
			var resolved = await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(Month(resolvedMonth), [employee]);
			resolved.ContainsKey(employee).ShouldBe(continues);
			if (continues) resolved[employee].InsuranceNumber.ShouldBe("CORRECTED-ESI");
		});
	}

	[Fact]
	public async Task Latest_applied_correction_overrides_original_and_locked_coverage_evidence()
	{
		var employee = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await InsertCorrectionAsync(employee, "2026-08", "2026-10", "WITHIN_WAGE_CEILING",
				"WITHIN_WAGE_CEILING", DateTime.UtcNow.AddMinutes(-2));
			await InsertCorrectionAsync(employee, "2026-08", "2026-11", "WITHIN_WAGE_CEILING",
				"ABOVE_WAGE_CEILING", DateTime.UtcNow.AddMinutes(-1));
			var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-08", employee)
			{
				EmployeeStateInsuranceRuleId = Guid.NewGuid(),
				EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING",
				EmployeeStateInsuranceNumber = "OLD-SNAPSHOT-ESI"
			};
			snapshot.MarkLocked();
			await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot);
		});
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(Month("2026-09"), [employee])).ShouldBeEmpty());
	}

	[Fact]
	public async Task Reviewed_but_unapplied_correction_does_not_change_issued_coverage()
	{
		var employee = Guid.NewGuid();
		await WithUnitOfWorkAsync(() => InsertCorrectionAsync(employee, "2026-08", "2026-10",
			"WITHIN_WAGE_CEILING", "ABOVE_WAGE_CEILING", DateTime.UtcNow, apply: false));
		await WithUnitOfWorkAsync(async () =>
		{
			var resolved = await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(Month("2026-09"), [employee]);
			resolved[employee].InsuranceNumber.ShouldBe("ISSUED-ESI");
		});
	}

	[Fact]
	public async Task Applied_correction_evidence_is_isolated_by_tenant_and_requested_employee()
	{
		var employee = Guid.NewGuid();
		var tenant = GetRequiredService<ICurrentTenant>();
		using (tenant.Change(Guid.NewGuid()))
			await WithUnitOfWorkAsync(() => InsertCorrectionAsync(employee, "2026-08", "2026-10",
				"ABOVE_WAGE_CEILING", "WITHIN_WAGE_CEILING", DateTime.UtcNow));
		using (tenant.Change(Guid.NewGuid()))
		{
			await WithUnitOfWorkAsync(() => InsertCorrectionAsync(Guid.NewGuid(), "2026-08", "2026-10",
				"ABOVE_WAGE_CEILING", "WITHIN_WAGE_CEILING", DateTime.UtcNow));
			await WithUnitOfWorkAsync(async () =>
				(await GetRequiredService<EsiEnrollmentResolver>().ResolveAsync(Month("2026-09"), [employee])).ShouldBeEmpty());
		}
	}

	[Fact]
	public async Task Correction_invalidation_stops_at_source_contribution_boundary()
	{
		var employee = Guid.NewGuid();
		var march = Guid.NewGuid();
		var april = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await InsertPreviewAsync(march, employee, "2026-03");
			await InsertPreviewAsync(april, employee, "2026-04");
			await InsertCorrectionAsync(employee, "2026-02", "2026-04", "ABOVE_WAGE_CEILING",
				"WITHIN_WAGE_CEILING", DateTime.UtcNow);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<EsiContinuityInvalidator>().InvalidateAsync("2026-02", [employee]);
			var previews = GetRequiredService<IRepository<PayrollPreviewRun, Guid>>();
			(await previews.GetAsync(march)).Status.ShouldBe("SUPERSEDED");
			(await previews.GetAsync(april)).Status.ShouldBe("SUCCEEDED");
		});
	}

	private async Task InsertCorrectionAsync(Guid employee, string sourceMonth, string settlementMonth,
		string issuedDecision, string correctedDecision, DateTime completedAt, bool apply = true)
	{
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var slips = GetRequiredService<IRepository<SalarySlip, Guid>>();
		var slip = await slips.FindAsync(x => x.EmployeeId == employee && x.MonthYear == sourceMonth);
		if (slip == null)
		{
			slip = new SalarySlip(Guid.NewGuid(), tenant, employee, sourceMonth, [], [], DateTime.UtcNow);
			slip.SetStatutorySnapshot([], null, null, null, null, Guid.NewGuid(), 1, 20000m, 0m,
				issuedDecision, "ISSUED-ESI", null, null);
			slip.FinalizeSlip();
			slip.RecordPayment(DateTimeOffset.UtcNow, "Synthetic paid-source evidence", Guid.NewGuid(), Guid.NewGuid());
			await slips.InsertAsync(slip, autoSave: true);
		}
		var documents = GetRequiredService<IRepository<PayslipDocument, Guid>>();
		var document = await documents.FindAsync(x => x.SalarySlipId == slip.Id);
		if (document == null)
		{
			document = new PayslipDocument(Guid.NewGuid(), tenant, slip.Id, employee, sourceMonth, 1,
				DateTime.UtcNow, Guid.NewGuid(), "{}", [1]);
			await documents.InsertAsync(document, autoSave: true);
		}
		var correction = new PayslipCorrection(Guid.NewGuid(), document, Guid.NewGuid(),
			"Synthetic contribution-period correction", Guid.NewGuid(), "{}");
		var revision = Guid.NewGuid();
		correction.Calculate("[]", JsonSerializer.Serialize(new RunPayrollItem
		{
			EmployeeStateInsuranceRuleId = Guid.NewGuid(),
			EmployeeStateInsuranceCoverageDecision = correctedDecision,
			EmployeeStateInsuranceNumber = "CORRECTED-ESI"
		}), revision);
		correction.Review(revision, Guid.NewGuid(), completedAt);
		if (apply) correction.Complete(revision, Guid.NewGuid(), completedAt, null, settlementMonth);
		await GetRequiredService<IRepository<PayslipCorrection, Guid>>().InsertAsync(correction);
	}

	private async Task InsertPreviewAsync(Guid id, Guid employee, string month)
	{
		var run = new PayrollPreviewRun(id, null, Guid.NewGuid(), month, null, "synthetic correction boundary", 1, DateTime.UtcNow);
		run.Start(Guid.Empty, DateTime.UtcNow);
		run.Complete("{}", DateTime.UtcNow);
		await GetRequiredService<IRepository<PayrollPreviewRun, Guid>>().InsertAsync(run);
		await GetRequiredService<IRepository<PayrollPreviewEmployee, Guid>>().InsertAsync(
			new PayrollPreviewEmployee(Guid.NewGuid(), null, id, employee, "Synthetic", "E1", "{}"));
		await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(
			new PayrollRunItemSnapshot(Guid.NewGuid(), null, month, employee));
	}

	private static DateTime Month(string value) => DateTime.ParseExact(value + "-01", "yyyy-MM-dd",
		System.Globalization.CultureInfo.InvariantCulture);
}
