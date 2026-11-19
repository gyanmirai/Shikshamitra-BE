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
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
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
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

[Collection(ApiTestCollection.Name)]
public sealed class ExamNotificationEvents_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Published = AcademicNotificationEventKeys.ExamTimetablePublished;
	private const string Reminder = AcademicNotificationEventKeys.ExamReminder;
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public async Task Publishing_an_exam_and_its_reminders_follow_the_released_subject_audience()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var examId = Guid.NewGuid();
		var studentA = Guid.NewGuid();
		var studentB = Guid.NewGuid();
		var today = DateTime.UtcNow.Date;
		var monday = today.AddDays((8 - (int)today.DayOfWeek) % 7 + 7);
		Employee teacher = null!;
		Employee primary = null!;
		NotificationRuleScope scope = null!;
		await InSchoolAsync(async services =>
		{
			var year = await InsertAsync(services, await services.GetRequiredService<AcademicYearManager>().CreateAsync(
				$"Exam notices {Guid.NewGuid():N}"[..32], today.AddMonths(-1), today.AddMonths(4)));
			var board = await InsertAsync(services, await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE));
			var medium = await InsertAsync(services, await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Exam notices"));
			var stage = await InsertAsync(services, await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Exam notice stage"));
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Exam notice class", 12, null));
			var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 25));
			var slots = new LectureSlotConfig(Guid.NewGuid(), TenantId, standard.Id, "08:00 - 17:00");
			slots.AddSlotGroup(new(Guid.NewGuid(), slots.Id, 1, 0, 4));
			await InsertAsync(services, slots);
			var type = await InsertAsync(services, new ExamType(Guid.NewGuid(), TenantId, "Exam notice type", null, medium.Id));
			var scale = await InsertAsync(services, new GradingScale(Guid.NewGuid(), TenantId, "Exam notice scale", "100", medium.Id));
			var group = new ElectiveGroup(Guid.NewGuid(), TenantId, "Exam electives", "#24597a", medium.Id);
			var art = new Subject(Guid.NewGuid(), TenantId, "Art", "#24597a", medium.Id);
			var music = new Subject(Guid.NewGuid(), TenantId, "Music", "#24597a", medium.Id);
			await InsertAsync(services, art);
			await InsertAsync(services, music);
			group.AddSubject(art.Id);
			group.AddSubject(music.Id);
			await InsertAsync(services, group);
			teacher = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Actor))!;
			primary = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.RoleId == teacher.RoleId && x.UserId.HasValue &&
				x.UserId != Actor && x.EmployeeStatusType == EmployeeStatusType.Active)).First();
			foreach (var subject in new[] { art, music })
			{
				var assignment = new AssignedSubject(Guid.NewGuid(), TenantId, standard.Id, subject.Id,
					$"EX{subject.Id:N}"[..10], SubjectStatus.Published, subjectType: SubjectType.Elective, electiveGroupId: group.Id);
				if (subject.Id == music.Id) assignment.SetPrimaryTeacher(primary.Id);
				await InsertAsync(services, assignment);
			}
			foreach (var (student, choice) in new[] { (studentA, art.Id), (studentB, music.Id), (Guid.NewGuid(), Guid.Empty) })
			{
				await InsertAsync(services, new Student(student, TenantId, "Sample", null, "Learner", year.Id, board.Id,
					medium.Id, stage.Id, standard.Id, division.Id, status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), TenantId, student, year.Id, standard.Id, division.Id));
				if (choice == Guid.Empty) continue;
				var selection = new StudentElectiveSelection(Guid.NewGuid(), TenantId, student, group.Id, choice, medium.Id);
				selection.LockSelection();
				await InsertAsync(services, selection);
			}
			var allocation = new TeacherSubjectAllocation(Guid.NewGuid(), standard.Id, division.Id, art.Id);
			allocation.Teachers.Add(new(allocation.Id, teacher.Id));
			await InsertAsync(services, allocation);

			var exam = new Exam(examId, type.Id, "Exam notice fixture", standard.Id, TenantId, scale.Id);
			exam.AddDivision(Guid.NewGuid(), division.Id);
			exam.AddSubject(new(Guid.NewGuid(), examId, art.Id, monday, monday.AddHours(10), monday.AddHours(11), 100, 35, gradeScalingId: scale.Id));
			exam.AddSubject(new(Guid.NewGuid(), examId, music.Id, monday.AddDays(1), monday.AddDays(1).AddHours(10),
				monday.AddDays(1).AddHours(11), 100, 35, gradeScalingId: scale.Id));
			await InsertAsync(services, exam);

			scope = NotificationRuleScope.Create(year.Id, board.Id, medium.Id, standard.Id, division.Id);
			foreach (var key in new[] { Published, Reminder })
			{
				var templates = new List<Guid>();
				foreach (var audience in new[] { "Guardian", "AssignedTeacher" })
				{
					var templateId = Guid.NewGuid();
					await InsertAsync(services, new CommunicationTemplate(templateId, TenantId) { TemplateName = key + "." + audience });
					var version = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, templateId, 1, key, audience,
						CommunicationChannelType.InApp, "en", "Exam schedule", "Open the school inbox.", 1);
					version.Publish(Actor, DateTimeOffset.UtcNow);
					await InsertAsync(services, version);
					templates.Add(templateId);
				}
				var rule = new NotificationRule(Guid.NewGuid(), TenantId, key, scope);
				var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
					[CommunicationChannelType.InApp], templates, NotificationRuleTimingConfiguration.Default with
					{ QuietHoursEnabled = false, ReminderSchoolDaysBefore = 0, ReminderLocalTime = new(8, 0) }, "en");
				rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>()
					.Serialize(settings, services.GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
				await InsertAsync(services, rule);
			}
			(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == examId)).ShouldBeEmpty();
		});

		var route = $"/api/academics/exams/{examId}/status";
		for (var attempt = 0; attempt < 2; attempt++)
		{
			using var response = await Client.PutAsJsonAsync(route, new { status = (int)ExamStatus.Published }, JsonOptions);
			response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		}
		var staff = new NotificationRecipientRefDto("AssignedTeacher", UserId: Actor, EmployeeId: teacher.Id);
		var classTeacher = new NotificationRecipientRefDto("AssignedTeacher", UserId: primary.UserId, EmployeeId: primary.Id);
		var intent = await CheckAsync(Published, [new("Student", studentA), new("Student", studentB), staff, classTeacher]);
		using (var viewer = Factory.CreateClient())
		{
			viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Academics.Notification.View);
			using var denied = await viewer.GetAsync($"/api/academic/notifications/operations/{intent.CampaignId}");
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		var cutoff = new DateTimeOffset(monday.AddHours(2.5), TimeSpan.Zero);
		await ScanAsync(cutoff.AddSeconds(-61));
		await InSchoolAsync(async services =>
		{
			var catalogue = services.GetRequiredService<AcademicNotificationCatalogue>();
			catalogue.GetAvailability(Reminder).IsAvailable.ShouldBeTrue();
			catalogue.GetManualAvailability(Reminder).IsAvailable.ShouldBeFalse();
			catalogue.Get(Reminder).RecipientSelectors.ShouldNotContain("Invigilator");
			catalogue.GetAvailability(AcademicNotificationEventKeys.ExamCancelled).IsAvailable.ShouldBeFalse();
			catalogue.GetAvailability(AcademicNotificationEventKeys.ExamRescheduled).IsAvailable.ShouldBeFalse();
			(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.SourceId == examId && x.EventKey == Reminder)).ShouldBeEmpty();
		});
		await ScanAsync(cutoff);
		await ScanAsync(cutoff.AddMinutes(2));
		var reminder = await CheckAsync(Reminder, [new("Student", studentA), staff]);
		reminder.ExpiresAt.ShouldBe(new DateTimeOffset(monday.AddHours(4.5), TimeSpan.Zero));
		await ScanAsync(cutoff.AddDays(1));
		var next = await CheckAsync(Reminder, [new("Student", studentB), classTeacher], expectedCount: 2);
		next.ExpiresAt.ShouldBe(reminder.ExpiresAt!.Value.AddDays(1));

		await InSchoolAsync(async services =>
		{
			var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, scope.MediumId!.Value, scope.AcademicYearId!.Value,
				"Exam closure fixture", monday, monday.AddDays(3));
			calendar.Publish(Actor);
			await InsertAsync(services, calendar);
			await InsertAsync(services, new AcademicCalendarDay(Guid.NewGuid(), TenantId, calendar.Id, monday,
				DayType.EmergencyClosure, "Synthetic closure", appliesToAll: false, standardId: scope.ClassId, divisionId: scope.DivisionId));
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(reminder, cutoff, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		});

		async Task ScanAsync(DateTimeOffset now)
		{
			using var workers = Factory.Services.CreateScope();
			var services = workers.ServiceProvider;
			using var school = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var sources = services.GetServices<INotificationDueSource>().Where(x => x.ScheduledEventKeys.Contains(Reminder)).ToList();
			sources.ShouldHaveSingleItem();
			var scheduler = ActivatorUtilities.CreateInstance<NotificationDueScheduler>(services, sources);
			for (var page = 0; page < 24; page++)
				if (!(await scheduler.ScanAsync(now, 100, CancellationToken.None)).HasMore) break;
		}

		async Task<NotificationIntent> CheckAsync(string key, NotificationRecipientRefDto[] expected, int expectedCount = 1)
		{
			NotificationIntent captured = null!;
			await InSchoolAsync(async services =>
			{
				var notices = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == TenantId && x.SourceId == examId && x.EventKey == key);
				notices.Count.ShouldBe(expectedCount);
				captured = notices.MaxBy(x => x.OccurredAt)!;
				var outcome = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(captured, DateTimeOffset.UtcNow, CancellationToken.None);
				outcome.CurrentRecipients.ToHashSet().SetEquals(expected).ShouldBeTrue();
				outcome.Snapshot!.RecipientContent!.ShouldAllBe(x => x.Variables.ContainsKey("exam_date"));
			});
			return captured;
		}
	}

	[Fact]
	public Task Seating_release_history_requires_access_to_seating() => PublishSeatingAsync(concurrent: false);

	internal async Task PublishSeatingAsync(bool concurrent)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		const string key = AcademicNotificationEventKeys.SeatingPublished;
		var examId = Guid.NewGuid();
		var planId = Guid.NewGuid();
		var studentId = Guid.NewGuid();
		await InSchoolAsync(async services =>
		{
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(Factory.SeededMediumStageId, "Seating notice class", 13, null));
			var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 25));
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
			var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var examType = await InsertAsync(services, new ExamType(Guid.NewGuid(), TenantId, "Seat notice exam", null, medium.Id));
			var scale = await InsertAsync(services, new GradingScale(Guid.NewGuid(), TenantId, "Seat notice scale", "100", medium.Id));
			var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), TenantId, "Seat notice subject", "#24597a", medium.Id));
			var teacher = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Actor))!;
			var curriculum = new AssignedSubject(Guid.NewGuid(), TenantId, standard.Id, subject.Id, "SEAT", SubjectStatus.Published);
			curriculum.SetPrimaryTeacher(teacher.Id);
			await InsertAsync(services, curriculum);
			var building = await InsertAsync(services, new Building { TenantId = TenantId, BuildingName = "Seat notice building", TotalFloors = 1 });
			var floor = await InsertAsync(services, new Floor { BuildingId = building.Id, FloorName = "First", FloorNo = 1 });
			var roomType = await InsertAsync(services, new RoomType { TenantId = TenantId, RoomTypeName = "Seat notice room", ShortName = "SN" });
			var room = await InsertAsync(services, new Room
			{
				TenantId = TenantId, BuildingId = building.Id, FloorId = floor.Id, RoomTypeId = roomType.Id, RoomName = "Seat notice room",
				Capacity = 5, TotalBenches = 5, BenchCapacity = 1, BenchesPerColumn = 5, BenchesPerRow = 1
			});
			var student = new Student(studentId, TenantId, "Synthetic", null, "Learner", board.AcademicYearId, board.Id,
				medium.Id, Factory.SeededMediumStageId, standard.Id, division.Id, status: StudentLifeCycleStatusEnum.Enrolled);
			await InsertAsync(services, student);
			await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), TenantId, studentId, board.AcademicYearId, standard.Id, division.Id));

			var exam = new Exam(examId, examType.Id, "Seat notice exam", standard.Id, TenantId, scale.Id);
			exam.AddDivision(Guid.NewGuid(), division.Id);
			var now = DateTime.UtcNow.Date;
			var date = DateTime.SpecifyKind(now.AddDays((8 - (int)now.DayOfWeek) % 7 + 7), DateTimeKind.Unspecified);
			var paper = new ExamSubject(Guid.NewGuid(), examId, subject.Id, date, date.AddHours(10), date.AddHours(11), 100, 35,
				gradeScalingId: scale.Id);
			paper.AddRoomAssignment(new(Guid.NewGuid(), paper.Id, division.Id, room.Capacity, [room.Id]));
			exam.AddSubject(paper);
			await InsertAsync(services, exam);
			var plan = new ExamSeatingPlan(planId, TenantId, exam.Id, "Seat notice plan", SeatingPlanScopeType.SingleSubject);
			plan.SaveDraft(room.Id.ToString(), "{}", string.Empty, string.Empty, SeatingPlanStatus.Completed);
			await InsertAsync(services, plan);
			await InsertAsync(services, new ExamSeatingPlanSubject(Guid.NewGuid(), TenantId, plan.Id, exam.Id, paper.Id, subject.Id));
			await InsertAsync(services, new ExamSeatAllocation(Guid.NewGuid(), TenantId, plan.Id, exam.Id, studentId, room.Id, 2, null, true));

			var headers = new List<Guid>();
			foreach (var recipient in new[] { "Guardian", "AssignedTeacher" })
			{
				var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId)
					{ TemplateName = "Seat notice " + recipient });
				headers.Add(template.Id);
				var variant = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, key, recipient,
					CommunicationChannelType.InApp, "en", "Exam seating", "Exam seating is available.", 1);
				variant.Publish(Actor, DateTimeOffset.UtcNow);
				await InsertAsync(services, variant);
			}
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, key,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id));
			var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], headers, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>()
				.Serialize(settings, services.GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
		});

		async Task SeatAsync()
		{
			using var response = await Client.PostAsJsonAsync($"/api/academics/exams/{examId}/seating-plan/v2/publish",
				new { notifyParents = true, notifyTeachers = true }, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		}

		async Task ExamAsync()
		{
			using var response = await Client.PutAsJsonAsync($"/api/academics/exams/{examId}/status",
				new { status = (int)ExamStatus.Published }, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
		}

		if (concurrent) await Task.WhenAll(SeatAsync(), ExamAsync());
		else
		{
			await SeatAsync();
			await InSchoolAsync(async services =>
				(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == examId))
					.ShouldBeEmpty());
			await ExamAsync();
		}
		await SeatAsync();

		var campaign = Guid.Empty;
		await InSchoolAsync(async services =>
		{
			var intent = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.TenantId == TenantId && x.SourceId == examId && x.EventKey == key)).ShouldHaveSingleItem();
			campaign = intent.CampaignId!.Value;
			var outcome = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
			outcome.CurrentRecipients.Count.ShouldBe(2);
			outcome.CurrentRecipients.ShouldContain(x => x.Kind == "AssignedTeacher" && x.UserId == Actor);
			var ownSeat = outcome.Snapshot!.RecipientContent!.ShouldHaveSingleItem();
			ownSeat.Recipient.StudentId.ShouldBe(studentId);
			ownSeat.Variables["seat_number"].Value.ShouldBe("2");
			outcome.Snapshot.Variables.ContainsKey("seat_number").ShouldBeFalse();
		});

		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await viewer.GetAsync($"/api/academic/notifications/operations/{campaign}");
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var allowed = await Client.GetAsync($"/api/academic/notifications/operations/{campaign}");
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
	}

	private static Task<TEntity> InsertAsync<TEntity>(IServiceProvider services, TEntity entity)
		where TEntity : class, IEntity<Guid> =>
		services.GetRequiredService<IRepository<TEntity, Guid>>().InsertAsync(entity, autoSave: true);

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}
}
