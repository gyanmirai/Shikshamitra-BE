using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.PermissionManagement;
using Xunit;
using TimetableRow = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Notifications;

public sealed class AcademicSettingsImpactNotification_Tests : SubjectTeacherReassignmentTestBase
{
	private const string Event = AcademicNotificationEventKeys.SettingsOperationalImpact;

	[Fact]
	public async Task Weekly_lecture_edits_notify_responsible_staff_once_when_a_published_timetable_stops_matching()
	{
		using var principal = AsAdmin();
		using var school = Tenant.Change(TenantId, "Test school");
		var weeklyId = Guid.NewGuid();
		var timetableId = Guid.NewGuid();
		var weekly = GetRequiredService<IWeeklyLectureSlotAppService>();
		var intents = GetRequiredService<IRepository<NotificationIntent, Guid>>();
		var sourceReader = GetRequiredService<NotificationIntentSourceReader>();
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var allocation = await GetRequiredService<IRepository<AssignedSubject, Guid>>().GetAsync(AssignmentId);
			allocation.Status = SubjectStatus.Published;
			await GetRequiredService<IRepository<AssignedSubject, Guid>>().UpdateAsync(allocation, autoSave: true);
			var record = new WeeklyLectureSlot(weeklyId, TenantId, StandardId);
			record.AddSubject(Guid.NewGuid(), SubjectId, null, 1);
			await GetRequiredService<IWeeklyLectureSlotRepository>().InsertAsync(record, autoSave: true);

			var current = new TimetableRow(timetableId, TenantId, StandardId, DivisionId);
			var period = new TimetableSlot(Guid.NewGuid(), current.Id, TimetableDayOfWeek.Monday, 1,
				new TimeOnly(9, 0), new TimeOnly(9, 30), SlotType.Period);
			period.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), period.Id, SubjectId, OldTeacher, null));
			current.AddSlot(period);
			current.Publish();
			await Timetables.InsertAsync(current, autoSave: true);
			var teacher = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(OldTeacher);
			var placement = (await GetRequiredService<TimetableNotificationSource>().FindScopeAsync(TenantId, timetableId, default)).ShouldNotBeNull();
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
			await GetRequiredService<IPermissionManager>().SetAsync(
				AcademicSettingsImpactNotificationSource.View,
				"U", teacher.UserId!.Value.ToString(), true);
			await GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(),
				TenantId, teacher.UserId.Value, academicYearId: placement.YearId, educationBoardId: placement.BoardId,
				educationMediumId: placement.MediumId, educationMediumStageId: standard.EducationMediumStageId,
				standardId: StandardId, divisionId: DivisionId), autoSave: true);

			var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Weekly plan impact" };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1,
				Event, "AssignedTeacher", CommunicationChannelType.InApp, "en", "Timetable review", "{{impact_summary}}", 1);
			revision.Publish(ActorId, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, Event, placement.RuleScope);
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], [template.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(config,
				GetRequiredService<AcademicNotificationCatalogue>().Get(Event)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		await Update(1);
		await WithUnitOfWorkAsync(async () => (await intents.GetListAsync(x => x.EventKey == Event)).ShouldBeEmpty());
		await Update(2);
		await Update(2);
		await WithUnitOfWorkAsync(async () =>
		{
			var plan = await GetRequiredService<IWeeklyLectureSlotRepository>().GetAsync(weeklyId);
			plan.Subjects.ShouldHaveSingleItem().WeeklyLectures.ShouldBe(2);
			var impact = SettingsTimetableImpact.Read(plan).ShouldHaveSingleItem();
			impact.TimetableId.ShouldBe(timetableId);
			var incident = (await intents.GetListAsync(x => x.EventKey == Event)).ShouldHaveSingleItem();
			incident.SourceId.ShouldBe(timetableId);
			var live = await sourceReader.LoadAsync(incident, DateTimeOffset.UtcNow, default);
			live.CurrentRecipients.ShouldHaveSingleItem().EmployeeId.ShouldBe(OldTeacher);
			live.Snapshot!.Variables["setting_name"].Value.ShouldBe("Weekly lecture requirements");
			var definition = GetRequiredService<AcademicNotificationCatalogue>().Get(Event);
			definition.SupportedChannels.ShouldBe([CommunicationChannelType.InApp, CommunicationChannelType.WebPush], ignoreOrder: true);
			(await Timetables.GetAsync(timetableId)).Status.ShouldBe(TimetableStatus.Published);
		});

		await Update(1);
		await WithUnitOfWorkAsync(async () =>
		{
			var incident = (await intents.GetListAsync(x => x.EventKey == Event)).ShouldHaveSingleItem();
			(await sourceReader.LoadAsync(incident, DateTimeOffset.UtcNow, default)).CurrentRecipients.ShouldBeEmpty();
		});
		await Update(2);
		await WithUnitOfWorkAsync(async () => (await intents.GetListAsync(x => x.EventKey == Event)).Count.ShouldBe(2));

		Task Update(int lectures) => WithTransactionalUnitOfWorkAsync(async () =>
		{
			await weekly.UpdateAsync(weeklyId, new() { Subjects = [new() { SubjectId = SubjectId, WeeklyLectures = lectures }] });
		});
	}
}
