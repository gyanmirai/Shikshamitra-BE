using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
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
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(",",
			ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View,
			ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add,
			ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Delete));
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var userId = Guid.Parse(TestAuthHandler.TestUserId);
		var teacherId = Guid.Empty;
		var replacementTeacherId = Guid.Empty;
		var replacementUserId = Guid.Empty;
		var subjectId = Guid.NewGuid();
		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IEmployeeRepository>();
			var teacher = (await repository.GetEmployeeByUserIdAsync(userId))!;
			teacherId = teacher.Id;
			replacementUserId = Guid.NewGuid();
			var suffix = replacementUserId.ToString("N")[..10];
			await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(
				new IdentityUser(replacementUserId, "substitute-" + suffix, suffix + "@example.test", TenantId), autoSave: true);
			var replacement = await services.GetRequiredService<EmployeeManager>().CreateEmployeeAsync(replacementUserId, teacher.RoleId,
				"Replacement", "Teacher", null, "RT", EmployeeStatusType.Active, null,
				panNo: "ZZZZZ2345Z", adharNo: "000000000001", employeeGroup: EmployeeGroupType.Teaching);
			await repository.InsertAsync(replacement, autoSave: true);
			replacementTeacherId = replacement.Id;
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId,
				userId: replacementUserId, academicYearId: Factory.SeededAcademicYearId), autoSave: true);
			var subject = new Subject(subjectId, TenantId, "Substitution science", "#24597a", Factory.SeededMediumId);
			await services.GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
			var allocation = new AssignedSubject(Guid.NewGuid(), TenantId, Factory.SeededStandardId, subjectId,
				"SUB" + subjectId.ToString("N")[..7], SubjectStatus.Published);
			allocation.Teachers.Add(new AssignedSubjectTeacher(allocation.Id, subjectId, teacherId, SubjectTeacherType.Primary, TenantId));
			allocation.Teachers.Add(new AssignedSubjectTeacher(allocation.Id, subjectId, replacementTeacherId, SubjectTeacherType.Secondary, TenantId));
			await services.GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(allocation, autoSave: true);
			await AddRuleAsync(services, AcademicNotificationEventKeys.SubstitutionAssigned);
			await AddRuleAsync(services, AcademicNotificationEventKeys.SubstitutionAcknowledgementDue, "SubstituteTeacher", "ConfiguredReviewer");
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
			OriginalTeacherId = replacementTeacherId,
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

		var morning = new DateTimeOffset(DateTime.SpecifyKind(dutyDate.AddHours(4), DateTimeKind.Utc));
		await ScanAtAsync(morning);
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

		await InSchoolAsync(async services =>
		{
			await AddRuleAsync(services, AcademicNotificationEventKeys.SubstitutionChanged, "SubstituteTeacher", "PreviousSubstitute");
			await AddRuleAsync(services, AcademicNotificationEventKeys.SubstitutionCancelled, "SubstituteTeacher", "OriginalTeacher", "PreviousSubstitute");
		});
		var cancel = new SubstitutionActionDto { Reason = "Confidential cancellation reason" };
		duty.StartTime = "11:05";
		using var moved = await Client.PutAsJsonAsync($"{Route}/{silent.Id}", duty, JsonOptions);
		moved.EnsureSuccessStatusCode();
		await CheckTransitionsAsync(silent.Id, 1, 0);
		duty.Remarks = "Internal-only edit";
		using var notes = await Client.PutAsJsonAsync($"{Route}/{silent.Id}", duty, JsonOptions);
		notes.EnsureSuccessStatusCode();
		await CheckTransitionsAsync(silent.Id, 1, 0);
		duty.StartTime = "11:00";
		using var movedBack = await Client.PutAsJsonAsync($"{Route}/{silent.Id}", duty, JsonOptions);
		movedBack.EnsureSuccessStatusCode();
		await CheckTransitionsAsync(silent.Id, 2, 0);
		await InSchoolAsync(async services =>
		{
			var assignment = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetAsync(x =>
				x.TenantId == TenantId && x.SourceId == silent.Id && x.EventKey == AcademicNotificationEventKeys.SubstitutionAssigned);
			var source = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(assignment, DateTimeOffset.UtcNow, CancellationToken.None);
			source.ExclusionCode.ShouldBe("SourceRevisionChanged");
		});

		var cancelled = await PostAndReadAsync<SubstitutionDto>($"{Route}/{silent.Id}/cancel", cancel);
		cancelled.NotificationStatus.ShouldBe("Queued");
		await CheckTransitionsAsync(silent.Id, 2, 1);
		await PostAndReadAsync<SubstitutionDto>($"{Route}/{silent.Id}/cancel", cancel);
		await CheckTransitionsAsync(silent.Id, 2, 1);

		duty.SendNotification = true;
		var neverNotified = await PostAndReadAsync<SubstitutionDto>(Route, duty);
		await CheckIntentsAsync(neverNotified.Id, 1, 0, false);
		await RecordProviderOutcomeAsync(neverNotified.Id, AcademicNotificationEventKeys.SubstitutionAssigned, NotificationDispatchState.UnknownAcceptance);
		var cancelledBeforeDelivery = await PostAndReadAsync<SubstitutionDto>($"{Route}/{neverNotified.Id}/cancel", cancel);
		cancelledBeforeDelivery.NotificationStatus.ShouldBe("Not Sent");
		await CheckTransitionsAsync(neverNotified.Id, 0, 0);

		duty.OriginalTeacherId = null;
		var toBeReassigned = await PostAndReadAsync<SubstitutionDto>(Route, duty);
		await RecordProviderOutcomeAsync(toBeReassigned.Id, AcademicNotificationEventKeys.SubstitutionAssigned, NotificationDispatchState.ProviderAccepted);
		var reassigned = await PostAndReadAsync<SubstitutionDto>($"{Route}/{toBeReassigned.Id}/assign",
			new AssignSubstituteDto { SubstituteTeacherId = replacementTeacherId, SendNotification = true });
		reassigned.NotificationStatus.ShouldBe("Queued");
		await PostAndReadAsync<SubstitutionDto>($"{Route}/{toBeReassigned.Id}/assign",
			new AssignSubstituteDto { SubstituteTeacherId = replacementTeacherId, SendNotification = true });
		await CheckTransitionsAsync(toBeReassigned.Id, 1, 0);
		await PostAndReadAsync<SubstitutionDto>($"{Route}/{toBeReassigned.Id}/assign",
			new AssignSubstituteDto { SubstituteTeacherId = replacementTeacherId, SendNotification = true, AssignedRoomName = "Replacement room" });
		await CheckTransitionsAsync(toBeReassigned.Id, 2, 0);
		var former = new NotificationRecipientRefDto("PreviousSubstitute", UserId: userId, EmployeeId: teacherId);
		await InSchoolAsync(async services =>
		{
			var intents = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.SourceId == toBeReassigned.Id && x.EventKey == AcademicNotificationEventKeys.SubstitutionChanged);
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			var preceding = await reader.LoadAsync(intents.MinBy(x => x.OccurredAt)!, DateTimeOffset.UtcNow, CancellationToken.None);
			preceding.ExclusionCode.ShouldBe("SourceRevisionChanged");
			var source = await reader.LoadAsync(intents.MaxBy(x => x.OccurredAt)!, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.ShouldContain(former);
			source.CurrentRecipients.ShouldContain(new NotificationRecipientRefDto("SubstituteTeacher", UserId: replacementUserId, EmployeeId: replacementTeacherId));
			source.Snapshot!.Variables["room_name"].Value.ShouldBe("Replacement room");
			var removal = source.Snapshot!.RecipientContent!.ShouldHaveSingleItem();
			removal.Recipient.ShouldBe(former);
			removal.Variables.Values.ShouldNotContain(x => x.Value == duty.SubjectName);
			removal.Variables.ContainsKey("room_name").ShouldBeFalse();
			removal.Variables["period_name"].Value.ShouldBe(duty.Period);
			removal.RelativeLink.ShouldBe(source.Snapshot.RelativeLink);
		});
		await PostAndReadAsync<SubstitutionDto>($"{Route}/{toBeReassigned.Id}/cancel", cancel);
		await CheckTransitionsAsync(toBeReassigned.Id, 2, 1, former);

		await CheckReviewerEscalationAsync();

		async Task CheckReviewerEscalationAsync()
		{
			duty.Date = duty.Date.AddDays(7);
			var awaiting = await PostAndReadAsync<SubstitutionDto>(Route, duty);
			await PostAndReadAsync<SubstitutionDto>($"{Route}/{awaiting.Id}/send-reminder", new { });
			const string reminder = AcademicNotificationEventKeys.SubstitutionAcknowledgementDue;
			var acceptedAt = await RecordProviderOutcomeAsync(awaiting.Id, reminder, NotificationDispatchState.ProviderAccepted, morning.AddHours(1));
			var expectedDue = acceptedAt.AddMinutes(30);
			var key = $"Notifications:Academic:Tenants:{TenantId:N}:SubstitutionReviewerUserId";
			var settings = Factory.Services.GetRequiredService<IConfiguration>();
			var previousReviewer = settings[key];
			try
			{
				settings[key] = replacementUserId.ToString();
				await ScanAtAsync(acceptedAt.AddMinutes(29));
				await CheckIntentsAsync(awaiting.Id, 1, 1, true);
				await ScanAtAsync(expectedDue);
				await ScanAtAsync(expectedDue.AddMinutes(2));
				await InSchoolAsync(async services =>
				{
					var rules = services.GetRequiredService<IRepository<NotificationRule, Guid>>();
					var reminderRule = await rules.GetAsync(x => x.EventKey == reminder && x.DivisionId == Factory.SeededDivisionId);
					var codec = services.GetRequiredService<NotificationRuleConfigurationCodec>();
					var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(reminder);
					var originalRule = codec.Deserialize(reminderRule.ConfigurationJson, definition);
					reminderRule.Configure(true, codec.Serialize(originalRule with
					{
						Timing = originalRule.Timing with { ReviewerEscalationMinutes = 90 }
					}, definition), reminderRule.ConcurrencyStamp);
					await rules.UpdateAsync(reminderRule, autoSave: true);
				});
				await InSchoolAsync(async services =>
				{
					var intents = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
						x.SourceId == awaiting.Id && x.EventKey == reminder);
					intents.Count.ShouldBe(2);
					var sources = services.GetRequiredService<NotificationIntentSourceReader>();
					foreach (var intent in intents)
					{
						var source = await sources.LoadAsync(intent, expectedDue, CancellationToken.None);
						if (source.Runtime.DuePhase == NotificationDuePhase.ReviewerEscalation)
						{
							(intent.DueAt - expectedDue).Duration().ShouldBeLessThan(TimeSpan.FromMilliseconds(1));
							source.CurrentRecipients.ShouldHaveSingleItem().ShouldBe(new("ConfiguredReviewer", UserId: replacementUserId));
							settings[key] = userId.ToString();
							(await sources.LoadAsync(intent, expectedDue, CancellationToken.None)).ExclusionCode
								.ShouldBe(NotificationErrorCodes.SourceUnavailable);
							settings[key] = replacementUserId.ToString();
						}
						else
						{
							source.CurrentRecipients.ShouldHaveSingleItem().ShouldBe(new("SubstituteTeacher", UserId: userId, EmployeeId: teacherId));
						}
					}
					intents.Count(x => x.RuleConfigurationJson.Contains("duePhase", StringComparison.Ordinal)).ShouldBe(1);
				});
				await PostAndReadAsync<SubstitutionDto>($"{Route}/{awaiting.Id}/acknowledge", new SubstitutionActionDto());
				await CheckIntentsAsync(awaiting.Id, 1, 2, false);
			}
			finally { settings[key] = previousReviewer; }
		}

		async Task ScanAtAsync(DateTimeOffset simulationTime)
		{
			using var workers = Factory.Services.CreateScope();
			var services = workers.ServiceProvider;
			using var school = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var sources = services.GetServices<INotificationDueSource>()
				.Where(x => x.SourceType == SubstitutionNotificationSource.Type).ToList();
			var scheduler = ActivatorUtilities.CreateInstance<NotificationDueScheduler>(services, sources);
			for (var page = 0; page < 24; page++)
				if (!(await scheduler.ScanAsync(simulationTime, 100, CancellationToken.None)).HasMore) break;
		}

		async Task CheckTransitionsAsync(Guid dutyId, int changes, int cancellations, NotificationRecipientRefDto? cancellationRecipient = null)
		{
			await InSchoolAsync(async services =>
			{
				var stored = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == TenantId && x.SourceId == dutyId &&
					(x.EventKey == AcademicNotificationEventKeys.SubstitutionChanged || x.EventKey == AcademicNotificationEventKeys.SubstitutionCancelled));
				stored.Count(x => x.EventKey == AcademicNotificationEventKeys.SubstitutionChanged).ShouldBe(changes);
				stored.Count(x => x.EventKey == AcademicNotificationEventKeys.SubstitutionCancelled).ShouldBe(cancellations);
				foreach (var intent in stored.Where(x => x.EventKey == AcademicNotificationEventKeys.SubstitutionCancelled))
				{
					var source = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
					source.CurrentRecipients.ShouldHaveSingleItem()
						.ShouldBe(cancellationRecipient ?? new("SubstituteTeacher", UserId: userId, EmployeeId: teacherId));
					source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == cancel.Reason);
				}
			});
		}

		async Task<DateTimeOffset> RecordProviderOutcomeAsync(Guid dutyId, string eventKey, NotificationDispatchState outcome, DateTimeOffset? observedAt = null)
		{
			DateTimeOffset acceptedAt = default;
			await InSchoolAsync(async services =>
			{
				var intents = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
				var intent = await intents.GetAsync(x => x.TenantId == TenantId && x.SourceId == dutyId && x.EventKey == eventKey);
				var snapshot = new CommunicationRecipientSnapshot(Guid.NewGuid(), TenantId)
				{
					CampaignId = intent.CampaignId!.Value, UserId = userId, RecipientId = teacherId, RecipientType = "SubstituteTeacher", DisplayName = "Teacher"
				};
				await services.GetRequiredService<IRepository<CommunicationRecipientSnapshot, Guid>>().InsertAsync(snapshot, autoSave: true);
				var utcNow = observedAt ?? DateTimeOffset.UtcNow;
				var now = utcNow > intent.DueAt ? utcNow : intent.DueAt;
				acceptedAt = now;
				var revisions = JsonSerializer.Deserialize<Guid[]>(intent.TemplateRevisionIdsJson)!;
				var template = await services.GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().GetAsync(x =>
					Enumerable.Contains(revisions, x.Id) && x.RecipientKind == "SubstituteTeacher");
				var dispatch = new NotificationDispatch(Guid.NewGuid(), TenantId, intent.Id,
					template.Id, "{}", "test-teacher",
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
			return acceptedAt;
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

	private async Task AddRuleAsync(IServiceProvider services, string eventKey, params string[] selectors)
	{
		if (selectors.Length == 0) selectors = ["SubstituteTeacher"];
		var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey);
		var templates = services.GetRequiredService<IRepository<CommunicationTemplate, Guid>>();
		var templateIds = new Guid[selectors.Length];
		for (var index = 0; index < selectors.Length; index++)
		{
			var selector = selectors[index];
			var template = await templates.InsertAsync(new(Guid.NewGuid(), TenantId) { TemplateName = eventKey + "." + selector }, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, eventKey, selector,
				CommunicationChannelType.InApp, "en", "Substitution duty", "Sign in for your duty details.", definition.SchemaVersion);
			revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
			await services.GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
			templateIds[index] = template.Id;
		}
		var rule = new NotificationRule(Guid.NewGuid(), TenantId, eventKey, NotificationRuleScope.Create(Factory.SeededAcademicYearId,
			Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId));
		var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, selectors,
			[CommunicationChannelType.InApp], templateIds, NotificationRuleTimingConfiguration.Default with
			{
				QuietHoursEnabled = false, ReminderSchoolDaysBefore = 0, ReminderLocalTime = new(9, 0),
				DelayMinutes = eventKey == AcademicNotificationEventKeys.SubstitutionAcknowledgementDue ? 5 : 0,
				ReviewerEscalationMinutes = eventKey == AcademicNotificationEventKeys.SubstitutionAcknowledgementDue &&
					selectors.Contains("ConfiguredReviewer") ? 30 : null
			}, "en");
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
