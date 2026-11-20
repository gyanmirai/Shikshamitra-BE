using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public sealed class AcademicCalendarDayNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	private const string EventKey = AcademicNotificationEventKeys.CalendarEventChanged;

	[Fact]
	public async Task A_published_event_date_change_queues_for_the_saved_division_only()
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, ActorId.ToString()), new Claim(AbpClaimTypes.Role, "admin"),
				new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
		var calendarId = Guid.NewGuid();
		var initial = Today.AddDays(4);
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await assignments.GetAsync(AssignmentId);
			assignment.ChangeStatus(SubjectStatus.Published);
			await assignments.UpdateAsync(assignment, autoSave: true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(new AcademicCalendar(calendarId, TenantId,
				AcademicMediumId, board.AcademicYearId, "Events calendar", initial.Date.AddDays(-10), initial.Date.AddDays(30)), autoSave: true);
			var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Calendar event changed" };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
			var version = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, EventKey, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "Calendar event changed", "Check the revised date.", 1);
			version.Publish(ActorId, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(version, autoSave: true);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, EventKey,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				["AssignedTeacher"], [CommunicationChannelType.InApp], [header.Id],
				NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config, GetRequiredService<AcademicNotificationCatalogue>().Get(EventKey)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		var service = GetRequiredService<IAcademicCalendarAppService>();
		var input = new CreateCalendarDayDto
		{
			Date = initial, DayType = DayType.Event, Title = "Science exhibition", Venue = "Main hall",
			StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(12), StandardId = StandardId, DivisionId = DivisionId
		};
		var dayId = Guid.Empty;
		await WithTransactionalUnitOfWorkAsync(async () => dayId = (await service.CreateCalendarDayAsync(calendarId, input)).Id);
		input.Date = initial.AddDays(1);
		await SaveAsync();
		await WithUnitOfWorkAsync(async () => (await NoticesAsync()).ShouldBeEmpty());
		await WithTransactionalUnitOfWorkAsync(async () =>
			(await service.PublishCalendarAsync(calendarId)).Status.ShouldBe(CalendarStatus.Published));

		input.Date = initial.AddDays(2);
		await SaveAsync();
		input.Description = "Private organizer notes";
		await SaveAsync();

		await WithUnitOfWorkAsync(async () =>
		{
			var notice = (await NoticesAsync()).ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(notice, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			source.CurrentRecipients.Select(x => x.EmployeeId!.Value).ShouldBe([OldTeacher, OtherTeacher], ignoreOrder: true);
			source.Snapshot!.Variables["event_date"].Value.ShouldBe(input.Date.ToString("yyyy-MM-dd"));
			source.Snapshot.Variables.Values.ShouldNotContain(x => x.Value == input.Description);
		});

		Task SaveAsync() => WithTransactionalUnitOfWorkAsync(async () =>
			(await service.UpdateCalendarDayAsync(dayId, input)).Date.Date.ShouldBe(input.Date.Date));
		Task<List<NotificationIntent>> NoticesAsync() =>
			GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.SourceId == dayId && x.EventKey == EventKey);
	}
}
