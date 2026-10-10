using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PayrollCalculationTestCollection
{
	public const string Name = "Payroll calculation regressions";
}

public partial class PayrollPreviewPostgres_Tests
{
	[PayrollPostgresFact]
	public async Task Legacy_completed_preview_cannot_be_reviewed_or_locked_and_fresh_preview_resets_review()
	{
		var run = await StartAsync("2026-06");
		await ExecuteAsync(run.Id);
		var rows = await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{run.Id}/employees?pageSize=100");
		var employeeId = rows.Items.First().EmployeeId;
		var request = new { month = run.Month, run.AcademicYearId, previewRunId = run.Id, employeeIds = new[] { employeeId } };
		(await Client.PostAsJsonAsync("/api/payroll/run/reviews", request)).EnsureSuccessStatusCode();
		await WithDatabaseAsync(async db =>
		{
			var stored = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == run.Id);
			stored.ExtraProperties.Remove(PayrollPreviewRun.CalculationVersionProperty);
			await db.SaveChangesAsync();
		});
		(await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{run.Id}")).IsCurrent.ShouldBeFalse();
		(await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{run.Id}/employees?pageSize=100")).IsCurrent.ShouldBeFalse();
		foreach (var endpoint in new[] { "/api/payroll/run/reviews", "/api/payroll/run" })
		{
			var rejected = await Client.PostAsJsonAsync(endpoint, request);
			rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict);
			(await rejected.Content.ReadAsStringAsync()).ShouldContain("PAYROLL_PREVIEW_STALE");
		}
		var fresh = await StartAsync(run.Month);
		fresh.Id.ShouldNotBe(run.Id);
		await ExecuteAsync(fresh.Id);
		(await StartAsync(run.Month)).Id.ShouldBe(fresh.Id);
		var refreshed = await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{fresh.Id}/employees?pageSize=100");
		refreshed.IsCurrent.ShouldBeTrue();
		refreshed.Items.Single(x => x.EmployeeId == employeeId).ReviewStatus.ShouldBe("PENDING");
	}

	[PayrollPostgresFact]
	public async Task Legacy_queued_worker_is_superseded_without_calculation()
	{
		var run = await StartAsync("2026-08");
		await WithDatabaseAsync(async db =>
		{
			var stored = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == run.Id);
			stored.ExtraProperties.Remove(PayrollPreviewRun.CalculationVersionProperty);
			await db.SaveChangesAsync();
		});
		await ExecuteAsync(run.Id);
		(await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{run.Id}")).Status.ShouldBe("SUPERSEDED");
		await WithDatabaseAsync(async db =>
			(await db.PayrollPreviewEmployees.CountAsync(x => x.RunId == run.Id)).ShouldBe(0));
		(await StartAsync(run.Month)).Id.ShouldNotBe(run.Id);
	}

	[PayrollPostgresFact]
	public async Task Legacy_running_preview_is_superseded_when_replacement_is_requested()
	{
		var run = await StartAsync("2026-09");
		await WithDatabaseAsync(async db =>
		{
			var stored = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == run.Id);
			stored.Start(Guid.NewGuid(), DateTime.UtcNow);
			stored.ExtraProperties.Remove(PayrollPreviewRun.CalculationVersionProperty);
			await db.SaveChangesAsync();
		});
		(await StartAsync(run.Month)).Id.ShouldNotBe(run.Id);
		(await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{run.Id}")).Status.ShouldBe("SUPERSEDED");
	}
}
