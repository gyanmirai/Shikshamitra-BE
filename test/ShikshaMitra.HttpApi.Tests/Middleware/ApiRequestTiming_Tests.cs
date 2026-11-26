using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Diagnostics;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Middleware;

public class ApiRequestTiming_Tests
{
	[Fact]
	public async Task Should_Report_Real_Database_Timing_Without_Logging_Request_Or_Sql_Secrets()
	{
		var observer = new ApiRequestTimingObserver();
		await observer.StartAsync(CancellationToken.None);
		try
		{
			var logger = new RecordingLogger();
			using var server = CreateServer(logger, async context =>
			{
				context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
					RoutePatternFactory.Parse("/api/test/{id}"), 0, null, "Timing test"));
				await using var database = new TimingDbContext(new DbContextOptionsBuilder<TimingDbContext>()
					.UseSqlite("Data Source=:memory:").Options);
				await database.Database.ExecuteSqlRawAsync("SELECT 'private-sql-value'");
				context.Response.StatusCode = StatusCodes.Status202Accepted;
				await context.Response.WriteAsync("unchanged response");
			});
			using var client = server.CreateClient();
			client.DefaultRequestHeaders.Authorization = new("Bearer", "private-token");
			using var response = await client.GetAsync("/api/test/private-applicant?password=private-query");
			response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
			(await response.Content.ReadAsStringAsync()).ShouldBe("unchanged response");
			var timing = string.Join(",", response.Headers.GetValues("Server-Timing"));
			timing.ShouldContain("app;dur=");
			timing.ShouldContain("db;dur=");
			timing.ShouldContain("db-open;dur=");
			logger.Messages.Count.ShouldBe(1);
			var message = logger.Messages[0];
			message.ShouldContain("DbCommandCount=1");
			message.ShouldContain("Route=/api/test/{id}");
			message.ShouldNotContain("private-");
			message.ShouldNotContain("SELECT");
		}
		finally
		{
			await observer.StopAsync(CancellationToken.None);
		}
	}

	[Fact]
	public async Task Should_Report_Payroll_Phases_Only_For_Matching_Reads()
	{
		var observer = new ApiRequestTimingObserver();
		await observer.StartAsync(CancellationToken.None);
		try
		{
			var logger = new RecordingLogger();
			using var server = CreateServer(logger, async context =>
			{
				var scope = ApiRequestTimingScope.Current;
				scope!.RecordPayrollCheckpoint(PayrollReadCheckpoint.RoutingCompleted);
				scope.RecordPayrollCheckpoint(PayrollReadCheckpoint.AuthenticationCompleted);
				PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.PermissionCheckStarted);
				PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.PermissionIdentityResolved);
				PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.PermissionProvidersChecked);
				scope.RecordPayrollCheckpoint(PayrollReadCheckpoint.AuthorizationCompleted);
				PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ActionStarted);
				await Task.Yield();
				PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ServiceStarted);
				PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.PageRead);
				PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ActionCompleted);
				await context.Response.WriteAsync("unchanged response");
			});
			using var client = server.CreateClient();
			using var payroll = await client.GetAsync("/api/payroll/run/register?month=private-value");
			var payrollTiming = string.Join(",", payroll.Headers.GetValues("Server-Timing"));
			payrollTiming.ShouldContain("payroll-routing;dur=");
			payrollTiming.ShouldContain("payroll-authentication;dur=");
			payrollTiming.ShouldContain("payroll-policy-resolution;dur=");
			payrollTiming.ShouldContain("payroll-permission-identity;dur=");
			payrollTiming.ShouldContain("payroll-permission-providers;dur=");
			payrollTiming.ShouldContain("payroll-authorization-end;dur=");
			payrollTiming.ShouldContain("payroll-mvc-action-filters;dur=");
			payrollTiming.ShouldContain("payroll-service-entry;dur=");
			payrollTiming.ShouldContain("payroll-page;dur=");
			payrollTiming.ShouldContain("payroll-action-exit;dur=");
			payrollTiming.ShouldContain("payroll-response-headers;dur=");
			payrollTiming.ShouldNotContain("payroll-count;dur=");
			payrollTiming.ShouldNotContain("private-value");
			logger.Messages[0].ShouldContain("PayrollPhases=payroll-routing");
			logger.Messages[0].ShouldNotContain("private-value");

			using var other = await client.GetAsync("/api/test");
			string.Join(",", other.Headers.GetValues("Server-Timing")).ShouldNotContain("payroll-");
		}
		finally
		{
			await observer.StopAsync(CancellationToken.None);
		}
	}

	[Fact]
	public async Task Should_Observe_Framework_Mvc_Events_Only_On_Payroll_Reads()
	{
		using var server = new TestServer(new WebHostBuilder()
			.ConfigureServices(services =>
			{
				services.AddControllers().AddApplicationPart(typeof(PayrollMvcTimingTestController).Assembly);
				services.AddHostedService<ApiRequestTimingObserver>();
			})
			.Configure(app =>
			{
				app.UseMiddleware<ApiRequestTimingMiddleware>();
				app.UseRouting();
				app.UseEndpoints(endpoints => endpoints.MapControllers());
			}));
		using var client = server.CreateClient();
		using var payroll = await client.GetAsync("/api/payroll/run/previews/current?month=private-value");
		payroll.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await payroll.Content.ReadAsStringAsync()).ShouldBe("private-value");
		var timing = string.Join(",", payroll.Headers.GetValues("Server-Timing"));
		timing.ShouldContain("payroll-mvc-invoker-setup;dur=");
		timing.ShouldContain("payroll-mvc-controller-activation;dur=");
		timing.ShouldContain("payroll-mvc-model-binding;dur=");
		timing.ShouldContain("payroll-mvc-action-filters;dur=");
		timing.ShouldNotContain("private-value");

		using var unrelated = await client.GetAsync("/api/timing-test?month=other");
		unrelated.StatusCode.ShouldBe(HttpStatusCode.OK);
		string.Join(",", unrelated.Headers.GetValues("Server-Timing")).ShouldNotContain("payroll-");
	}

	[Fact]
	public async Task Should_Leave_NonApi_Requests_And_Exceptions_Unchanged()
	{
		var logger = new RecordingLogger();
		using var server = CreateServer(logger, context => context.Response.WriteAsync("ok"));
		using var client = server.CreateClient();
		using var response = await client.GetAsync("/health");
		response.Headers.Contains("Server-Timing").ShouldBeFalse();
		logger.Messages.ShouldBeEmpty();

		var expected = new InvalidOperationException("test exception");
		var context = new DefaultHttpContext();
		context.Request.Path = "/api/test";
		var middleware = new ApiRequestTimingMiddleware(_ => throw expected, logger, new ConfigurationBuilder().Build());
		var actual = await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
		actual.ShouldBeSameAs(expected);
		ApiRequestTimingScope.Current.ShouldBeNull();
	}

	[Fact]
	public async Task Concurrent_Request_Counters_Should_Remain_Isolated_And_Restore_Outer_Scope()
	{
		using var outer = ApiRequestTimingScope.Start();
		async Task MeasureAsync(int count)
		{
			using var scope = ApiRequestTimingScope.Start();
			await Task.Yield();
			for (var i = 0; i < count; i++) scope.RecordCommand(TimeSpan.FromMilliseconds(i + 1));
			scope.CommandCount.ShouldBe(count);
			scope.MaxCommandMilliseconds.ShouldBe(count);
		}
		await Task.WhenAll(MeasureAsync(2), MeasureAsync(3));
		ApiRequestTimingScope.Current.ShouldBeSameAs(outer);
		outer.CommandCount.ShouldBe(0);
	}

	[Theory]
	[InlineData("invalid")]
	[InlineData("NaN")]
	[InlineData("-1")]
	public async Task Invalid_Threshold_Should_Not_Break_Api_Responses(string threshold)
	{
		var logger = new RecordingLogger();
		using var server = CreateServer(logger, context => context.Response.WriteAsync("ok"), threshold);
		using var client = server.CreateClient();
		using var response = await client.GetAsync("/api/test");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		logger.Messages.ShouldBeEmpty();
	}

	private static TestServer CreateServer(RecordingLogger logger, RequestDelegate handler, string threshold = "0") => new(
		new WebHostBuilder()
			.ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["PerformanceDiagnostics:SlowRequestMilliseconds"] = threshold
			}))
			.ConfigureServices(services => services.AddSingleton<ILogger<ApiRequestTimingMiddleware>>(logger))
			.Configure(app => { app.UseMiddleware<ApiRequestTimingMiddleware>(); app.Run(handler); }));

	private sealed class TimingDbContext(DbContextOptions<TimingDbContext> options) : DbContext(options);
	private sealed class RecordingLogger : ILogger<ApiRequestTimingMiddleware>
	{
		public List<string> Messages { get; } = [];
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
	}
}

[ApiController]
public sealed class PayrollMvcTimingTestController : ControllerBase
{
	public PayrollMvcTimingTestController() => PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ControllerActivated);

	[HttpGet("/api/payroll/run/previews/current")]
	[HttpGet("/api/timing-test")]
	public IActionResult Get([FromQuery] string month)
	{
		PayrollReadDiagnostics.Mark(PayrollReadCheckpoint.ActionStarted);
		return Content(month);
	}
}
