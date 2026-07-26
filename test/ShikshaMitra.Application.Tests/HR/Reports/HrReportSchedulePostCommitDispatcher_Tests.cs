using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class HrReportSchedulePostCommitDispatcher_Tests
{
	[Fact]
	public async Task Dispatch_Should_Wait_For_The_Current_Unit_Of_Work_To_Commit()
	{
		var unitOfWorkManager = Substitute.For<IUnitOfWorkManager>();
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWorkManager.Current.Returns(unitOfWork);
		Func<Task>? completion = null;
		unitOfWork
			.When(current => current.OnCompleted(Arg.Any<Func<Task>>()))
			.Do(call => completion = call.Arg<Func<Task>>());
		var dispatcher = CreateDispatcher(unitOfWorkManager);
		var deliveryCount = 0;

		await dispatcher.DispatchAsync(
			() =>
			{
				deliveryCount++;
				return Task.CompletedTask;
			},
			Guid.NewGuid());

		deliveryCount.ShouldBe(0);
		completion.ShouldNotBeNull();
		await completion();
		deliveryCount.ShouldBe(1);
	}

	[Fact]
	public async Task Dispatch_Should_Run_Immediately_Without_An_Ambient_Unit_Of_Work()
	{
		var unitOfWorkManager = Substitute.For<IUnitOfWorkManager>();
		unitOfWorkManager.Current.Returns((IUnitOfWork?)null);
		var dispatcher = CreateDispatcher(unitOfWorkManager);
		var delivered = false;

		await dispatcher.DispatchAsync(
			() =>
			{
				delivered = true;
				return Task.CompletedTask;
			},
			Guid.NewGuid());

		delivered.ShouldBeTrue();
	}

	[Fact]
	public async Task Post_Commit_Delivery_Failure_Should_Remain_Best_Effort()
	{
		var unitOfWorkManager = Substitute.For<IUnitOfWorkManager>();
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWorkManager.Current.Returns(unitOfWork);
		Func<Task>? completion = null;
		unitOfWork
			.When(current => current.OnCompleted(Arg.Any<Func<Task>>()))
			.Do(call => completion = call.Arg<Func<Task>>());
		var dispatcher = CreateDispatcher(unitOfWorkManager);

		await dispatcher.DispatchAsync(
			() => Task.FromException(new InvalidOperationException("SMTP unavailable")),
			Guid.NewGuid());

		completion.ShouldNotBeNull();
		await completion();
	}

	private static HrReportSchedulePostCommitDispatcher CreateDispatcher(
		IUnitOfWorkManager unitOfWorkManager) =>
		new(
			unitOfWorkManager,
			Substitute.For<ILogger<HrReportSchedulePostCommitDispatcher>>());
}
