using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.HR.Payroll;

[Trait("Database", "PostgreSQL")]
public class PayrollPreviewPostgres_Tests(PayrollPostgresTestFactory factory, ITestOutputHelper output)
	: ShikshaMitraApiTestBase(factory), IClassFixture<PayrollPostgresTestFactory>
{
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private const string Previews = "/api/payroll/run/previews";

	[PayrollPostgresFact]
	public async Task Performance_gates_for_50_and_1000_approved_compensation_employees()
	{
		// The fixture is confined to the disposable PostgreSQL database required by the factory.
		// Copies only seeded synthetic employee/compensation data, not user or login credentials.
		foreach (var size in new[] { 50, 1000 })
		{
			var month = size == 50 ? "2026-08" : "2026-09";
			var before = await StartAsync(month);
			await ExpandSyntheticPopulationAsync(size - before.EmployeeCount, size);
			var run = await StartAsync(month);
			run.EmployeeCount.ShouldBe(size);
			var watch = Stopwatch.StartNew();
			await ExecuteAsync(run.Id);
			var duration = watch.Elapsed;
			var status = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{run.Id}");
			status.Status.ShouldBe("SUCCEEDED", status.ErrorMessage);
			status.IsCurrent.ShouldBeTrue();
			output.WriteLine($"PERFORMANCE: {size} employees, cold worker {duration.TotalSeconds:F3}s");
			duration.ShouldBeLessThan(TimeSpan.FromSeconds(size == 50 ? 15 : 60));
			var rows = await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{run.Id}/employees?pageSize=50&search=PayrollPerf");
			rows.Items.ShouldNotBeEmpty();
			rows.Items.ShouldAllBe(item => item.GrossSalary > 0);
			foreach (var endpoint in new[] { "start", "status", "page" })
			{
				var measurements = new List<double>();
				for (var request = 0; request < 20; request++)
				{
					watch.Restart();
					if (endpoint == "start") (await StartAsync(month)).Id.ShouldBe(run.Id);
					else
					{
						var response = await Client.GetAsync(endpoint == "status" ? $"{Previews}/{run.Id}" : $"{Previews}/{run.Id}/employees?pageSize=25");
						response.EnsureSuccessStatusCode();
						await response.Content.ReadAsByteArrayAsync();
					}
					measurements.Add(watch.Elapsed.TotalMilliseconds);
				}
				var p95 = measurements.Order().ElementAt(18);
				output.WriteLine($"PERFORMANCE: {size} employees, {endpoint} p95 {p95:F1}ms (20 requests)");
				p95.ShouldBeLessThan(2000);
			}
		}
	}

	private Task ExpandSyntheticPopulationAsync(int count, int batch) => WithDatabaseAsync(async db =>
	{
		count.ShouldBeGreaterThan(0);
		var source = await db.Set<EmployeeCompensationRevision>()
			.Where(x => x.Status == CompensationRevisionStatus.Approved && !x.EmployeeId.Equals(Guid.Empty))
			.OrderBy(x => x.GrossSalary).FirstAsync();
		await db.Database.ExecuteSqlInterpolatedAsync($"""
			INSERT INTO "AppEmployees"
			SELECT (jsonb_populate_record(NULL::"AppEmployees", to_jsonb(e) || jsonb_build_object(
			  'Id', gen_random_uuid(), 'UserId', NULL, 'EmployeeCode', 'PERF-' || {batch} || '-' || n,
			  'FirstName', 'PayrollPerf', 'LastName', {batch} || '-' || n,
			  'Email', 'payroll-perf-' || {batch} || '-' || n || '@example.invalid',
			  'PanNo', 'TEST' || lpad(({batch} + n)::text, 6, '0'),
			  'AdharNo', 'TEST' || lpad(({batch} + n)::text, 8, '0'),
			  'DateOfJoining', '2026-04-01', 'ConcurrencyStamp', gen_random_uuid()::text))).*
			FROM "AppEmployees" e CROSS JOIN generate_series(1, {count}) n WHERE e."Id" = {source.EmployeeId};
			INSERT INTO "HR_EmployeeCompensationRevisions"
			SELECT (jsonb_populate_record(NULL::"HR_EmployeeCompensationRevisions", to_jsonb(r) || jsonb_build_object(
			  'Id', gen_random_uuid(), 'EmployeeId', e."Id", 'ConcurrencyStamp', gen_random_uuid()::text))).*
			FROM "HR_EmployeeCompensationRevisions" r CROSS JOIN "AppEmployees" e
			WHERE r."Id" = {source.Id} AND e."EmployeeCode" LIKE 'PERF-' || {batch} || '-%';
			INSERT INTO "HR_EmployeeCompensationRevisionLines"
			SELECT (jsonb_populate_record(NULL::"HR_EmployeeCompensationRevisionLines", to_jsonb(l) || jsonb_build_object(
			  'Id', gen_random_uuid(), 'RevisionId', r."Id", 'EmployeeId', r."EmployeeId"))).*
			FROM "HR_EmployeeCompensationRevisionLines" l CROSS JOIN "HR_EmployeeCompensationRevisions" r
			JOIN "AppEmployees" e ON e."Id" = r."EmployeeId"
			WHERE l."RevisionId" = {source.Id} AND e."EmployeeCode" LIKE 'PERF-' || {batch} || '-%';
			INSERT INTO "AppEmployeeAttendances" ("Id", "TenantId", "EmployeeId", "AttendanceDate", "StatusType", "Source", "Remarks", "CreationTime", "IsDeleted")
			SELECT gen_random_uuid(), e."TenantId", e."Id", d::date, 'Present', 0, 'Synthetic performance fixture', NOW(), false
			FROM "AppEmployees" e CROSS JOIN generate_series('2026-08-01'::date, '2026-09-30'::date, '1 day') d
			WHERE e."EmployeeCode" LIKE 'PERF-' || {batch} || '-%';
			""");
	});

	[PayrollPostgresFact]
	public async Task Expired_worker_cannot_renew_or_release_its_replacements_lease()
	{
		const string month = "2026-12";
		var firstOwns = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var replacementOwns = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseReplacement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var first = WithLeaseAsync(month, async check =>
		{
			firstOwns.SetResult();
			await replacementOwns.Task.WaitAsync(TimeSpan.FromSeconds(30));
			(await Should.ThrowAsync<BusinessException>(check)).Code.ShouldBe("PAYROLL_PREVIEW_STALE");
			return true;
		});
		await firstOwns.Task.WaitAsync(TimeSpan.FromSeconds(30));
		await WithDatabaseAsync(async db =>
		{
			var lease = await db.Set<PayrollMonthLock>().SingleAsync(x => x.MonthYear == month);
			lease.Renew(DateTime.UtcNow.AddMinutes(-1));
			await db.SaveChangesAsync();
		});
		Guid replacementId = Guid.Empty;
		var replacement = WithLeaseAsync(month, async check =>
		{
			await check();
			await WithDatabaseAsync(async db =>
			{
				var lease = await db.Set<PayrollMonthLock>().SingleAsync(x => x.MonthYear == month);
				replacementId = lease.Id;
				lease.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(10));
			});
			replacementOwns.SetResult();
			await releaseReplacement.Task.WaitAsync(TimeSpan.FromSeconds(30));
			return true;
		});
		try
		{
			await first;
			await WithDatabaseAsync(async db =>
				(await db.Set<PayrollMonthLock>().SingleAsync(x => x.MonthYear == month)).Id.ShouldBe(replacementId));
		}
		finally { releaseReplacement.TrySetResult(); }
		await replacement;
		await WithDatabaseAsync(async db =>
			(await db.Set<PayrollMonthLock>().AnyAsync(x => x.MonthYear == month)).ShouldBeFalse());
	}

	[PayrollPostgresFact]
	public async Task Database_failure_is_not_reported_as_month_contention()
	{
		factory.Barrier.FailNextLeaseRead();
		var watch = Stopwatch.StartNew();
		await Should.ThrowAsync<NpgsqlException>(() => WithLeaseAsync("2027-01", _ => Task.FromResult(true)));
		watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
	}

	[PayrollPostgresFact]
	public async Task Abandoned_attempt_recovers_and_retry_budget_is_bounded()
	{
		var recoverable = await StartAsync("2026-10");
		await WithDatabaseAsync(async db =>
		{
			var run = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == recoverable.Id);
			run.Start(Guid.NewGuid(), DateTime.UtcNow.AddHours(-1));
			await db.SaveChangesAsync();
		});
		await ExecuteAsync(recoverable.Id);
		var recovered = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{recoverable.Id}");
		recovered.Status.ShouldBe("SUCCEEDED", recovered.ErrorMessage);
		recovered.IsCurrent.ShouldBeTrue();

		var exhausted = await StartAsync("2026-11");
		await WithDatabaseAsync(async db =>
		{
			var run = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == exhausted.Id);
			for (var attempt = 0; attempt < 3; attempt++) run.Start(Guid.NewGuid(), DateTime.UtcNow.AddHours(-1));
			await db.SaveChangesAsync();
		});
		await ExecuteAsync(exhausted.Id);
		var failed = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{exhausted.Id}");
		failed.Status.ShouldBe("FAILED");
		failed.IsCurrent.ShouldBeFalse();
		await WithDatabaseAsync(async db =>
		{
			(await db.PayrollPreviewRuns.SingleAsync(x => x.Id == exhausted.Id)).Attempts.ShouldBe(3);
			(await db.PayrollPreviewEmployees.CountAsync(x => x.RunId == exhausted.Id)).ShouldBe(0);
		});
	}

	private async Task<T> WithLeaseAsync<T>(string month, Func<Func<Task>, Task<T>> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		return await scope.ServiceProvider.GetRequiredService<PayrollSnapshotLockProvider>().WithMonthLockAsync(month, action);
	}

	[PayrollPostgresFact]
	public async Task Concurrent_starts_share_one_job_and_paged_results_keep_full_summary()
	{
		var requests = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => StartAsync("2026-07")));
		requests.Select(x => x.Id).Distinct().Count().ShouldBe(1);
		var run = requests[0];
		await WithDatabaseAsync(async db =>
		{
			(await db.PayrollPreviewRuns.CountAsync(x => x.Id == run.Id)).ShouldBe(1);
			var jobs = await db.Database.SqlQueryRaw<int>(
				"""SELECT COUNT(*)::int AS "Value" FROM "AbpBackgroundJobs" WHERE "JobArgs" LIKE {0}""",
				$"%{run.Id}%").SingleAsync();
			jobs.ShouldBe(1);
		});
		var watch = Stopwatch.StartNew();
		await ExecuteAsync(run.Id);
		output.WriteLine($"Cold calculation: {run.EmployeeCount} employees in {watch.Elapsed.TotalSeconds:F2}s.");
		var status = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{run.Id}");
		status.Status.ShouldBe("SUCCEEDED", status.ErrorMessage);
		status.IsCurrent.ShouldBeTrue();
		var first = await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{run.Id}/employees?page=0&pageSize=10");
		var empty = await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{run.Id}/employees?page=0&pageSize=10&search=__no_matches__");
		first.Items.Count.ShouldBeLessThanOrEqualTo(10);
		empty.TotalCount.ShouldBe(0);
		empty.Readiness!.EmployeeCount.ShouldBe(first.Readiness!.EmployeeCount);
		first.Readiness.EmployeeCount.ShouldBe(run.EmployeeCount);
		(await StartAsync("2026-07")).Id.ShouldBe(run.Id);
		using var otherTenant = Factory.CreateClient();
		otherTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await otherTenant.GetAsync($"{Previews}/{run.Id}")).StatusCode.ShouldNotBe(HttpStatusCode.OK);
	}

	[PayrollPostgresFact]
	public async Task Attendance_changed_during_calculation_cannot_publish_or_lock_stale_results()
	{
		var run = await StartAsync("2026-06");
		factory.Barrier.Arm();
		var calculation = ExecuteAsync(run.Id);
		try
		{
			await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
			await ChangeAttendanceAsync(new DateOnly(2026, 6, 2));
		}
		finally { factory.Barrier.Release(); }
		await calculation;
		await WithDatabaseAsync(async db =>
		{
			var stored = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == run.Id);
			stored.Status.ShouldBe("QUEUED");
			stored.Attempts.ShouldBe(1);
			(await db.PayrollPreviewEmployees.CountAsync(x => x.RunId == run.Id)).ShouldBe(0);
		});
		// A retry is a new attempt and must calculate against the new input revision.
		await ExecuteAsync(run.Id);
		var status = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{run.Id}");
		status.Status.ShouldBe("SUCCEEDED", status.ErrorMessage);
		status.IsCurrent.ShouldBeTrue();
		var page = await GetAndReadAsync<RunPayrollResponse>($"{Previews}/{run.Id}/employees?pageSize=10");
		var selected = page.Items.First().EmployeeId;
		await ChangeAttendanceAsync(new DateOnly(2026, 6, 2));
		var body = new { month = run.Month, run.AcademicYearId, previewRunId = run.Id, employeeIds = new[] { selected } };
		foreach (var endpoint in new[] { "/api/payroll/run/reviews", "/api/payroll/run" })
		{
			var rejected = await Client.PostAsJsonAsync(endpoint, body);
			rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict, await rejected.Content.ReadAsStringAsync());
			(await rejected.Content.ReadAsStringAsync()).ShouldContain("PAYROLL_PREVIEW_STALE");
		}
		(await GetAndReadAsync<PayrollPreviewStatusDto>($"{Previews}/{run.Id}")).IsCurrent.ShouldBeFalse();
	}

	private async Task<PayrollPreviewStatusDto> StartAsync(string month)
	{
		var watch = Stopwatch.StartNew();
		var response = await Client.PostAsJsonAsync(Previews, new { month });
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		output.WriteLine($"Start {month}: {watch.Elapsed.TotalMilliseconds:F0}ms");
		return (await response.Content.ReadFromJsonAsync<PayrollPreviewStatusDto>(JsonOptions))!;
	}

	private async Task ExecuteAsync(Guid runId)
	{
		using var scope = Factory.Services.CreateScope();
		await scope.ServiceProvider.GetRequiredService<PayrollPreviewBackgroundJob>().ExecuteAsync(
			new PayrollPreviewJobArgs { TenantId = TenantId, RunId = runId });
	}

	private Task ChangeAttendanceAsync(DateOnly date) => WithDatabaseAsync(async db =>
	{
		var employee = await db.Set<Employee>().FirstAsync(x => x.UserId == Guid.Parse(TestAuthHandler.TestUserId));
		var attendance = await db.Set<EmployeeAttendance>().FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.AttendanceDate == date);
		if (attendance == null)
			db.Set<EmployeeAttendance>().Add(new EmployeeAttendance
			{
				TenantId = TenantId, EmployeeId = employee.Id, AttendanceDate = date,
				StatusType = EmployeeAttendanceStatusType.Present, Remarks = "Synthetic concurrency fixture",
			});
		else
			attendance.StatusType = attendance.StatusType == EmployeeAttendanceStatusType.Present
				? EmployeeAttendanceStatusType.FullDayLeave : EmployeeAttendanceStatusType.Present;
		await db.SaveChangesAsync();
	});

	private async Task WithDatabaseAsync(Func<ShikshaMitraDbContext, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var db = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		await action(db);
		await uow.CompleteAsync();
	}
}
