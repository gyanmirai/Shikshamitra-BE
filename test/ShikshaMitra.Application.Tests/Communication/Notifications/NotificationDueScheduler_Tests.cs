using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDueScheduler_Tests
{
	private const string EventKey = "Academic.Homework.Due";
	private const string SourceType = "Academic.Homework";
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly NotificationSourceScopeDto _scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
	private readonly INotificationDueSource _source = Substitute.For<INotificationDueSource>();
	private readonly INotificationIntentWriter _writer = Substitute.For<INotificationIntentWriter>();
	private readonly List<NotificationScheduleCursor> _cursorRows = [];
	private readonly NotificationRule _rule;
	private readonly NotificationDueScheduler _scheduler;

	public NotificationDueScheduler_Tests()
	{
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(_tenantId);
		var source = Substitute.For<INotificationSourceAdapter>(); source.EventKeys.Returns(new[] { EventKey });
		var catalogue = new AcademicNotificationCatalogue([source]);
		var codec = new NotificationRuleConfigurationCodec();
		_rule = new NotificationRule(Guid.NewGuid(), _tenantId, EventKey, NotificationRuleScope.Create());
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.InApp], [Guid.NewGuid()], NotificationRuleTimingConfiguration.Default, "en");
		_rule.Configure(true, codec.Serialize(config, catalogue.Get(EventKey)), _rule.ConcurrencyStamp);
		_source.SourceType.Returns(SourceType);
		var unit = Substitute.For<IUnitOfWork>();
		var units = Substitute.For<IUnitOfWorkManager>();
		units.Current.Returns((IUnitOfWork?)null);
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(unit);
		var cursors = Repository(_cursorRows);
		cursors.InsertAsync(Arg.Any<NotificationScheduleCursor>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var cursor = call.ArgAt<NotificationScheduleCursor>(0); _cursorRows.Add(cursor); return cursor;
		});
		var executer = new AsyncQueryableExecuter([]);
		var calendar = new AcademicNotificationWorkingDays(tenant, executer, Repository<AcademicYear>([]), Repository<AcademicCalendar>([]),
			Repository<AcademicCalendarDay>([]), Substitute.For<ILectureSlotConfigRepository>(), Substitute.For<ITimetableRepository>());
		var guids = Substitute.For<IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		_scheduler = new NotificationDueScheduler(tenant, units, Substitute.For<INotificationPersonalWriteLock>(), cursors, executer, guids,
			[_source], [catalogue], new(tenant, Repository(new[] { _rule }), executer), codec, new(tenant, Substitute.For<ISettingManager>()),
			calendar, new(), _writer);
	}

	[Fact]
	public async Task Bounded_scans_resume_the_same_cycle_then_start_a_new_cycle_for_future_work()
	{
		var due = Candidate(Now.AddHours(-1), Now.AddHours(1));
		var expired = Candidate(Now.AddHours(-2), Now);
		var future = Candidate(Now.AddMinutes(30), Now.AddHours(1));
		var windows = new List<NotificationDueScanRequest>();
		_source.ReadPageAsync(Arg.Any<NotificationDueScanRequest>(), Arg.Any<CancellationToken>()).Returns(call => {
			var request = call.ArgAt<NotificationDueScanRequest>(0); windows.Add(request);
			return request.Continuation == null ? new NotificationDueSourcePage([due, expired], "next") : new([future], null);
		});
		_writer.WriteWithOutcomeAsync(Arg.Is<NotificationEventInput>(x => x.SourceId == due.SourceId), Arg.Any<CancellationToken>())
			.Returns(new NotificationIntentWriteResult(Guid.NewGuid(), true));

		(await _scheduler.ScanAsync(Now, 2, default)).ShouldBe(new NotificationScanResult(2, 1, 1, true));
		(await _scheduler.ScanAsync(Now.AddSeconds(1), 2, default)).Examined.ShouldBe(0);
		(await _scheduler.ScanAsync(Now.AddMinutes(1), 2, default)).ShouldBe(new NotificationScanResult(1, 0, 1, false));
		(await _scheduler.ScanAsync(Now.AddMinutes(2), 2, default)).Examined.ShouldBe(2);

		windows.Count.ShouldBe(3);
		windows[1].Continuation.ShouldBe("next");
		windows[1].CycleStartedAt.ShouldBe(Now);
		windows[1].Now.ShouldBe(Now.AddMinutes(1));
		windows[2].CycleStartedAt.ShouldBe(Now.AddMinutes(2));
		windows[2].Continuation.ShouldBeNull();
		_cursorRows.Count.ShouldBe(1);
		await _writer.Received(2).WriteWithOutcomeAsync(Arg.Is<NotificationEventInput>(x => x.SourceId == due.SourceId &&
			x.DueAt == due.OccurrenceAt && x.ExpiresAt == due.ExpiresAt &&
			x.DueOccurrence == new NotificationDueOccurrenceDto(_scope, due.OccurrenceAt, _rule.Id, _rule.Revision)), Arg.Any<CancellationToken>());
		await _writer.DidNotReceive().WriteWithOutcomeAsync(Arg.Is<NotificationEventInput>(x => x.SourceId != due.SourceId), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Repeated_continuation_leaves_the_existing_cursor_unchanged()
	{
		_source.ReadPageAsync(Arg.Any<NotificationDueScanRequest>(), Arg.Any<CancellationToken>())
			.Returns(new NotificationDueSourcePage([], "same"));
		await _scheduler.ScanAsync(Now, 2, default);
		var next = _cursorRows.Single().NextScanAt;

		var error = await Should.ThrowAsync<BusinessException>(() => _scheduler.ScanAsync(Now.AddMinutes(1), 2, default));

		error.Code.ShouldBe(NotificationErrorCodes.StateConflict);
		_cursorRows.Single().NextScanAt.ShouldBe(next);
	}

	private NotificationDueSourceCandidate Candidate(DateTimeOffset at, DateTimeOffset expires) =>
		new(EventKey, Guid.NewGuid(), "source-1", _scope, at, expires, NotificationDueTimeMode.FixedOccurrence);

	private static IRepository<T, Guid> Repository<T>(IEnumerable<T> rows) where T : class, IEntity<Guid>
	{
		var repo = Substitute.For<IRepository<T, Guid>>();
		repo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		return repo;
	}
}
