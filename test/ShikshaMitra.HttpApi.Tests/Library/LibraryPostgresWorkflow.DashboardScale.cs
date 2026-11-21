using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp.SettingManagement;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private static Task ConfigureFinancePgDashboardScaleClockAsync(LibraryPostgresApiTestFactory factory) =>
		InFinancePgAsync(factory, async services =>
		{
			await services.GetRequiredService<ISettingManager>().SetForTenantAsync(FinancePgTenantId,
				LibraryPolicySettingNames.CalendarTimeZone, "UTC", forceToSet: true);
			return 0;
		});

	private async Task<FinanceDashboardLargeMeasurement> MeasureFinancePgPublicDashboardAsync(
		LibraryPostgresApiTestFactory factory, int population, string anchorLink)
	{
		using var client = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Library.Dashboard.View);
		using var serverTracing = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded
		};
		ActivitySource.AddActivityListener(serverTracing);
		using var activity = new Activity("Native public Dashboard scale").SetIdFormat(ActivityIdFormat.W3C).Start();
		// TestServer does not preserve the caller's AsyncLocal context. Propagate
		// only correlation, leaving identity and tenant resolution to HTTP headers.
		client.DefaultRequestHeaders.Add("traceparent", activity.Id!);
		using var capture = new FinanceConsumerPgSqlCapture(activity.TraceId, captureAnySourceOrFinance: true);
		var watch = Stopwatch.StartNew();
		using var response = await client.GetAsync($"/api/library/dashboard/overview?academicYearId={factory.YearId:D}");
		watch.Stop();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		var overview = document.RootElement;
		overview.GetProperty("schoolDateSource").GetString().ShouldBe("ConfirmedLibraryClock");
		var schoolDay = DateOnly.ParseExact(overview.GetProperty("schoolAsOfDate").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
		var summary = overview.GetProperty("financePosition");
		summary.GetProperty("verifiedCount").GetInt32().ShouldBe(population);
		summary.GetProperty("unpostedCount").GetInt32().ShouldBe(0);
		summary.GetProperty("needsReviewCount").GetInt32().ShouldBe(0);
		var total = summary.GetProperty("currencyTotals").EnumerateArray().ShouldHaveSingleItem();
		total.GetProperty("currency").GetString().ShouldBe("INR");
		var principal = population * 100.75m;
		foreach (var field in new[] { "originalAmountText", "financialOutstandingAmountText", "collectibleAmountText" })
			total.GetProperty(field).GetString().ShouldBe(principal.ToString("F2", CultureInfo.InvariantCulture));
		foreach (var field in new[] { "acceptedAmountText", "settledAmountText", "pendingVerificationAmountText", "netWaivedAmountText", "refundedAmountText" })
			total.GetProperty(field).GetString().ShouldBe("0.00");
		overview.GetProperty("charges").GetProperty("pendingCount").GetInt32().ShouldBe(population);
		overview.GetProperty("charges").GetProperty("pendingAmount").GetDecimal().ShouldBe(principal);
		var alerts = overview.GetProperty("alerts").EnumerateArray().ToArray();
		alerts.Where(alert => alert.GetProperty("type").GetString() == "pendingCharges")
			.ShouldHaveSingleItem().GetProperty("count").GetInt32().ShouldBe(population);
		using var link = JsonDocument.Parse(anchorLink);
		using var snapshot = JsonDocument.Parse(link.RootElement.GetProperty("SourceSnapshotJson").GetString()!);
		var anchorDue = DateOnly.ParseExact(snapshot.RootElement.GetProperty("DueDate").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
		var overdue = (new DateOnly(2026, 11, 24) < schoolDay ? population - 1 : 0) + (anchorDue < schoolDay ? 1 : 0);
		var overdueAlerts = alerts.Where(alert => alert.GetProperty("type").GetString() == "overdueCharges").ToArray();
		if (overdue == 0) overdueAlerts.ShouldBeEmpty();
		else overdueAlerts.ShouldHaveSingleItem().GetProperty("count").GetInt32().ShouldBe(overdue);
		alerts.ShouldNotContain(alert => alert.GetProperty("type").GetString() == "unpostedCharges" ||
			alert.GetProperty("type").GetString() == "reviewCharges");
		var commands = capture.Commands.ToArray();
		commands.Length.ShouldBe(3, "The public summary and alerts must share the three grouped financial reads.");
		commands.ShouldAllBe(command => command.CommandTimeout == 30, "The public read must retain the ordinary command timeout.");
		consumerOutput.WriteLine(JsonSerializer.Serialize(new
		{
			Stage = "public-dashboard-scale", Population = population, ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
			FinancialCommands = commands.Length, SqlLengths = commands.Select(command => command.Sql.Length).ToArray(),
			CommandTimeouts = commands.Select(command => command.CommandTimeout).ToArray(),
			ConfirmedSchoolDay = schoolDay, Overdue = overdue
		}));
		return new(commands.Select(command => command.Sql.Length).ToArray());
	}

	private sealed record FinanceDashboardLargeMeasurement(int[] SqlLengths);
}
