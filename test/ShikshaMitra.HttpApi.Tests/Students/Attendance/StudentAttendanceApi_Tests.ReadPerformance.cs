using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public partial class StudentAttendanceApi_Tests
{
	[Fact]
	public async Task Daily_facts_opt_out_skips_annual_report_queries_and_preserves_roster_and_action_capabilities()
	{
		var context = await ResolveSeededDivisionContextAsync(currentYear: true);
		var division = await CreateDivisionAsync(context.StandardId, "DailyReadPerformance");
		var student = await CreateEnrolledStudentAsync(context, "Daily facts performance", division);
		await EnsureWorkingDateAsync(context, context.ReferenceDate);
		DbConnection connection;
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var work = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var db = await services.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			connection = db.Database.GetDbConnection();
			await work.CompleteAsync();
		}
		var url = $"{AttendanceBaseUrl}/students?academicYearId={context.AcademicYearId}&standardId={context.StandardId}&divisionId={division}&date={context.ReferenceDate:yyyy-MM-dd}";
		StudentAttendanceListDto legacy;
		int legacyAnnualReads;
		using (var reads = new AttendanceReportReadRecorder(connection))
		{
			var response = await Client.GetAsync(url);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			legacy = (await response.Content.ReadFromJsonAsync<StudentAttendanceListDto>(JsonOptions))!;
			legacyAnnualReads = reads.AnnualReads;
		}
		legacyAnnualReads.ShouldBeGreaterThan(0);
		legacy.Students.Single(row => row.StudentId == student.StudentId).ExpectedDays.ShouldBeGreaterThan(0);
		using var dailyReads = new AttendanceReportReadRecorder(connection);
		var dailyResponse = await Client.GetAsync(url + "&includeCumulativeSummary=false");
		dailyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await dailyResponse.Content.ReadAsStringAsync());
		var daily = (await dailyResponse.Content.ReadFromJsonAsync<StudentAttendanceListDto>(JsonOptions))!;
		foreach (var row in legacy.Students)
		{
			row.PresentDays = 0; row.AttendancePercentage = null; row.ExpectedDays = 0;
			row.RecordedDays = 0; row.MissingDays = 0; row.IsProvisional = true;
		}
		JsonSerializer.Serialize(daily, JsonOptions).ShouldBe(JsonSerializer.Serialize(legacy, JsonOptions));
		Console.WriteLine($"Synthetic HTTP daily roster; legacy annual entry reads={legacyAnnualReads}, daily-facts annual entry reads={dailyReads.AnnualReads}.");
		dailyReads.AnnualReads.ShouldBe(0);
	}

	private sealed class AttendanceReportReadRecorder : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
	{
		private readonly DbConnection connection;
		private readonly IDisposable listeners;
		private readonly ConcurrentBag<IDisposable> subscriptions = [];
		private readonly ConcurrentBag<string> commands = [];
		public AttendanceReportReadRecorder(DbConnection connection)
		{
			this.connection = connection;
			listeners = DiagnosticListener.AllListeners.Subscribe(this);
		}
		public int AnnualReads => commands.Count(sql => sql.Contains("AppAttendanceEntries", StringComparison.Ordinal) &&
			sql.Contains("\"Date\" >=", StringComparison.Ordinal) && sql.Contains("\"Date\" <=", StringComparison.Ordinal));
		public void OnNext(DiagnosticListener listener)
		{
			if (listener.Name == "Microsoft.EntityFrameworkCore")
				subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
		}
		public void OnNext(KeyValuePair<string, object?> value)
		{
			if (value.Value is CommandExecutedEventData data && data.Context != null &&
				ReferenceEquals(data.Context.Database.GetDbConnection(), connection)) commands.Add(data.Command.CommandText);
		}
		public void OnCompleted() { }
		public void OnError(Exception error) { }
		public void Dispose() { listeners.Dispose(); foreach (var item in subscriptions) item.Dispose(); }
	}
}
