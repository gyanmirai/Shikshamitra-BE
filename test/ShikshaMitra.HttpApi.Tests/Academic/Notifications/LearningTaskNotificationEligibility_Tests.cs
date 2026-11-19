using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[Collection(ApiTestCollection.Name)]
public partial class LearningTaskNotificationEligibility_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string SourceType = "Academic.Homework";
	private const string EventKey = AcademicNotificationEventKeys.HomeworkDue;
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private const string StaffActions = ShikshaMitraSubmodulePermissions.Academics.Notification.View + "," +
		ShikshaMitraSubmodulePermissions.Academics.Notification.Send;
	private const string SourceView = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View;

	[Fact]
	public async Task Upcoming_homework_targets_only_saved_eligible_learners_and_rechecks_the_roster()
	{
		var seed = await SeedAsync();
		var snapshot = await ReadAsync(seed.TaskId);
		snapshot.IsEligible.ShouldBeTrue();
		snapshot.EligibleRecipients.Select(x => x.StudentId!.Value).ToHashSet().SetEquals([seed.Active, seed.Legacy]).ShouldBeTrue();
		var slice = snapshot.AudienceSlices!.ShouldHaveSingleItem();
		slice.Scope.ShouldBe(new(Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
			Factory.SeededStandardId, Factory.SeededDivisionId));
		snapshot.RecipientContent!.All(x => x.Variables.ContainsKey("due_date") && x.RelativeLink == "/dashboard?openNotifications=1").ShouldBeTrue();
		(await ReadAsync(seed.DraftId)).IsEligible.ShouldBeFalse();
		(await ReadAsync(seed.PastId)).IsEligible.ShouldBeFalse();
		(await ReadAsync(seed.TaskId, Guid.NewGuid())).IsEligible.ShouldBeFalse();

		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
			var active = await repository.GetAsync(x => x.StudentId == seed.Active && x.TenantId == TenantId);
			active.Deactivate();
			await repository.UpdateAsync(active);
		});
		var changed = await ReadAsync(seed.TaskId);
		changed.SourceRevision.ShouldBe(snapshot.SourceRevision);
		changed.EligibleRecipients.ShouldHaveSingleItem().StudentId.ShouldBe(seed.Legacy);
	}

	[Fact]
	public async Task Manual_review_uses_the_real_homework_adapter_and_requires_source_access()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var seed = await SeedAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, StaffActions);
		using var catalogue = await Client.GetAsync("/api/academic/notifications/catalogue");
		catalogue.StatusCode.ShouldBe(HttpStatusCode.OK, await catalogue.Content.ReadAsStringAsync());
		var events = (await catalogue.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("events");
		var due = events.EnumerateArray().Single(x => x.GetProperty("definition").GetProperty("eventKey").GetString() == EventKey);
		due.GetProperty("manualAvailability").GetProperty("isAvailable").GetBoolean().ShouldBeTrue();

		using var denied = await PreviewAsync(seed.TaskId, seed.Active);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());

		Client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{StaffActions},{SourceView}");
		using var authorized = await PreviewAsync(seed.TaskId, seed.Active);
		authorized.StatusCode.ShouldBe(HttpStatusCode.OK, await authorized.Content.ReadAsStringAsync());
		using var outside = await PreviewAsync(seed.TaskId, seed.Unselected);
		outside.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await outside.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Due_scan_pages_saved_deadlines_and_revalidates_the_exact_occurrence()
	{
		var seed = await SeedAsync();
		var legacyTaskId = Guid.NewGuid();
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
			var task = await repository.GetAsync(seed.TaskId);
			var legacyType = await InsertAsync(services, new HomeworkAssignmentType(Guid.NewGuid(), "Legacy practice",
				WorkType.Homework, Factory.SeededMediumId, TenantId) { Name = "Legacy practice" });
			var legacy = new HomeworkAssignmentEntity(legacyTaskId, task.StandardId, Factory.SeededDivisionId,
				task.SubjectId, "Legacy practice", "", legacyType.Id, SubmissionType.OnlineText,
				DateTime.UtcNow.AddDays(4), TenantId);
			legacy.Publish();
			await InsertAsync(services, legacy);
		});

		await InSchoolAsync(async services =>
		{
			var source = services.GetServices<INotificationDueSource>().SingleOrDefault(x => x.SourceType == SourceType);
			source.ShouldNotBeNull();
			var now = DateTimeOffset.UtcNow;
			var request = new NotificationDueScanRequest(TenantId, now, now, null, 1);
			var found = new List<NotificationDueSourceCandidate>();
			for (var index = 0; index < 100; index++)
			{
				var page = await source.ReadPageAsync(request, CancellationToken.None);
				page.Candidates.Count.ShouldBeLessThanOrEqualTo(1);
				found.AddRange(page.Candidates);
				if (page.Continuation == null) break;
				page.Continuation.ShouldNotBe(request.Continuation);
				request = request with { Continuation = page.Continuation };
			}
			found.Count(x => x.SourceId == legacyTaskId).ShouldBe(1);
			found.ShouldNotContain(x => x.SourceId == seed.DraftId || x.SourceId == seed.PastId);
			var occurrence = found.Single(x => x.SourceId == seed.TaskId);
			occurrence.Scope.DivisionId.ShouldBe(Factory.SeededDivisionId);
			occurrence.OccurrenceAt.ShouldBe(occurrence.ExpiresAt);
			occurrence.TimeMode.ShouldBe(NotificationDueTimeMode.PrecedingSchoolDays);

			var adapter = services.GetServices<INotificationSourceAdapter>().Single(x => x.EventKeys.Contains(EventKey));
			var context = new NotificationIntentContext(Guid.Empty, TenantId, EventKey, SourceType, seed.TaskId,
				occurrence.SourceRevision, now, [], occurrence.Scope, occurrence.OccurrenceAt);
			var initial = await adapter.LoadAsync(context, CancellationToken.None);
			initial.IsEligible.ShouldBeTrue();
			initial.SourceRevision.ShouldBe(occurrence.SourceRevision);
			initial.EligibleRecipients.Select(x => x.StudentId!.Value).ToHashSet().SetEquals([seed.Active, seed.Legacy]).ShouldBeTrue();

			var foreign = await source.ReadPageAsync(request with { TenantId = Guid.NewGuid(), Continuation = null }, CancellationToken.None);
			foreign.Candidates.ShouldBeEmpty();

			await InsertAsync(services, new StudentSubmissionEntity(Guid.NewGuid(), seed.TaskId, seed.Active, Factory.SeededDivisionId,
				occurrence.OccurrenceAt.UtcDateTime, TenantId) { Status = SubmissionStatus.Submitted });
			var reloaded = await adapter.LoadAsync(context with { IntentId = Guid.NewGuid(),
				AffectedRecipients = [new("Student", seed.Active)] }, CancellationToken.None);
			reloaded.EligibleRecipients.ShouldBeEmpty();

			var repository = services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
			var task = (await repository.WithDetailsAsync(x => x.DivisionSchedules)).Single(x => x.Id == seed.TaskId);
			task.DivisionSchedules.Single().DueDate = occurrence.OccurrenceAt.UtcDateTime.AddDays(1);
			await repository.UpdateAsync(task, autoSave: true);
			(await adapter.LoadAsync(context, CancellationToken.None)).IsEligible.ShouldBeFalse();
			var refreshed = await source.ReadPageAsync(request with { Continuation = null, PageSize = 100 }, CancellationToken.None);
			var moved = refreshed.Candidates.Single(x => x.SourceId == seed.TaskId);
			moved.OccurrenceAt.ShouldBe(occurrence.OccurrenceAt.AddDays(1));
			moved.SourceRevision.ShouldNotBe(occurrence.SourceRevision);
		});
	}

	[Fact]
	public async Task Scheduler_queues_one_real_homework_intent_and_rechecks_submissions_before_dispatch()
	{
		var divisionId = Guid.Empty;
		await InSchoolAsync(async services =>
		{
			var division = await services.GetRequiredService<DivisionManager>().CreateAsync(Factory.SeededStandardId,
				$"Reminder {Guid.NewGuid():N}"[..20], 32);
			divisionId = (await InsertAsync(services, division)).Id;
		});
		var seed = await SeedAsync(divisionId);
		var dueAt = DateTimeOffset.MinValue;
		await InSchoolAsync(async services =>
		{
			var homework = services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
			var task = (await homework.WithDetailsAsync(x => x.DivisionSchedules)).Single(x => x.Id == seed.TaskId);
			dueAt = new DateTimeOffset(DateTime.SpecifyKind(task.DivisionSchedules.Single().DueDate, DateTimeKind.Utc));

			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId);
			var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, Factory.SeededMediumId, year.Id,
				$"Reminder {Guid.NewGuid():N}"[..24], year.StartDate, year.EndDate);
			calendar.Publish(Guid.NewGuid());
			await InsertAsync(services, calendar);

			var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Homework due" });
			var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(EventKey);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, EventKey, "Guardian",
				CommunicationChannelType.InApp, "en", "Homework reminder", "Homework is due soon.", definition.SchemaVersion);
			revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
			await InsertAsync(services, revision);

			var rule = new NotificationRule(Guid.NewGuid(), TenantId, EventKey,
				NotificationRuleScope.Create(year.Id, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededStandardId, divisionId));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
				[CommunicationChannelType.InApp], [template.Id],
				NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			var codec = services.GetRequiredService<NotificationRuleConfigurationCodec>();
			rule.Configure(true, codec.Serialize(configuration, definition), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
		});

		var scanAt = dueAt.AddMinutes(-20);
		using (var scope = Factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			using var school = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var scheduler = services.GetRequiredService<INotificationDueScheduler>();
			(await scheduler.ScanAsync(scanAt, 100, CancellationToken.None)).Created.ShouldBeGreaterThan(0);
			(await scheduler.ScanAsync(scanAt.AddMinutes(2), 100, CancellationToken.None)).Created.ShouldBe(0);
		}

		await InSchoolAsync(async services =>
		{
			var intents = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			var intent = await intents.GetAsync(x => x.TenantId == TenantId && x.SourceId == seed.TaskId && x.EventKey == EventKey);
			intent.Status.ShouldBe(NotificationIntentState.Ready);
			intent.ExpiresAt.ShouldBe(dueAt);
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			var captured = await reader.LoadAsync(intent, scanAt, CancellationToken.None);
			captured.CurrentRecipients.Select(x => x.StudentId!.Value).ToHashSet().SetEquals([seed.Active, seed.Legacy]).ShouldBeTrue();
			captured.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);

			await InsertAsync(services, new StudentSubmissionEntity(Guid.NewGuid(), seed.TaskId, seed.Active, divisionId,
				dueAt.UtcDateTime, TenantId) { Status = SubmissionStatus.Submitted });
			var remaining = await reader.LoadAsync(intent, scanAt, CancellationToken.None);
			remaining.CurrentRecipients.ShouldHaveSingleItem().StudentId.ShouldBe(seed.Legacy);
			(await reader.LoadAsync(intent, dueAt, CancellationToken.None)).ExclusionCode.ShouldBe("SourceExpired");
		});
	}

	private Task<HttpResponseMessage> PreviewAsync(Guid taskId, Guid studentId) =>
		Client.PostAsJsonAsync("/api/academic/notifications/manual/preview",
			new PreviewAcademicNotificationManualDto(EventKey, SourceType, taskId,
				[new("Student", studentId)], [CommunicationChannelType.InApp]), JsonOptions);

	private async Task<NotificationSourceSnapshot> ReadAsync(Guid taskId, Guid? contextTenant = null)
	{
		NotificationSourceSnapshot snapshot = null!;
		await InSchoolAsync(async services =>
		{
			var adapter = services.GetServices<INotificationSourceAdapter>().Single(x => x.EventKeys.Contains(EventKey));
			snapshot = await adapter.LoadAsync(new(Guid.Empty, contextTenant ?? TenantId, EventKey, SourceType, taskId,
				string.Empty, DateTimeOffset.UtcNow, []), CancellationToken.None);
		});
		return snapshot;
	}

	private sealed record Seed(Guid TaskId, Guid DraftId, Guid PastId, Guid Active, Guid Legacy, Guid Unselected,
		Guid Submitted, Guid AwaitingReview);

	private async Task<Seed> SeedAsync(Guid? divisionId = null)
	{
		var seed = new Seed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid());
		await InSchoolAsync(async services =>
		{
			var medium = Factory.SeededMediumId;
			var standard = Factory.SeededStandardId;
			var division = divisionId ?? Factory.SeededDivisionId;
			var year = Factory.SeededAcademicYearId;
			var subject = new Subject(Guid.NewGuid(), TenantId, "Reminder subject", "#24597a", medium);
			var group = new ElectiveGroup(Guid.NewGuid(), TenantId, "Reminder electives", "#24597a", medium);
			await InsertAsync(services, subject);
			await InsertAsync(services, group);
			await InsertAsync(services, new AssignedSubject(Guid.NewGuid(), TenantId, standard, subject.Id,
				$"R{Guid.NewGuid():N}"[..10], SubjectStatus.Published, electiveGroupId: group.Id));
			var type = await services.GetRequiredService<HomeworkTypeManager>().CreateAsync(medium, $"Reminder {Guid.NewGuid():N}"[..28]);
			await SaveHomeworkAsync(seed.TaskId, DateTime.UtcNow.AddDays(3), true);
			await SaveHomeworkAsync(seed.DraftId, DateTime.UtcNow.AddDays(3), false);
			await SaveHomeworkAsync(seed.PastId, DateTime.UtcNow.AddDays(-1), true);

			var withdrawn = Guid.NewGuid();
			var submitted = seed.Submitted;
			var needsReview = seed.AwaitingReview;
			var unlocked = Guid.NewGuid();
			var deletedEnrollment = Guid.NewGuid();
			foreach (var id in new[] { seed.Active, seed.Legacy, seed.Unselected, withdrawn, submitted, needsReview, unlocked, deletedEnrollment })
			{
				await InsertAsync(services, new Student(id, TenantId, "Sample", null, "Learner", year, Factory.SeededBoardId,
					medium, Factory.SeededMediumStageId, standard, division, status: StudentLifeCycleStatusEnum.Enrolled));
				if (id != seed.Legacy)
				{
					var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TenantId, id, year, standard, division);
					if (id == withdrawn) enrollment.Deactivate();
					if (id == deletedEnrollment) enrollment.IsDeleted = true;
					await InsertAsync(services, enrollment);
				}
				if (id == seed.Unselected) continue;
				var selection = new StudentElectiveSelection(Guid.NewGuid(), TenantId, id, group.Id, subject.Id, medium);
				if (id != unlocked) selection.LockSelection();
				await InsertAsync(services, selection);
			}
			await InsertAsync(services, new StudentSubmissionEntity(Guid.NewGuid(), seed.TaskId, submitted, division,
				DateTime.UtcNow.AddDays(3), TenantId) { Status = SubmissionStatus.Submitted, SubmissionDate = DateTimeOffset.UtcNow });
			await InsertAsync(services, new StudentSubmissionEntity(Guid.NewGuid(), seed.TaskId, needsReview, division,
				DateTime.UtcNow.AddDays(3), TenantId) { Status = SubmissionStatus.NeedsReview });

			async Task SaveHomeworkAsync(Guid id, DateTime due, bool published)
			{
				var task = new HomeworkAssignmentEntity(id, standard, subject.Id, "Practice", "", type.Id,
					SubmissionType.OnlineText, TenantId, educationMediumId: medium);
				task.AddDivisionSchedule(new(Guid.NewGuid(), id, division, null, due, TenantId));
				if (published) task.PublishAll();
				await InsertAsync(services, task);
			}
		});
		return seed;
	}

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await action(services);
		await uow.CompleteAsync();
	}

	private static Task<T> InsertAsync<T>(IServiceProvider services, T entity) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
		services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
}
