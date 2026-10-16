using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Diagnostics;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Middleware;

public class ApiRequestTiming_Tests
{
	[Fact]
	public async Task Should_Report_Real_Database_Timing_Without_Logging_Request_Or_Sql_Secrets()
	{
		var observer = new ApiDatabaseTimingObserver();
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
