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

public sealed class AcademicCalendarNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	private const string EventKey = AcademicNotificationEventKeys.CalendarPublished;

	[Fact]
	public async Task Calendar_publication_queues_once_and_archiving_invalidates_the_saved_notice()
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, ActorId.ToString()), new Claim(AbpClaimTypes.Role, "admin"),
				new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
		var calendarId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await assignments.GetAsync(AssignmentId);
			assignment.ChangeStatus(SubjectStatus.Published);
			await assignments.UpdateAsync(assignment, autoSave: true);
			await AddTimetableAsync();
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var start = Today.UtcDateTime.Date.AddDays(-7);
			var end = start.AddDays(90);
			await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(new AcademicCalendar(calendarId, TenantId,
				AcademicMediumId, board.AcademicYearId, "Term calendar", start, end, "Synthetic internal planning detail"), autoSave: true);

			var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Calendar publication" };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
			var version = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, EventKey, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "View the term calendar", "Open the Academic calendar.", 1);
			version.Publish(ActorId, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(version, autoSave: true);

			var rule = new NotificationRule(Guid.NewGuid(), TenantId, EventKey,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				["AssignedTeacher"], [CommunicationChannelType.InApp], [template.Id],
				NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(configuration,
				GetRequiredService<AcademicNotificationCatalogue>().Get(EventKey)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});
		await WithUnitOfWorkAsync(async () => (await NoticesAsync()).ShouldBeEmpty());

		var calendar = GetRequiredService<IAcademicCalendarAppService>();
		for (var i = 0; i < 2; i++)
			await WithTransactionalUnitOfWorkAsync(async () =>
				(await calendar.PublishCalendarAsync(calendarId)).Status.ShouldBe(CalendarStatus.Published));

		NotificationIntent queued = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			queued = (await NoticesAsync()).ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(queued, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			source.CurrentRecipients.Select(x => x.EmployeeId!.Value).ShouldBe([OldTeacher, OtherTeacher], ignoreOrder: true);
			source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == "Synthetic internal planning detail");
		});

		await WithTransactionalUnitOfWorkAsync(async () =>
			(await calendar.ArchiveCalendarAsync(calendarId)).Status.ShouldBe(CalendarStatus.Archived));
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(queued, DateTimeOffset.UtcNow, CancellationToken.None))
				.CurrentRecipients.ShouldBeEmpty());

		Task<System.Collections.Generic.List<NotificationIntent>> NoticesAsync() =>
			GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.SourceId == calendarId && x.EventKey == EventKey);
	}
}
