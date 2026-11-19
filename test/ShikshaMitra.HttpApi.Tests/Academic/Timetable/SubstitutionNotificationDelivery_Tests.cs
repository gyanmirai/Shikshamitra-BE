using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

[Collection(ApiTestCollection.Name)]
public class SubstitutionNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Route = "/api/academics/timetable/substitutions";
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);

	[Fact]
	public async Task Assignment_and_reminder_use_real_delivery_work_and_only_the_assigned_teacher()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var userId = Guid.Parse(TestAuthHandler.TestUserId);
		var teacherId = Guid.Empty;
		var subjectId = Guid.NewGuid();
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IEmployeeRepository>();
			teacherId = (await repository.GetEmployeeByUserIdAsync(userId))!.Id;
			var subject = new Subject(subjectId, TenantId, "Substitution science", "#24597a", Factory.SeededMediumId);
			await services.GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
			var allocation = new AssignedSubject(Guid.NewGuid(), TenantId, Factory.SeededStandardId, subjectId,
				"SUB" + subjectId.ToString("N")[..7], SubjectStatus.Published);
			allocation.Teachers.Add(new AssignedSubjectTeacher(allocation.Id, subjectId, teacherId, SubjectTeacherType.Primary, TenantId));
			await services.GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(allocation, autoSave: true);
			await AddRuleAsync(services, AcademicNotificationEventKeys.SubstitutionAssigned);
			await AddRuleAsync(services, AcademicNotificationEventKeys.SubstitutionAcknowledgementDue);
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId);
			var calendar = new AcademicCalendar(Guid.NewGuid(), TenantId, Factory.SeededMediumId, year.Id,
				"Substitute duty calendar", year.StartDate, year.EndDate);
			calendar.Publish(Guid.NewGuid());
			await services.GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);
		});

		var dutyDate = DateTimeOffset.UtcNow.Date.AddDays(7);
		while (dutyDate.DayOfWeek != DayOfWeek.Tuesday) dutyDate = dutyDate.AddDays(1);
		var duty = new UpdateSubstitutionDto
		{
			Date = new DateTimeOffset(DateTime.SpecifyKind(dutyDate, DateTimeKind.Utc)),
			AcademicYearId = Factory.SeededAcademicYearId,
			AcademicMediumId = Factory.SeededMediumId,
			StandardId = Factory.SeededStandardId,
			DivisionId = Factory.SeededDivisionId,
			Period = "Period 3",
			StartTime = "11:00",
			EndTime = "11:30",
			ClassName = "Test class",
			DivisionName = "A",
			SubjectId = subjectId,
			SubjectName = "Substitution science",
			SubstituteTeacherId = teacherId,
			AcknowledgementRequired = true,
			SendNotification = false,
			Reason = "Private staffing note"
		};
		var silent = await PostAndReadAsync<SubstitutionDto>(Route, duty);
		silent.NotificationStatus.ShouldBe("Not Sent");
		silent.AcknowledgementStatus.ShouldBe("Not Sent");
		await CheckIntentsAsync(silent.Id, 0, 0, false);

		var assign = new AssignSubstituteDto { SubstituteTeacherId = teacherId, SendNotification = true };
		var assigned = await PostAndReadAsync<SubstitutionDto>($"{Route}/{silent.Id}/assign", assign);
		assigned.NotificationStatus.ShouldBe("Queued");
		assigned.AcknowledgementStatus.ShouldBe("Not Sent");
		(await PostAndReadAsync<SubstitutionDto>($"{Route}/{silent.Id}/assign", assign)).NotificationStatus.ShouldBe("Queued");
		await CheckIntentsAsync(silent.Id, 1, 0, false);
		await RecordProviderOutcomeAsync(silent.Id, AcademicNotificationEventKeys.SubstitutionAssigned, NotificationDispatchState.ProviderAccepted);
		var accepted = await GetAndReadAsync<SubstitutionDto>($"{Route}/{silent.Id}");
		accepted.NotificationStatus.ShouldBe("Sent");
		accepted.AcknowledgementStatus.ShouldBe("Sent");

		using (var workers = Factory.Services.CreateScope())
		{
			var services = workers.ServiceProvider;
			using var school = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var scheduler = services.GetRequiredService<INotificationDueScheduler>();
			var morning = new DateTimeOffset(DateTime.SpecifyKind(dutyDate.AddHours(4), DateTimeKind.Utc));
			for (var page = 0; page < 24; page++)
				if (!(await scheduler.ScanAsync(morning, 100, CancellationToken.None)).HasMore) break;
		}
		await CheckIntentsAsync(silent.Id, 1, 1, true);
		(await PostAndReadAsync<SubstitutionDto>($"{Route}/{silent.Id}/send-reminder", new { })).NotificationStatus.ShouldBe("Queued");
		await CheckIntentsAsync(silent.Id, 1, 1, true);
		await RecordProviderOutcomeAsync(silent.Id, AcademicNotificationEventKeys.SubstitutionAcknowledgementDue, NotificationDispatchState.UnknownAcceptance);
		var unconfirmed = await PostAndReadAsync<SubstitutionDto>($"{Route}/{silent.Id}/send-reminder", new { });
		unconfirmed.NotificationStatus.ShouldBe("Unknown");
		unconfirmed.AcknowledgementStatus.ShouldBe("Sent");
		await CheckIntentsAsync(silent.Id, 1, 1, true);

		var acknowledged = await PostAndReadAsync<SubstitutionDto>($"{Route}/{silent.Id}/acknowledge", new SubstitutionActionDto());
		acknowledged.AcknowledgementStatus.ShouldBe("Accepted");
		await CheckIntentsAsync(silent.Id, 1, 1, false);
		using var removed = await Client.PostAsync($"{Route}/{silent.Id}/send-reminder", null);
		removed.IsSuccessStatusCode.ShouldBeFalse();
		(await removed.Content.ReadAsStringAsync()).ShouldContain(NotificationErrorCodes.StateConflict);

		duty.StartTime = "11:05";
		using var moved = await Client.PutAsJsonAsync($"{Route}/{silent.Id}", duty, JsonOptions);
		moved.EnsureSuccessStatusCode();
		duty.StartTime = "11:00";
		using var movedBack = await Client.PutAsJsonAsync($"{Route}/{silent.Id}", duty, JsonOptions);
		movedBack.EnsureSuccessStatusCode();
		await InSchoolAsync(async services =>
		{
			var assignment = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetAsync(x =>
				x.TenantId == TenantId && x.SourceId == silent.Id && x.EventKey == AcademicNotificationEventKeys.SubstitutionAssigned);
			var source = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(assignment, DateTimeOffset.UtcNow, CancellationToken.None);
			source.ExclusionCode.ShouldBe("SourceRevisionChanged");
		});

		async Task RecordProviderOutcomeAsync(Guid dutyId, string eventKey, NotificationDispatchState outcome)
		{
			await InSchoolAsync(async services =>
			{
				var intents = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
				var intent = await intents.GetAsync(x => x.TenantId == TenantId && x.SourceId == dutyId && x.EventKey == eventKey);
				var snapshot = new CommunicationRecipientSnapshot(Guid.NewGuid(), TenantId)
				{
					CampaignId = intent.CampaignId!.Value, UserId = userId, RecipientType = "SubstituteTeacher", DisplayName = "Teacher"
				};
				await services.GetRequiredService<IRepository<CommunicationRecipientSnapshot, Guid>>().InsertAsync(snapshot, autoSave: true);
				var utcNow = DateTimeOffset.UtcNow;
				var now = utcNow > intent.DueAt ? utcNow : intent.DueAt;
				var dispatch = new NotificationDispatch(Guid.NewGuid(), TenantId, intent.Id,
					JsonSerializer.Deserialize<Guid[]>(intent.TemplateRevisionIdsJson)!.Single(), "{}", "test-teacher",
					CommunicationChannelType.InApp, "synthetic", "synthetic", "synthetic", $"test-{intent.Id:N}", now, intent.ExpiresAt);
				dispatch.BindPlanningEvidence(snapshot.CampaignId, snapshot.Id, null);
				var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				var lease = Guid.NewGuid();
				var dispatchEntry = db.Entry(dispatch);
				dispatchEntry.Property(x => x.Status).CurrentValue = NotificationDispatchState.Processing;
				dispatchEntry.Property(x => x.LeaseId).CurrentValue = lease;
				dispatchEntry.Property(x => x.LeaseExpiresAt).CurrentValue = now.AddMinutes(2);
				dispatchEntry.Property(x => x.AttemptNumber).CurrentValue = 1;
				dispatch.BeginIo(lease, now);
				dispatch.RecordOutcome(lease, now, outcome);
				await services.GetRequiredService<IRepository<NotificationDispatch, Guid>>().InsertAsync(dispatch);
				var intentEntry = db.Entry(intent);
				intentEntry.Property(x => x.Status).CurrentValue = NotificationIntentState.Processing;
				intentEntry.Property(x => x.LeaseId).CurrentValue = lease;
				intentEntry.Property(x => x.LeaseExpiresAt).CurrentValue = now.AddMinutes(2);
				intent.CompletePlanning(lease, now);
				await intents.UpdateAsync(intent);
			});
			using var worker = Factory.Services.CreateScope();
			using var school = worker.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			await worker.ServiceProvider.GetRequiredService<NotificationOutcomeProjector>().RefreshBatchAsync(100, CancellationToken.None);
		}

		async Task CheckIntentsAsync(Guid dutyId, int assignments, int reminders, bool reminderEligible)
		{
			await InSchoolAsync(async services =>
			{
				var stored = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
					.GetListAsync(x => x.TenantId == TenantId && x.SourceId == dutyId && x.SourceType == "Academic.Substitution");
				stored.Count(x => x.EventKey == AcademicNotificationEventKeys.SubstitutionAssigned).ShouldBe(assignments);
				stored.Count(x => x.EventKey == AcademicNotificationEventKeys.SubstitutionAcknowledgementDue).ShouldBe(reminders);
				var sources = services.GetRequiredService<NotificationIntentSourceReader>();
				foreach (var intent in stored)
				{
					var source = await sources.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
					if (intent.EventKey == AcademicNotificationEventKeys.SubstitutionAcknowledgementDue && !reminderEligible)
					{
						source.ExclusionCode.ShouldBe(NotificationErrorCodes.SourceUnavailable);
						continue;
					}
					source.CurrentRecipients.ShouldHaveSingleItem().ShouldBe(new("SubstituteTeacher", UserId: userId, EmployeeId: teacherId));
					source.Runtime.SourceScope.DivisionId.ShouldBe(Factory.SeededDivisionId);
					source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == "Private staffing note");
				}
			});
		}
	}

	private async Task AddRuleAsync(IServiceProvider services, string eventKey)
	{
		var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey);
		var templates = services.GetRequiredService<IRepository<CommunicationTemplate, Guid>>();
		var template = await templates.InsertAsync(new(Guid.NewGuid(), TenantId) { TemplateName = eventKey }, autoSave: true);
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, eventKey, "SubstituteTeacher",
			CommunicationChannelType.InApp, "en", "Substitution duty", "Sign in for your duty details.", definition.SchemaVersion);
		revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
		await services.GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
		var rule = new NotificationRule(Guid.NewGuid(), TenantId, eventKey, NotificationRuleScope.Create(Factory.SeededAcademicYearId,
			Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId));
		var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["SubstituteTeacher"],
			[CommunicationChannelType.InApp], [template.Id], NotificationRuleTimingConfiguration.Default with
			{ QuietHoursEnabled = false, ReminderSchoolDaysBefore = 0, ReminderLocalTime = new(9, 0) }, "en");
		rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(configuration, definition), rule.ConcurrencyStamp);
		await services.GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
	}

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var school = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await action(services);
		await unit.CompleteAsync();
	}
}
