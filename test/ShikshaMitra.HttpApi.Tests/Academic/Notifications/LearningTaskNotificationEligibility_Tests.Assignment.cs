using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class LearningTaskNotificationEligibility_Tests
{
	private const string AssignmentDue = AcademicNotificationEventKeys.AssignmentDue;
	private const string AssignmentSource = "Academic.Assignment";
	private const string AssignmentView = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View;

	[Fact]
	public async Task Assignment_due_uses_published_division_schedules_and_assignment_specific_submissions()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var division = Guid.Empty;
		await InSchoolAsync(async services =>
		{
			var target = await services.GetRequiredService<DivisionManager>().CreateAsync(Factory.SeededStandardId,
				$"Assignment {Guid.NewGuid():N}"[..22], 32);
			division = (await InsertAsync(services, target)).Id;
		});
		var seed = await SeedAsync(division);
		var assignmentId = Guid.NewGuid();
		var futureId = Guid.NewGuid();
		var draftId = Guid.NewGuid();
		var deadline = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(4).AddHours(12));
		await InSchoolAsync(async services =>
		{
			var homework = await services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().GetAsync(seed.TaskId);
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId);
			var medium = Factory.SeededMediumId;
			var type = await services.GetRequiredService<AssignmentTypeManager>().CreateAsync(medium, $"Reminder {Guid.NewGuid():N}"[..28], defaultMarks: 10, defaultEvaluationMethod: EvaluationMethod.Marks);
			await AddAssignmentAsync(assignmentId, true, null);
			await AddAssignmentAsync(draftId, false, null);
			await AddAssignmentAsync(futureId, true, deadline.UtcDateTime.AddMinutes(-5));

			await InsertAsync(services, new StudentSubmissionEntity(Guid.NewGuid(), assignmentId, seed.Legacy, division,
				deadline.UtcDateTime, TenantId, isAssignment: true) { Status = SubmissionStatus.Submitted });

			var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, medium, year.Id,
				$"Assignment reminders {Guid.NewGuid():N}"[..36], year.StartDate, year.EndDate);
			calendar.Publish(Guid.NewGuid());
			await InsertAsync(services, calendar);
			var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(AssignmentDue);
			var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Assignment due" });
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, AssignmentDue, "Guardian",
				CommunicationChannelType.InApp, "en", "Assignment reminder", "The assignment is due soon.", definition.SchemaVersion);
			revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
			await InsertAsync(services, revision);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, AssignmentDue, NotificationRuleScope.Create(year.Id,
				Factory.SeededBoardId, medium, homework.StandardId, division));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
				[CommunicationChannelType.InApp], [template.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(configuration, definition), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
			await ResetScanCursorsAsync(services);

			async Task AddAssignmentAsync(Guid id, bool publish, DateTime? publishAt)
			{
				var assignment = new AssignmentEntity(id, homework.StandardId, homework.SubjectId, "Practice assignment", "",
					type.Id, SubmissionType.OnlineText, 10, TenantId, year.Id, educationBoardId: Factory.SeededBoardId,
					educationMediumId: medium, educationMediumStageId: Factory.SeededMediumStageId);
				assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(Guid.NewGuid(), id, division, publishAt, deadline.UtcDateTime, TenantId));
				if (publish)
				{
					assignment.ChangeStatus(AssignmentStatus.Published);
					assignment.PublishAll();
				}
				await InsertAsync(services, assignment);
			}
		});

		var scanAt = deadline.AddMinutes(-20);
		await InSchoolAsync(async services =>
		{
			var dueSource = services.GetServices<INotificationDueSource>().SingleOrDefault(x => x.ScheduledEventKeys.Contains(AssignmentDue));
			dueSource.ShouldNotBeNull();
			var page = await dueSource.ReadPageAsync(new(TenantId, scanAt, scanAt, null, 100), CancellationToken.None);
			page.Candidates.ShouldNotContain(x => x.SourceId == futureId || x.SourceId == draftId);
			page.Candidates.Single(x => x.SourceId == assignmentId).Scope.DivisionId.ShouldBe(division);
		});

		using (var scope = Factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var scheduler = scope.ServiceProvider.GetRequiredService<INotificationDueScheduler>();
			(await ScanDueSourcesAsync(scheduler, scanAt)).ShouldBeGreaterThan(0);
			(await ScanDueSourcesAsync(scheduler, scanAt.AddMinutes(2))).ShouldBe(0);
		}

		await InSchoolAsync(async services =>
		{
			var stored = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.EventKey == AssignmentDue);
			var intent = stored.Single(x => x.SourceId == assignmentId);
			stored.ShouldNotContain(x => x.SourceId == futureId || x.SourceId == draftId);
			intent.Status.ShouldBe(NotificationIntentState.Ready);
			intent.ExpiresAt.ShouldBe(deadline);
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			var source = await reader.LoadAsync(intent, scanAt, CancellationToken.None);
			source.CurrentRecipients.Select(x => x.StudentId!.Value).ShouldBe(
				[seed.Active, seed.Submitted, seed.AwaitingReview], ignoreOrder: true);

			await InsertAsync(services, new StudentSubmissionEntity(Guid.NewGuid(), assignmentId, seed.Submitted, division,
				deadline.UtcDateTime, TenantId, isAssignment: true) { Status = SubmissionStatus.Submitted });
			var changed = await reader.LoadAsync(intent, scanAt, CancellationToken.None);
			changed.CurrentRecipients.Select(x => x.StudentId!.Value).ShouldBe([seed.Active, seed.AwaitingReview], ignoreOrder: true);
		});

		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, StaffActions);
		using var denied = await PreviewAssignmentAsync(seed.Active);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		Client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, StaffActions + "," + AssignmentView);
		using var authorized = await PreviewAssignmentAsync(seed.Active);
		authorized.StatusCode.ShouldBe(HttpStatusCode.OK, await authorized.Content.ReadAsStringAsync());
		using var submitted = await PreviewAssignmentAsync(seed.Legacy);
		submitted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		async Task<System.Net.Http.HttpResponseMessage> PreviewAssignmentAsync(Guid studentId) =>
			await Client.PostAsJsonAsync("/api/academic/notifications/manual/preview",
				new PreviewAcademicNotificationManualDto(AssignmentDue, AssignmentSource, assignmentId,
					[new("Student", studentId)], [CommunicationChannelType.InApp]), JsonOptions);
	}

	private static Task ResetScanCursorsAsync(IServiceProvider services) =>
		services.GetRequiredService<IRepository<NotificationScheduleCursor, Guid>>().DeleteDirectAsync(x => x.TenantId == TenantId);

	private static async Task<int> ScanDueSourcesAsync(INotificationDueScheduler scheduler, DateTimeOffset now)
	{
		var created = 0;
		for (var index = 0; index < 8; index++)
		{
			var result = await scheduler.ScanAsync(now, 100, CancellationToken.None);
			created += result.Created;
			if (!result.HasMore) return created;
		}
		throw new InvalidOperationException("Synthetic reminder scan did not finish.");
	}
}
