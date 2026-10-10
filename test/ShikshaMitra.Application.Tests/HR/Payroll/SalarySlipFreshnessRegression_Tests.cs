using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class SalarySlipFreshnessRegression_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(SalarySlipStatus.Draft)]
	[InlineData(SalarySlipStatus.Finalized)]
	[InlineData(SalarySlipStatus.Paid)]
	public async Task Metadata_refresh_preserves_identity_and_never_rewrites_issued_slips(SalarySlipStatus status)
	{
		var id = Guid.NewGuid();
		var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), null, "2026-04", Guid.NewGuid())
		{
			DayCalculationMode = "working-days"
		};
		var processed = new DateTime(2026, 4, 30);
		await WithUnitOfWorkAsync(async () =>
		{
			var slip = new SalarySlip(id, null, snapshot.EmployeeId, snapshot.MonthYear, [], [], processed);
			SalarySlipGenerationRules.ApplySnapshotMetadata(slip, snapshot, DayCalculationType.WorkingDays);
			if (status != SalarySlipStatus.Draft) slip.FinalizeSlip();
			if (status == SalarySlipStatus.Paid)
				slip.RecordPayment(DateTimeOffset.UtcNow, "Synthetic fixture", Guid.NewGuid(), Guid.NewGuid());
			await GetRequiredService<IRepository<SalarySlip, Guid>>().InsertAsync(slip);
			await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var slips = GetRequiredService<IRepository<SalarySlip, Guid>>();
			var original = await slips.GetAsync(id);
			var freshness = GetRequiredService<SalarySlipDraftFreshnessService>();
			(await freshness.EnsureFreshAsync(original)).ProcessedDate.ShouldBe(processed);
			var stored = await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetAsync(snapshot.Id);
			stored.EmployeeStateInsuranceNumber = "1234567890";
			stored.CompensationRevisionNumber = 2;
			await GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().UpdateAsync(stored, autoSave: true);
			var refreshed = await freshness.EnsureFreshAsync(original);
			refreshed.Id.ShouldBe(id);
			refreshed.Status.ShouldBe(status);
			refreshed.NetSalary.ShouldBe(0m);
			refreshed.EmployeeStateInsuranceNumber.ShouldBe(status == SalarySlipStatus.Draft ? "1234567890" : null);
			refreshed.CompensationRevisionNumber.ShouldBe(status == SalarySlipStatus.Draft ? 2 : null);
			if (status == SalarySlipStatus.Draft) refreshed.ProcessedDate.ShouldBeGreaterThan(processed);
			else refreshed.ProcessedDate.ShouldBe(processed);
		});
	}
}
