using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

[Collection(ApiTestCollection.Name)]
public sealed class TimetableNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private const string Published = AcademicNotificationEventKeys.TimetablePublished;
	private const string Changed = AcademicNotificationEventKeys.TimetableChanged;
	private const string Withdrawn = AcademicNotificationEventKeys.TimetableWithdrawn;
	private const string DayReminder = AcademicNotificationEventKeys.TimetableDayReminder;

	[Fact]
	public async Task Timetable_transitions_notify_only_affected_locked_elective_cohorts_and_teachers()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var classId = Guid.Empty;
		var divisionId = Guid.Empty;
		var groupId = Guid.NewGuid();
		var subjectA = Guid.NewGuid();
		var subjectB = Guid.NewGuid();
		var learnerA = Guid.NewGuid();
		var learnerB = Guid.NewGuid();
		var enrollmentA = Guid.NewGuid();
		Employee[] teachers = [];
		await InSchoolAsync(async services =>
		{
			var year = (await InsertAsync(services, await services.GetRequiredService<AcademicYearManager>().CreateAsync(
				$"Timetable {Guid.NewGuid():N}"[..32], DateTime.UtcNow.Date.AddDays(-5), DateTime.UtcNow.Date.AddMonths(4)))).Id;
			var board = (await InsertAsync(services, await services.GetRequiredService<EducationBoardManager>()
				.CreateAsync(year, EducationBoardType.CBSE))).Id;
			var medium = (await InsertAsync(services, await services.GetRequiredService<EducationMediumManager>()
				.CreateAsync(board, "Timetable test medium"))).Id;
			var stage = (await InsertAsync(services, await services.GetRequiredService<EducationMediumStageManager>()
				.CreateAsync(medium, "Timetable test stage"))).Id;
			var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage,
				$"Schedule {Guid.NewGuid():N}"[..20], 12, null);
			classId = (await InsertAsync(services, standard)).Id;
			var division = await services.GetRequiredService<DivisionManager>().CreateAsync(classId, "A", 30);
			divisionId = (await InsertAsync(services, division)).Id;
			var teacher = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Guid.Parse(TestAuthHandler.TestUserId)))!;
			teachers = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.RoleId == teacher.RoleId && x.UserId.HasValue && x.EmployeeStatusType == EmployeeStatusType.Active))
				.OrderBy(x => x.Id).DistinctBy(x => x.UserId).Take(3).ToArray();
			teachers.Length.ShouldBe(3);
			await InsertAsync(services, new Subject(subjectA, TenantId, "Timetable art", "#24597a", medium));
			await InsertAsync(services, new Subject(subjectB, TenantId, "Timetable music", "#24597a", medium));
			var group = new ElectiveGroup(groupId, TenantId, "Timetable electives", "#24597a", medium);
			group.AddSubject(subjectA);
			group.AddSubject(subjectB);
			await InsertAsync(services, group);
			foreach (var subject in new[] { subjectA, subjectB })
				await InsertAsync(services, new AssignedSubject(Guid.NewGuid(), TenantId, classId, subject,
					$"TT{subject:N}"[..10], SubjectStatus.Published, subjectType: SubjectType.Elective, electiveGroupId: groupId));
			var config = new LectureSlotConfig(Guid.NewGuid(), TenantId, classId, "08:00 - 17:00");
			config.AddSlotGroup(new(Guid.NewGuid(), config.Id, 1, 0, 5));
			await InsertAsync(services, config);
			var weekly = new WeeklyLectureSlot(Guid.NewGuid(), TenantId, classId);
			weekly.AddSubject(Guid.NewGuid(), null, groupId, 1);
			await InsertAsync(services, weekly);
			foreach (var (learner, enrollment, subject) in new[] { (learnerA, enrollmentA, subjectA),
				(learnerB, Guid.NewGuid(), subjectB), (Guid.NewGuid(), Guid.NewGuid(), Guid.Empty) })
			{
				await InsertAsync(services, new Student(learner, TenantId, "Sample", null, "Learner", year, board,
					medium, stage, classId, divisionId, status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(services, StudentEnrollment.Create(enrollment, TenantId, learner, year, classId, divisionId));
				if (subject == Guid.Empty) continue;
				var choice = new StudentElectiveSelection(Guid.NewGuid(), TenantId, learner, groupId, subject, medium);
				choice.LockSelection();
				await InsertAsync(services, choice);
			}

			var scope = NotificationRuleScope.Create(year, board, medium, classId, divisionId);
			foreach (var key in new[] { Published, Changed, Withdrawn, DayReminder })
			{
				var templateIds = new List<Guid>();
				var audiences = key == DayReminder ? new[] { "AssignedTeacher", "StudentAccount" } : ["AssignedTeacher", "Guardian"];
				foreach (var audience in audiences)
				{
					var templateId = Guid.NewGuid();
					await InsertAsync(services, new CommunicationTemplate(templateId, TenantId) { TemplateName = key + "." + audience });
					var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, templateId, 1, key, audience,
						CommunicationChannelType.InApp, "en", "Timetable update", "Open the school inbox.", 1);
					revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
					await InsertAsync(services, revision);
					templateIds.Add(templateId);
				}
				var rule = new NotificationRule(Guid.NewGuid(), TenantId, key, scope);
				var ruleConfig = new NotificationRuleRuntimeConfiguration(1,
					NotificationGuardianMode.PrimaryOnly, [], key == DayReminder, ["AssignedTeacher"],
					[CommunicationChannelType.InApp], templateIds, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
				rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(ruleConfig,
					services.GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
				await InsertAsync(services, rule);
			}
		});

		var slot = new TimetableSlotSaveDto { Sequence = 1, StartTime = new(15, 11), EndTime = new(15, 31), Type = SlotType.Period,
			SubjectType = SubjectType.Elective, ElectiveGroupId = groupId, Subjects =
			[
				new() { SubjectId = subjectA, TeacherId = teachers[0].Id },
				new() { SubjectId = subjectB, TeacherId = teachers[1].Id }
			] };
		var input = new SaveTimetableDto { Status = TimetableStatus.Draft, Timetable = { [TimetableDayOfWeek.Tuesday] = [slot] } };
		await SaveAsync();
		var timetableId = Guid.Empty;
		await InSchoolAsync(async services =>
		{
			timetableId = (await services.GetRequiredService<ITimetableRepository>().FindByStandardAndDivisionAsync(classId, divisionId))!.Id;
			(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == timetableId)).ShouldBeEmpty();
		});

		input.Status = TimetableStatus.Published;
		await SaveAsync();
		slot.Subjects.Reverse();
		await SaveAsync();
		var initial = await AssertAudienceAsync(Published, [new("Student", learnerA), new("Student", learnerB), Teacher(0), Teacher(1)]);
		var today = DateTime.UtcNow.Date;
		var monday = today.AddDays((8 - (int)today.DayOfWeek) % 7 + 7);
		var reminderAt = new DateTimeOffset(monday.AddHours(11).AddMinutes(30), TimeSpan.Zero);
		var firstPeriod = new DateTimeOffset(monday.AddDays(1).AddHours(9).AddMinutes(41), TimeSpan.Zero);
		await InSchoolAsync(async services =>
		{
			services.GetRequiredService<AcademicNotificationCatalogue>().GetAvailability(DayReminder).IsAvailable.ShouldBeTrue();
			services.GetRequiredService<AcademicNotificationCatalogue>().GetManualAvailability(DayReminder).IsAvailable.ShouldBeFalse();
			var scanner = services.GetServices<INotificationDueSource>().Single(x => x.ScheduledEventKeys.Contains(DayReminder));
			var page = await scanner.ReadPageAsync(new(TenantId, reminderAt, reminderAt, null, 100), CancellationToken.None);
			var candidate = page.Candidates.Single(x => x.SourceId == timetableId);
			candidate.OccurrenceAt.ShouldBe(firstPeriod);
			candidate.ExpiresAt.ShouldBe(firstPeriod);
			var source = await services.GetRequiredService<TimetableNotificationSource>().LoadAsync(new(Guid.Empty, TenantId,
				candidate.EventKey, scanner.SourceType, candidate.SourceId, candidate.SourceRevision, reminderAt, [], candidate.Scope,
				candidate.OccurrenceAt), CancellationToken.None);
			source.IsEligible.ShouldBeTrue();
			source.RecipientContent!.Select(x => x.Recipient).ToHashSet().SetEquals(
				[new("Student", learnerA), new("Student", learnerB), Teacher(0), Teacher(1)]).ShouldBeTrue();
			source.RecipientContent!.All(x => x.Variables["period_count"].Value == "1").ShouldBeTrue();
			source.RecipientContent!.All(x => x.Variables["period_times"].Value == "15:11–15:31").ShouldBeTrue();
		});
		await ScanAtAsync(reminderAt);
		await ScanAtAsync(reminderAt.AddMinutes(2));
		var reminder = await AssertAudienceAsync(DayReminder, [new("Student", learnerA), new("Student", learnerB), Teacher(0), Teacher(1)]);
		reminder.DueAt.ShouldBe(reminderAt);
		reminder.ExpiresAt.ShouldBe(firstPeriod);
		await InSchoolAsync(async services =>
		{
			var timetableSource = services.GetRequiredService<TimetableNotificationSource>();
			var scope = (await timetableSource.FindScopeAsync(TenantId, timetableId, CancellationToken.None))!;
			var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, scope.MediumId, scope.YearId, "Reminder holiday",
				monday, monday.AddDays(5));
			calendar.Publish(Guid.Parse(TestAuthHandler.TestUserId));
			await InsertAsync(services, calendar);
			await InsertAsync(services, new AcademicCalendarDay(Guid.NewGuid(), TenantId, calendar.Id, monday.AddDays(1),
				DayType.Holiday, "Scope closure", appliesToAll: false, standardId: classId, divisionId: divisionId));
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(reminder, reminderAt, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			var scanner = services.GetServices<INotificationDueSource>().Single(x => x.ScheduledEventKeys.Contains(DayReminder));
			var page = await scanner.ReadPageAsync(new(TenantId, reminderAt, reminderAt, null, 100), CancellationToken.None);
			page.Candidates.ShouldNotContain(x => x.SourceId == timetableId);
		});
		using (var deniedClient = Factory.CreateClient())
		{
			deniedClient.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
				ShikshaMitraSubmodulePermissions.Academics.Notification.View);
			using var denied = await deniedClient.GetAsync($"/api/academic/notifications/operations/{initial.CampaignId}");
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		slot.Subjects.Single(x => x.SubjectId == subjectA).TeacherId = teachers[2].Id;
		await SaveAsync();
		var change = await AssertAudienceAsync(Changed, [new("Student", learnerA), Teacher(0), Teacher(2)]);

		await InSchoolAsync(async services =>
		{
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(initial, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			var current = await reader.LoadAsync(change, DateTimeOffset.UtcNow, CancellationToken.None);
			var former = current.Snapshot!.RecipientContent!.ShouldHaveSingleItem();
			former.Recipient.ShouldBe(Teacher(0));
			former.Variables.Keys.ShouldNotContain("class_name");
		});
		slot.Subjects.RemoveAll(x => x.SubjectId == subjectA);
		await SaveAsync();
		var removal = await AssertAudienceAsync(Changed, [new("Student", learnerA), Teacher(2)], count: 2);
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
			var enrollment = await repository.GetAsync(enrollmentA);
			enrollment.Deactivate();
			await repository.UpdateAsync(enrollment, autoSave: true);
		});
		await InSchoolAsync(async services =>
		{
			var current = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(removal, DateTimeOffset.UtcNow, CancellationToken.None);
			current.CurrentRecipients.ShouldNotContain(x => x.StudentId == learnerA);
		});
		input.Status = TimetableStatus.Draft;
		input.Timetable.Clear();
		slot.Subjects.Single().TeacherId = teachers[0].Id;
		input.Timetable[TimetableDayOfWeek.Wednesday] = [slot];
		await SaveAsync();
		slot.StartTime = new(15, 15);
		await SaveAsync();
		await AssertAudienceAsync(Withdrawn, [new("Student", learnerB), Teacher(1)]);

		NotificationRecipientRefDto Teacher(int index) => new("AssignedTeacher", UserId: teachers[index].UserId, EmployeeId: teachers[index].Id);
		async Task ScanAtAsync(DateTimeOffset now)
		{
			using var workers = Factory.Services.CreateScope();
			var services = workers.ServiceProvider;
			using var school = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var scheduler = services.GetRequiredService<INotificationDueScheduler>();
			for (var page = 0; page < 24; page++)
				if (!(await scheduler.ScanAsync(now, 100, CancellationToken.None)).HasMore) break;
		}

		async Task SaveAsync()
		{
			using var result = await Client.PostAsJsonAsync($"/api/academics/timetable/by-standard/{classId}/division/{divisionId}", input, JsonOptions);
			result.IsSuccessStatusCode.ShouldBeTrue(await result.Content.ReadAsStringAsync());
		}

		async Task<NotificationIntent> AssertAudienceAsync(string key, NotificationRecipientRefDto[] expected, int count = 1)
		{
			NotificationIntent intent = null!;
			await InSchoolAsync(async services =>
			{
				var rows = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == TenantId && x.SourceId == timetableId && x.EventKey == key);
				rows.Count.ShouldBe(count, $"Timetable operation count for {key}.");
				intent = rows.MaxBy(x => x.OccurredAt)!;
				var source = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
				source.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
				source.CurrentRecipients.ToHashSet().SetEquals(expected).ShouldBeTrue();
			});
			return intent;
		}
	}

	private static Task<TEntity> InsertAsync<TEntity>(IServiceProvider services, TEntity entity)
		where TEntity : class, IEntity<Guid> =>
		services.GetRequiredService<IRepository<TEntity, Guid>>().InsertAsync(entity, autoSave: true);

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var school = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}
}
