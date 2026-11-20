using System;
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

public sealed class AcademicCalendarEventCancellation_Tests : SubjectTeacherReassignmentTestBase
{
	private const string Cancelled = AcademicNotificationEventKeys.CalendarEventCancelled;
	private const string Changed = AcademicNotificationEventKeys.CalendarEventChanged;

	[Fact]
	public async Task Deleting_a_published_event_notifies_its_division_and_suppresses_the_pending_reschedule()
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, ActorId.ToString()), new Claim(AbpClaimTypes.Role, "admin"),
				new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
		var date = Today.AddDays(4);
		var calendarId = Guid.NewGuid();
		var notices = GetRequiredService<IRepository<NotificationIntent, Guid>>();
		var dayRepository = GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await assignments.GetAsync(AssignmentId);
			assignment.ChangeStatus(SubjectStatus.Published);
			await assignments.UpdateAsync(assignment, autoSave: true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(new AcademicCalendar(calendarId,
				TenantId, medium.Id, board.AcademicYearId, "Events calendar", date.Date.AddDays(-10), date.Date.AddDays(20)), autoSave: true);
			foreach (var key in new[] { Changed, Cancelled })
			{
				var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = key };
				await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, key,
					"AssignedTeacher", CommunicationChannelType.InApp, "en", "Event update", "{{event_title}}", 1);
				revision.Publish(ActorId, DateTimeOffset.UtcNow);
				await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
				var rule = new NotificationRule(Guid.NewGuid(), TenantId, key,
					NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
				var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
					["AssignedTeacher"], [CommunicationChannelType.InApp], [template.Id],
					NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
				rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
					config, GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
				await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
			}
		});

		var service = GetRequiredService<IAcademicCalendarAppService>();
		var input = new CreateCalendarDayDto
		{
			Date = date, DayType = DayType.Event, Title = "Science exhibition", Venue = "Main Hall",
			Description = "Private organizer notes", StandardId = StandardId, DivisionId = DivisionId
		};
		Guid draft = default, published = default;
		await WithTransactionalUnitOfWorkAsync(async () => draft = (await service.CreateCalendarDayAsync(calendarId, input)).Id);
		await WithTransactionalUnitOfWorkAsync(() => service.DeleteCalendarDayAsync(draft));
		await WithUnitOfWorkAsync(async () => (await notices.GetListAsync(x => x.SourceId == draft)).ShouldBeEmpty());

		await WithTransactionalUnitOfWorkAsync(async () => await service.PublishCalendarAsync(calendarId));
		await WithTransactionalUnitOfWorkAsync(async () => published = (await service.CreateCalendarDayAsync(calendarId, input)).Id);
		input.Date = date.AddDays(1);
		await WithTransactionalUnitOfWorkAsync(async () => await service.UpdateCalendarDayAsync(published, input));
		await WithTransactionalUnitOfWorkAsync(() => service.DeleteCalendarDayAsync(published));

		await WithUnitOfWorkAsync(async () =>
		{
			(await dayRepository.FindAsync(published)).ShouldBeNull();
			var reader = GetRequiredService<NotificationIntentSourceReader>();
			var reschedule = (await notices.GetListAsync(x => x.SourceId == published && x.EventKey == Changed)).ShouldHaveSingleItem();
			(await reader.LoadAsync(reschedule, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			var cancellation = (await notices.GetListAsync(x => x.SourceId == published && x.EventKey == Cancelled)).ShouldHaveSingleItem();
			var source = await reader.LoadAsync(cancellation, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			source.CurrentRecipients.Select(x => x.EmployeeId!.Value).ShouldBe([OldTeacher, OtherTeacher], ignoreOrder: true);
			source.Snapshot!.Variables["event_date"].Value.ShouldBe(input.Date.ToString("yyyy-MM-dd"));
			source.Snapshot.Variables.Values.ShouldNotContain(x => x.Value == input.Description);
		});
	}
}
