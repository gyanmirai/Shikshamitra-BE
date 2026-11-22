using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

[Collection(ApiTestCollection.Name)]
public sealed class AcademicCalendarPublicationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string EventKey = AcademicNotificationEventKeys.CalendarPublished;
	private const string EventChanged = AcademicNotificationEventKeys.CalendarEventChanged;
	private const string EventCancelled = AcademicNotificationEventKeys.CalendarEventCancelled;
	private const string HolidayChanged = AcademicNotificationEventKeys.CalendarHolidayChanged;
	private const string EventReminder = AcademicNotificationEventKeys.CalendarEventReminder;
	private const string ClosureDeclared = AcademicNotificationEventKeys.CalendarEmergencyClosureDeclared;
	private const string ClosureChanged = AcademicNotificationEventKeys.CalendarEmergencyClosureChanged;
	private const string Path = "/api/academic/calendars";
	private const string PrivateDescription = "Internal bell scheduling notes";
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Teacher = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task Published_calendars_target_the_saved_division_and_archiving_invalidates_the_notice() => RunAsync();

	internal async Task RunAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var learner = Guid.NewGuid();
		var sibling = Guid.NewGuid();
		var otherClassLearner = Guid.NewGuid();
		var start = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
		Guid mediumId = default;
		Guid schoolYear = default, classId = default, divisionId = default, siblingDivision = default, teacherId = default;
		Guid otherClassId = default, otherClassDivision = default;
		await InSchoolAsync(async services =>
		{
			var requiredEnd = DateTime.SpecifyKind(start.Date.AddDays(30), DateTimeKind.Utc);
			var years = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetQueryableAsync();
			var boards = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetQueryableAsync();
			var mediums = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetQueryableAsync();
			var stages = await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetQueryableAsync();
			var hierarchy = await (from year in years
				join board in boards on year.Id equals board.AcademicYearId
				join medium in mediums on board.Id equals medium.EducationBoardId
				join stage in stages on medium.Id equals stage.EducationMediumId
				where year.EndDate >= requiredEnd
				orderby year.StartDate, board.Id, medium.Id, stage.SortOrder, stage.Id
				select new { Year = year.Id, year.StartDate, Board = board.Id, Medium = medium.Id, Stage = stage.Id }).FirstAsync();
			if (hierarchy.StartDate.Date > start.Date)
				start = new DateTimeOffset(hierarchy.StartDate.Date, TimeSpan.Zero);
			mediumId = hierarchy.Medium;
			schoolYear = hierarchy.Year;
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(hierarchy.Stage, "Calendar notice class", 12, null));
			var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 25));
			var other = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "B", 25));
			classId = standard.Id;
			divisionId = division.Id;
			siblingDivision = other.Id;
			var otherClass = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(hierarchy.Stage, "Second calendar notice class", 13, null));
			var otherSection = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(otherClass.Id, "A", 25));
			otherClassId = otherClass.Id;
			otherClassDivision = otherSection.Id;
			var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Calendar notice subject", "#24597a", mediumId));
			teacherId = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Teacher))!.Id;
			var allocation = new AssignedSubject(Guid.NewGuid(), School, standard.Id, subject.Id, "CAL", SubjectStatus.Published);
			allocation.SetPrimaryTeacher(teacherId);
			await InsertAsync(services, allocation);
			foreach (var (id, standardId, section) in new[]
				{ (learner, standard.Id, division.Id), (sibling, standard.Id, other.Id), (otherClassLearner, otherClassId, otherClassDivision) })
			{
				await InsertAsync(services, new Student(id, School, "Synthetic", null, "Learner", schoolYear,
					hierarchy.Board, mediumId, hierarchy.Stage, standardId, section, status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), School, id, schoolYear, standardId, section));
			}

			var kinds = new[] { "AssignedTeacher", "Guardian" };
			var headers = new Guid[kinds.Length];
			for (var i = 0; i < kinds.Length; i++)
			{
				var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
					{ TemplateName = "Calendar publication " + kinds[i] });
				headers[i] = header.Id;
				var version = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1, EventKey, kinds[i],
					CommunicationChannelType.InApp, "en", "Calendar publication", "{{event_title}} is available.", 1);
				version.Publish(Teacher, DateTimeOffset.UtcNow);
				await InsertAsync(services, version);
			}
			var rule = new NotificationRule(Guid.NewGuid(), School, EventKey,
				NotificationRuleScope.Create(schoolYear, hierarchy.Board, mediumId, standard.Id, division.Id));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				["AssignedTeacher"], [CommunicationChannelType.InApp], headers,
				NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config, services.GetRequiredService<AcademicNotificationCatalogue>().Get(EventKey)), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);

			var eventHeader = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
				{ TemplateName = "Calendar event moved" });
			var eventVersion = new CommunicationTemplateRevision(Guid.NewGuid(), School, eventHeader.Id, 1, EventChanged,
				"Guardian", CommunicationChannelType.InApp, "en", "Event changed", "{{event_title}} changed.", 1);
			eventVersion.Publish(Teacher, DateTimeOffset.UtcNow);
			await InsertAsync(services, eventVersion);
			var eventRule = new NotificationRule(Guid.NewGuid(), School, EventChanged,
				NotificationRuleScope.Create(schoolYear, hierarchy.Board, mediumId, standard.Id));
			eventRule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config with { StaffAudienceSelectors = [], TemplateIds = [eventHeader.Id] },
				services.GetRequiredService<AcademicNotificationCatalogue>().Get(EventChanged)), eventRule.ConcurrencyStamp);
			await InsertAsync(services, eventRule);

			var reminderHeader = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
				{ TemplateName = "Calendar event reminder" });
			var reminderVersion = new CommunicationTemplateRevision(Guid.NewGuid(), School, reminderHeader.Id, 1,
				EventReminder, "Guardian", CommunicationChannelType.InApp, "en", "Event reminder", "{{event_title}} starts soon.", 1);
			reminderVersion.Publish(Teacher, DateTimeOffset.UtcNow);
			await InsertAsync(services, reminderVersion);
			var reminderRule = new NotificationRule(Guid.NewGuid(), School, EventReminder,
				NotificationRuleScope.Create(schoolYear, hierarchy.Board, mediumId, standard.Id, division.Id));
			reminderRule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config with { StaffAudienceSelectors = [], TemplateIds = [reminderHeader.Id] },
				services.GetRequiredService<AcademicNotificationCatalogue>().Get(EventReminder)), reminderRule.ConcurrencyStamp);
			await InsertAsync(services, reminderRule);

			var cancellationHeader = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
				{ TemplateName = "Calendar event cancelled" });
			var cancellationVersion = new CommunicationTemplateRevision(Guid.NewGuid(), School, cancellationHeader.Id, 1,
				EventCancelled, "Guardian", CommunicationChannelType.InApp, "en", "Event cancelled", "{{event_title}} was cancelled.", 1);
			cancellationVersion.Publish(Teacher, DateTimeOffset.UtcNow);
			await InsertAsync(services, cancellationVersion);
			var cancellationRule = new NotificationRule(Guid.NewGuid(), School, EventCancelled,
				NotificationRuleScope.Create(schoolYear, hierarchy.Board, mediumId, standard.Id));
			cancellationRule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config with { StaffAudienceSelectors = [], TemplateIds = [cancellationHeader.Id] },
				services.GetRequiredService<AcademicNotificationCatalogue>().Get(EventCancelled)), cancellationRule.ConcurrencyStamp);
			await InsertAsync(services, cancellationRule);

			var holidayHeader = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
				{ TemplateName = "Holiday schedule changed" });
			var holidayVersion = new CommunicationTemplateRevision(Guid.NewGuid(), School, holidayHeader.Id, 1, HolidayChanged,
				"Guardian", CommunicationChannelType.InApp, "en", "Holiday schedule changed", "{{change_summary}}", 1);
			holidayVersion.Publish(Teacher, DateTimeOffset.UtcNow);
			await InsertAsync(services, holidayVersion);
			foreach (var (classScope, sectionScope) in new[] { (classId, divisionId), (otherClassId, otherClassDivision) })
			{
				var holidayRule = new NotificationRule(Guid.NewGuid(), School, HolidayChanged,
					NotificationRuleScope.Create(schoolYear, hierarchy.Board, mediumId, classScope, sectionScope));
				holidayRule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
					config with { StaffAudienceSelectors = [], TemplateIds = [holidayHeader.Id] },
					services.GetRequiredService<AcademicNotificationCatalogue>().Get(HolidayChanged)), holidayRule.ConcurrencyStamp);
				await InsertAsync(services, holidayRule);
			}

			foreach (var key in new[] { ClosureDeclared, ClosureChanged })
			{
				var closureTemplates = new System.Collections.Generic.List<Guid>();
				foreach (var kind in new[] { "AssignedTeacher", "Guardian", "StudentAccount" })
				{
					var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
						{ TemplateName = key + " " + kind });
					var version = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1, key,
						kind, CommunicationChannelType.InApp, "en", "School closure", "{{change_summary}}", 1);
					version.Publish(Teacher, DateTimeOffset.UtcNow);
					await InsertAsync(services, version);
					closureTemplates.Add(header.Id);
				}
				foreach (var (classScope, sectionScope) in new[] { (classId, divisionId), (otherClassId, otherClassDivision) })
				{
					var closureRule = new NotificationRule(Guid.NewGuid(), School, key,
						NotificationRuleScope.Create(schoolYear, hierarchy.Board, mediumId, classScope, sectionScope));
					closureRule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
						config with { IncludeStudentAccount = true, TemplateIds = closureTemplates },
						services.GetRequiredService<AcademicNotificationCatalogue>().Get(key)), closureRule.ConcurrencyStamp);
					await InsertAsync(services, closureRule);
				}
			}
		});

		using var created = await Client.PostAsJsonAsync(Path, new CreateCalendarDto
		{
			AcademicMediumId = mediumId, AcademicYearId = schoolYear, Name = "Synthetic term calendar",
			StartDate = start, EndDate = start.AddDays(30), Description = PrivateDescription,
		}, JsonOptions);
		created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		var draft = (await created.Content.ReadFromJsonAsync<AcademicCalendarDto>(JsonOptions))!;
		draft.Status.ShouldBe(CalendarStatus.Draft);
		var calendarId = draft.Id;
		await InSchoolAsync(async services => (await ReadNoticesAsync(services)).ShouldBeEmpty());

		for (var i = 0; i < 2; i++)
		{
			using var published = await Client.PostAsync($"{Path}/{calendarId}/publish", null);
			published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
		}
		NotificationIntent notice = null!;
		await InSchoolAsync(async services =>
		{
			notice = (await ReadNoticesAsync(services)).ShouldHaveSingleItem();
			var current = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(notice, DateTimeOffset.UtcNow, CancellationToken.None);
			current.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
			current.Runtime.Configuration.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
			current.Runtime.Configuration.IncludeStudentAccount.ShouldBeFalse();
			current.CurrentRecipients.ShouldBe([new("AssignedTeacher", UserId: Teacher, EmployeeId: teacherId),
				new("Student", StudentId: learner)], ignoreOrder: true);
			current.Snapshot!.Variables["event_title"].Value.ShouldBe("Synthetic term calendar");
			current.Snapshot.Variables.Values.ShouldNotContain(x => x.Value == PrivateDescription);
		});

		var operation = $"/api/academic/notifications/operations/{notice.CampaignId}";
		using var withoutCalendarView = Factory.CreateClient();
		withoutCalendarView.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await withoutCalendarView.GetAsync(operation);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		using var allowed = await Client.GetAsync(operation);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());

		var marker = new CreateCalendarDayDto
		{
			Date = start.AddDays(5), DayType = DayType.Event, Title = "Science exhibition", Venue = "Original hall",
			StandardId = classId, DivisionId = divisionId, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11)
		};
		using var added = await Client.PostAsJsonAsync($"{Path}/{calendarId}/days", marker, JsonOptions);
		added.StatusCode.ShouldBe(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
		var day = (await added.Content.ReadFromJsonAsync<AcademicCalendarDayDto>(JsonOptions))!;
		var reminderClock = new DateTimeOffset(start.AddDays(4).Date.AddHours(18), TimeSpan.FromMinutes(330));
		using (var workers = Factory.Services.CreateScope())
		{
			var services = workers.ServiceProvider;
			using var school = services.GetRequiredService<ICurrentTenant>().Change(School, "Synthetic school");
			var sources = services.GetServices<INotificationDueSource>().Where(x => x.ScheduledEventKeys.Contains(EventReminder)).ToList();
			sources.ShouldHaveSingleItem();
			var scheduler = ActivatorUtilities.CreateInstance<NotificationDueScheduler>(services, sources);
			(await scheduler.ScanAsync(reminderClock, 100, CancellationToken.None)).Created.ShouldBe(1);
			(await scheduler.ScanAsync(reminderClock.AddMinutes(2), 100, CancellationToken.None)).Created.ShouldBe(0);
		}
		NotificationIntent reminder = null!;
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			reminder = (await repository.GetListAsync(x => x.SourceId == day.Id && x.EventKey == EventReminder)).ShouldHaveSingleItem();
			reminder.DueAt.ShouldBe(reminderClock.AddHours(-1));
			var snapshot = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(reminder, reminderClock, CancellationToken.None);
			snapshot.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
			snapshot.CurrentRecipients.ShouldBe([new("Student", StudentId: learner)]);
		});
		marker.DivisionId = siblingDivision;
		marker.Date = start.AddDays(6);
		marker.Venue = "New hall";
		using var moved = await Client.PutAsJsonAsync($"/api/academic/calendar-days/{day.Id}", marker, JsonOptions);
		moved.StatusCode.ShouldBe(HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());
		await InSchoolAsync(async services =>
		{
			var intents = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.TenantId == School && x.SourceId == day.Id && x.EventKey == EventChanged);
			intents.Count.ShouldBe(2);
			intents.Select(x => x.CampaignId).Distinct().ShouldHaveSingleItem();
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(reminder, reminderClock, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			foreach (var intent in intents)
			{
				var current = await reader.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
				if (current.Runtime.SourceScope.DivisionId == divisionId)
				{
					current.CurrentRecipients.ShouldBe([new("Student", StudentId: learner)]);
					current.Snapshot!.RecipientContent!.Single(x => x.Recipient.StudentId == learner)
						.Variables["location"].Value.ShouldBe("Original hall");
				}
				else
				{
					current.Runtime.SourceScope.DivisionId.ShouldBe(siblingDivision);
					current.CurrentRecipients.ShouldBe([new("Student", StudentId: sibling)]);
					current.Snapshot!.Variables["location"].Value.ShouldBe("New hall");
				}
			}
		});

		using var deleted = await Client.DeleteAsync($"/api/academic/calendar-days/{day.Id}");
		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
		Guid? cancellationOperation = null;
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			var priorChanges = await repository.GetListAsync(x => x.SourceId == day.Id && x.EventKey == EventChanged);
			priorChanges.Count.ShouldBe(2);
			foreach (var prior in priorChanges)
				(await reader.LoadAsync(prior, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			var cancellation = (await repository.GetListAsync(x => x.SourceId == day.Id && x.EventKey == EventCancelled)).ShouldHaveSingleItem();
			var source = await reader.LoadAsync(cancellation, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.ShouldBe([new("Student", StudentId: sibling)]);
			source.Runtime.SourceScope.DivisionId.ShouldBe(siblingDivision);
			source.Snapshot!.Variables["event_title"].Value.ShouldBe("Science exhibition");
			cancellationOperation = cancellation.CampaignId;
		});
		using var deniedCancellation = await withoutCalendarView.GetAsync($"/api/academic/notifications/operations/{cancellationOperation}");
		deniedCancellation.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await deniedCancellation.Content.ReadAsStringAsync());
		using var allowedCancellation = await Client.GetAsync($"/api/academic/notifications/operations/{cancellationOperation}");
		allowedCancellation.StatusCode.ShouldBe(HttpStatusCode.OK, await allowedCancellation.Content.ReadAsStringAsync());

		var closurePath = "/api/academic/calendar-days";
		using var declared = await Client.PostAsJsonAsync($"{closurePath}/emergency-closure", new CreateEmergencyClosureDto
		{
			AcademicMediumId = mediumId, Date = start.AddDays(9), EndDate = start.AddDays(10),
			Title = "Severe weather", Description = PrivateDescription, StandardIds = [classId],
			NotifyParents = true, AutoScanImpacts = false
		}, JsonOptions);
		declared.StatusCode.ShouldBe(HttpStatusCode.OK, await declared.Content.ReadAsStringAsync());
		var closures = (await declared.Content.ReadFromJsonAsync<AcademicCalendarDayDto[]>(JsonOptions))!;
		closures.Length.ShouldBe(2);
		NotificationIntent closureNotice = null!;
		NotificationIntent nextDayNotice = null!;
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			closureNotice = (await repository.GetListAsync(x => x.SourceId == closures[0].Id && x.EventKey == ClosureDeclared)).ShouldHaveSingleItem();
			nextDayNotice = (await repository.GetListAsync(x => x.SourceId == closures[1].Id && x.EventKey == ClosureDeclared)).ShouldHaveSingleItem();
			var source = await services.GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(closureNotice, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.ShouldBe([new("Student", StudentId: learner)]);
			source.Runtime.Configuration.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
			source.Runtime.Configuration.IncludeStudentAccount.ShouldBeFalse();
			source.Runtime.Configuration.StaffAudienceSelectors.ShouldBeEmpty();
			source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == PrivateDescription);
		});
		using var deniedClosure = await withoutCalendarView.GetAsync($"/api/academic/notifications/operations/{closureNotice.CampaignId}");
		deniedClosure.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await deniedClosure.Content.ReadAsStringAsync());
		using var allowedClosure = await Client.GetAsync($"/api/academic/notifications/operations/{closureNotice.CampaignId}");
		allowedClosure.StatusCode.ShouldBe(HttpStatusCode.OK, await allowedClosure.Content.ReadAsStringAsync());

		for (var i = 0; i < 2; i++)
		{
			using var reopened = await Client.PostAsync($"{closurePath}/{closures[0].Id}/cancel-emergency-closure", null);
			reopened.EnsureSuccessStatusCode();
		}
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(closureNotice, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			(await reader.LoadAsync(nextDayNotice, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients
				.ShouldBe([new("Student", StudentId: learner)]);
			var changed = (await repository.GetListAsync(x => x.SourceId == closures[0].Id && x.EventKey == ClosureChanged)).ShouldHaveSingleItem();
			var source = await reader.LoadAsync(changed, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.ShouldBe([new("Student", StudentId: learner)]);
			source.Snapshot!.Variables["change_summary"].Value.ShouldBe("The emergency closure has been lifted.");
		});

		using var audienceEdit = await Client.PutAsJsonAsync($"{closurePath}/{closures[1].Id}/emergency-closure", new UpdateEmergencyClosureDto
		{
			StartDate = start.AddDays(10), Title = "Severe weather", StandardIds = [classId],
			NotifyStudents = true, AutoScanImpacts = false
		}, JsonOptions);
		audienceEdit.StatusCode.ShouldBe(HttpStatusCode.OK, await audienceEdit.Content.ReadAsStringAsync());
		var closureRows = await Client.GetFromJsonAsync<PagedResultDto<EmergencyClosureListItemDto>>(
			$"/api/academic/calendar-emergency-closures?academicMediumId={mediumId}", JsonOptions);
		var savedSelection = closureRows!.Items.Single(x => x.Id == closures[1].Id);
		savedSelection.NotifyTeachers.ShouldBeFalse();
		savedSelection.NotifyParents.ShouldBeFalse();
		savedSelection.NotifyStudents.ShouldBeTrue();
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(nextDayNotice, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			(await repository.GetListAsync(x => x.SourceId == closures[1].Id && x.EventKey == ClosureChanged)).ShouldBeEmpty();
		});

		using var movedClosure = await Client.PutAsJsonAsync($"{closurePath}/{closures[1].Id}/emergency-closure", new UpdateEmergencyClosureDto
		{
			StartDate = start.AddDays(11), Title = "Revised severe weather closure", StandardIds = [otherClassId],
			NotifyParents = true, AutoScanImpacts = false
		}, JsonOptions);
		movedClosure.StatusCode.ShouldBe(HttpStatusCode.OK, await movedClosure.Content.ReadAsStringAsync());
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(nextDayNotice, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			var changes = await repository.GetListAsync(x => x.SourceId == closures[1].Id && x.EventKey == ClosureChanged);
			changes.Count.ShouldBe(2);
			changes.Select(x => x.CampaignId).Distinct().ShouldHaveSingleItem();
			foreach (var changed in changes)
			{
				var source = await reader.LoadAsync(changed, DateTimeOffset.UtcNow, CancellationToken.None);
				if (source.Runtime.SourceScope.DivisionId == divisionId)
				{
					source.CurrentRecipients.ShouldBe([new("Student", StudentId: learner)]);
					var previous = source.Snapshot!.RecipientContent!.Single(x => x.Recipient.StudentId == learner);
					previous.Variables["event_title"].Value.ShouldBe("Severe weather");
					previous.Variables["event_date"].Value.ShouldBe(start.AddDays(10).ToString("yyyy-MM-dd"));
				}
				else
				{
					source.Runtime.SourceScope.DivisionId.ShouldBe(otherClassDivision);
					source.CurrentRecipients.ShouldBe([new("Student", StudentId: otherClassLearner)]);
					source.Snapshot!.Variables["event_title"].Value.ShouldBe("Revised severe weather closure");
					source.Snapshot.Variables["event_date"].Value.ShouldBe(start.AddDays(11).ToString("yyyy-MM-dd"));
				}
			}
		});

		var vacation = new CreateCalendarDayDto
		{
			Date = start.AddDays(14), EndDate = start.AddDays(16), DayType = DayType.Vacation,
			Title = "Term break", StandardId = classId, DivisionId = divisionId
		};
		using var addedVacation = await Client.PostAsJsonAsync($"{Path}/{calendarId}/days", vacation, JsonOptions);
		addedVacation.StatusCode.ShouldBe(HttpStatusCode.OK, await addedVacation.Content.ReadAsStringAsync());
		var priorVacation = (await addedVacation.Content.ReadFromJsonAsync<AcademicCalendarDayDto>(JsonOptions))!;
		vacation.Date = start.AddDays(15);
		vacation.EndDate = start.AddDays(17);
		vacation.StandardId = otherClassId;
		vacation.DivisionId = otherClassDivision;
		using var vacationUpdate = await Client.PutAsJsonAsync($"{closurePath}/{priorVacation.Id}", vacation, JsonOptions);
		vacationUpdate.StatusCode.ShouldBe(HttpStatusCode.OK, await vacationUpdate.Content.ReadAsStringAsync());
		var revisedVacation = (await vacationUpdate.Content.ReadFromJsonAsync<AcademicCalendarDayDto>(JsonOptions))!;
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			var changes = await repository.GetListAsync(x => x.SourceId == revisedVacation.Id && x.EventKey == HolidayChanged);
			changes.Count.ShouldBe(2);
			changes.Select(x => x.CampaignId).Distinct().ShouldHaveSingleItem();
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			foreach (var changed in changes)
			{
				var source = await reader.LoadAsync(changed, DateTimeOffset.UtcNow, CancellationToken.None);
				var variables = source.Snapshot!.Variables;
				if (source.Runtime.SourceScope.DivisionId == divisionId)
				{
					source.CurrentRecipients.ShouldBe([new("Student", StudentId: learner)]);
					variables["event_date"].Value.ShouldBe(start.AddDays(14).ToString("yyyy-MM-dd"));
					variables["holiday_end_date"].Value.ShouldBe(start.AddDays(16).ToString("yyyy-MM-dd"));
				}
				else
				{
					source.Runtime.SourceScope.DivisionId.ShouldBe(otherClassDivision);
					source.CurrentRecipients.ShouldBe([new("Student", StudentId: otherClassLearner)]);
					variables["event_date"].Value.ShouldBe(start.AddDays(15).ToString("yyyy-MM-dd"));
					variables["holiday_end_date"].Value.ShouldBe(start.AddDays(17).ToString("yyyy-MM-dd"));
				}
			}
		});

		using var archived = await Client.PostAsync($"{Path}/{calendarId}/archive", null);
		archived.StatusCode.ShouldBe(HttpStatusCode.OK, await archived.Content.ReadAsStringAsync());
		await InSchoolAsync(async services =>
			(await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(notice, DateTimeOffset.UtcNow, CancellationToken.None))
				.CurrentRecipients.ShouldBeEmpty());

		Task<System.Collections.Generic.List<NotificationIntent>> ReadNoticesAsync(IServiceProvider services) =>
			services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.TenantId == School && x.EventKey == EventKey && x.SourceId == calendarId);
	}

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(School, "Synthetic school");
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}

	private static Task<TEntity> InsertAsync<TEntity>(IServiceProvider services, TEntity row) where TEntity : class, IEntity<Guid> =>
		services.GetRequiredService<IRepository<TEntity, Guid>>().InsertAsync(row, autoSave: true);
}

[Collection(ApiTestCollection.Name)]
public sealed class AcademicCalendarPublicationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Calendar_publication_scope_and_history_work_on_Postgres()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new AcademicCalendarPublicationDelivery_Tests(fixture).RunAsync();
	}
}
