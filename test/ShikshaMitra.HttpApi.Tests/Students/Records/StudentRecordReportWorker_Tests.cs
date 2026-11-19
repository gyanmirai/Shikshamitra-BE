using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using ShikshaMitra.BackgroundWorkers;
using Shouldly;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

[Trait("Module", "Students"), Trait("Tier", "Critical")]
public class StudentRecordReportWorker_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Timed_out_poll_disposes_failed_read_and_recovers_on_next_tick(bool wrapped, bool duringEnumeration)
	{
		Exception timeout = new NpgsqlException("Read failed", new TimeoutException("Timeout during reading attempt"));
		if (wrapped) timeout = new InvalidOperationException("Transient database failure", timeout);
		using var fixture = new Fixture(timeout, duringEnumeration);

		await fixture.Worker.TickAsync(fixture.Services);
		fixture.FailedRead.Received(1).Dispose();
		fixture.FailedFilter.Received(1).Dispose();
		await fixture.FailedRead.DidNotReceive().CompleteAsync(Arg.Any<CancellationToken>());

		await fixture.Worker.TickAsync(fixture.Services);
		fixture.QueryAttempts.ShouldBe(2);
		await fixture.NextRead.Received(1).CompleteAsync(Arg.Any<CancellationToken>());
		fixture.NextRead.Received(1).Dispose();
		fixture.NextFilter.Received(1).Dispose();
	}

	[Theory]
	[InlineData("schema")]
	[InlineData("application")]
	[InlineData("cancellation")]
	public async Task Permanent_errors_and_cancellation_are_not_treated_as_temporary_outages(string kind)
	{
		Exception failure = kind switch
		{
			"schema" => new PostgresException("Missing relation", "ERROR", "ERROR", "42P01"),
			"cancellation" => new OperationCanceledException(),
			_ => new InvalidOperationException("Invalid query")
		};
		using var fixture = new Fixture(failure, false);
		var actual = await Record.ExceptionAsync(() => fixture.Worker.TickAsync(fixture.Services));
		actual.ShouldBeSameAs(failure);
		fixture.FailedRead.Received(1).Dispose();
		fixture.FailedFilter.Received(1).Dispose();
	}

	private sealed class Fixture : IDisposable
	{
		public ServiceProvider Services { get; }
		public TestWorker Worker { get; }
		public IUnitOfWork FailedRead { get; } = Substitute.For<IUnitOfWork>();
		public IUnitOfWork NextRead { get; } = Substitute.For<IUnitOfWork>();
		public IDisposable FailedFilter { get; } = Substitute.For<IDisposable>();
		public IDisposable NextFilter { get; } = Substitute.For<IDisposable>();
		public int QueryAttempts { get; private set; }

		public Fixture(Exception failure, bool duringEnumeration)
		{
			var repository = Substitute.For<IRepository<StudentRecordReportJob, Guid>>();
			repository.GetQueryableAsync().Returns(_ =>
			{
				QueryAttempts++;
				if (QueryAttempts != 1) return Task.FromResult(Array.Empty<StudentRecordReportJob>().AsQueryable());
				return duringEnumeration
					? Task.FromResult(FailingRows(failure).AsQueryable())
					: Task.FromException<IQueryable<StudentRecordReportJob>>(failure);
			});
			var units = Substitute.For<IUnitOfWorkManager>();
			units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(FailedRead, NextRead);
			var filter = Substitute.For<IDataFilter>();
			filter.Disable<IMultiTenant>().Returns(FailedFilter, NextFilter);
			Services = new ServiceCollection()
				.AddSingleton(repository).AddSingleton(units).AddSingleton(filter)
				.AddSingleton<IAsyncQueryableExecuter>(new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>()))
				.AddSingleton<ILogger<StudentRecordReportWorker>>(NullLogger<StudentRecordReportWorker>.Instance)
				.BuildServiceProvider();
			Worker = new TestWorker(new AbpAsyncTimer(), Services.GetRequiredService<IServiceScopeFactory>());
		}

		private static IEnumerable<StudentRecordReportJob> FailingRows(Exception failure)
		{
			yield return new StudentRecordReportJob(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "poll-test", "test", "{}", "test", DateTimeOffset.UtcNow.AddHours(1));
			throw failure;
		}

		public void Dispose() => Services.Dispose();
	}

	private sealed class TestWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : StudentRecordReportWorker(timer, scopes)
	{
		public Task TickAsync(IServiceProvider provider) => DoWorkAsync(new PeriodicBackgroundWorkerContext(provider));
	}
}
