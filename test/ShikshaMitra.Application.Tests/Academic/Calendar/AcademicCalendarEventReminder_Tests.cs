using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public sealed class AcademicCalendarEventReminder_Tests : SubjectTeacherReassignmentTestBase
{
	private const string Key = AcademicNotificationEventKeys.CalendarEventReminder;

	[Fact]
	public async Task A_published_event_reminder_uses_the_preceding_open_school_day_and_rechecks_cancellation()
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, ActorId.ToString()), new Claim(AbpClaimTypes.Role, "admin"),
				new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
		var date = Today.AddDays(7);
		var expectedReminder = date.AddDays(-2).AddHours(17);
		var before = expectedReminder.AddMinutes(30);
		var after = before.AddMinutes(2);
		var calendarId = Guid.NewGuid();
		var eventId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var subjects = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var allocation = await subjects.GetAsync(AssignmentId);
			allocation.ChangeStatus(SubjectStatus.Published);
			await subjects.UpdateAsync(allocation, autoSave: true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(new AcademicCalendar(calendarId,
				TenantId, medium.Id, board.AcademicYearId, "Events calendar", date.Date.AddDays(-15), date.Date.AddDays(15)), autoSave: true);
			var days = GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
			await days.InsertAsync(new(Guid.NewGuid(), TenantId, calendarId, date.Date.AddDays(-2), DayType.WorkingDay,
				"Open school day", autoScanImpacts: false), autoSave: true);
			await days.InsertAsync(new(Guid.NewGuid(), TenantId, calendarId, date.Date.AddDays(-1), DayType.Holiday,
				"School holiday", autoScanImpacts: false), autoSave: true);
			await days.InsertAsync(new(eventId, TenantId, calendarId, date.Date, DayType.Event, "Science exhibition",
				description: "Private organizer notes", appliesToAll: false, standardId: StandardId, divisionId: DivisionId,
				startTime: TimeSpan.FromHours(10), endTime: TimeSpan.FromHours(11), autoScanImpacts: false), autoSave: true);

			var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = Key };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
			var version = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, Key, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "Event reminder", "{{event_title}} starts soon.", 1);
			version.Publish(ActorId, before.AddDays(-1));
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(version, autoSave: true);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, Key,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				["AssignedTeacher"], [CommunicationChannelType.InApp], [header.Id],
				NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config, GetRequiredService<AcademicNotificationCatalogue>().Get(Key)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		var calendar = GetRequiredService<IAcademicCalendarAppService>();
		var intents = GetRequiredService<IRepository<NotificationIntent, Guid>>();
		var sources = GetRequiredService<IEnumerable<INotificationDueSource>>().Where(source => source.ScheduledEventKeys.Contains(Key)).ToList();
		sources.ShouldHaveSingleItem();
		// Use the same transactional ABP manager as the hosted scheduler.
		var units = ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider);
		var scheduler = ActivatorUtilities.CreateInstance<NotificationDueScheduler>(ServiceProvider, units, sources);
		await ScanAsync(before);
		await WithUnitOfWorkAsync(async () => (await intents.GetListAsync(x => x.SourceId == eventId && x.EventKey == Key)).ShouldBeEmpty());
		await WithTransactionalUnitOfWorkAsync(async () => await calendar.PublishCalendarAsync(calendarId));
		await ScanAsync(after);
		var repeat = after.AddMinutes(2);
		await ScanAsync(repeat);
		var reader = GetRequiredService<NotificationIntentSourceReader>();
		NotificationIntent intent = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			intent = (await intents.GetListAsync(x => x.SourceId == eventId && x.EventKey == Key)).ShouldHaveSingleItem();
			intent.DueAt.ShouldBe(expectedReminder);
			var source = await reader.LoadAsync(intent, repeat, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			source.CurrentRecipients.Select(x => x.EmployeeId!.Value).ShouldBe([OldTeacher, OtherTeacher], ignoreOrder: true);
			source.Snapshot!.Variables["event_date"].Value.ShouldBe(date.ToString("yyyy-MM-dd"));
			DateTimeOffset.Parse(source.Snapshot.Variables["event_start"].Value!).ShouldBe(date.AddHours(10));
			source.Snapshot.Variables.Values.ShouldNotContain(x => x.Value == "Private organizer notes");
		});

		await WithTransactionalUnitOfWorkAsync(() => calendar.DeleteCalendarDayAsync(eventId));
		await WithUnitOfWorkAsync(async () =>
			(await reader.LoadAsync(intent, repeat, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty());

		async Task ScanAsync(DateTimeOffset now)
		{
			for (var i = 0; i < 25; i++)
			{
				if (!(await scheduler.ScanAsync(now, 20, CancellationToken.None)).HasMore) return;
			}
			throw new InvalidOperationException("Academic reminder scan did not finish within the test budget.");
		}
	}
}
