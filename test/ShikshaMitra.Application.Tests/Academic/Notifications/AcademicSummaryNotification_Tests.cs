using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Xunit;
using ClassTeacherRow = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

public sealed class AcademicSummaryNotification_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public void Weekly_window_follows_the_school_day_even_when_daylight_saving_changes_the_offset()
	{
		var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
		var cutoff = new DateTimeOffset(2026, 3, 13, 17, 0, 0, TimeSpan.FromHours(-4));
		var time = new TimeOnly(17, 0);
		AcademicWeeklySummaryTiming.ClosedWindow(cutoff.AddMinutes(-1), zone, DayOfWeek.Friday, time).ShouldBeNull();
		var window = AcademicWeeklySummaryTiming.ClosedWindow(cutoff, zone, DayOfWeek.Friday, time).ShouldNotBeNull();
		window.Start.ShouldBe(new DateTimeOffset(2026, 3, 6, 17, 0, 0, TimeSpan.FromHours(-5)));
		window.End.ShouldBe(cutoff);
		AcademicWeeklySummaryTiming.ClosedWindow(window.ExpiresAt, zone, DayOfWeek.Friday, time).ShouldBeNull();
	}

	[Fact]
	public async Task The_weekly_capture_sends_child_totals_only_to_their_verified_family_and_separate_staff_aggregate()
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		var first = Guid.NewGuid();
		var second = Guid.NewGuid();
		var unlinked = Guid.NewGuid();
		var guardian = Guid.NewGuid();
		var secondGuardian = Guid.NewGuid();
		var now = new DateTimeOffset(2026, 11, 20, 17, 15, 0, TimeSpan.FromMinutes(330)).ToUniversalTime();
		var cutoff = now.AddMinutes(-15);
		var eventKey = AcademicNotificationEventKeys.SummaryWeekly;
		NotificationSourceScopeDto scope = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			scope = (await GetRequiredService<AcademicNotificationDivisionReader>().FindAsync(TenantId, DivisionId, default))!.Scope;
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
			var assigned = (await GetRequiredService<IRepository<AssignedSubject, Guid>>().GetListAsync(x => x.Id == AssignmentId)).Single();
			assigned.Status = SubjectStatus.Published;
			await GetRequiredService<IRepository<AssignedSubject, Guid>>().UpdateAsync(assigned, autoSave: true);
			await GetRequiredService<IRepository<ClassTeacherRow, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TenantId, StandardId, DivisionId, OldTeacher, AcademicMediumId), autoSave: true);
			var classTeacher = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(OldTeacher);
			var staffUserId = classTeacher.UserId!.Value;
			await GetRequiredService<IPermissionManager>().SetAsync(AcademicWeeklySummaryStaff.View, "U", staffUserId.ToString(), true);
			await GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(),
				TenantId, staffUserId, academicYearId: scope.AcademicYearId, educationBoardId: scope.BoardId,
				educationMediumId: scope.MediumId, educationMediumStageId: standard.EducationMediumStageId,
				standardId: StandardId, divisionId: DivisionId), autoSave: true);

			foreach (var (studentId, userId, email) in new[]
			{
				(first, (Guid?)guardian, "first@example.invalid"),
				(second, (Guid?)secondGuardian, "second@example.invalid"),
				(unlinked, (Guid?)null, "unlinked@example.invalid")
			})
			{
				var student = new Student(studentId, TenantId, "Learner", null, "Example", scope.AcademicYearId!.Value,
					scope.BoardId!.Value, scope.MediumId!.Value, standard.EducationMediumStageId, StandardId, DivisionId,
					status: StudentLifeCycleStatusEnum.Enrolled);
				var family = new StudentFamilyDetail(student.Id, TenantId);
				family.UpdateMotherDetails(new("Parent", null, null, email, null, null, null, null, null, null, null, null, null));
				await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);
				await GetRequiredService<IRepository<StudentFamilyDetail, Guid>>().InsertAsync(family, autoSave: true);
				if (userId is not Guid linked) continue;
				var user = new IdentityUser(linked, $"summary-{linked:N}", email, TenantId);
				user.SetEmailConfirmed(linked == guardian);
				await GetRequiredService<IIdentityUserRepository>().InsertAsync(user, autoSave: true);
				await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(
					new(Guid.NewGuid(), TenantId, studentId, linked, "guardian", "mother", ActorId), autoSave: true);
			}

			var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(AcademicMediumId, "Weekly practice");
			await InsertTaskAsync([first, second, unlinked], cutoff.AddDays(-1), published: true);
			await InsertTaskAsync([second], cutoff.AddDays(-2), published: true);
			await InsertTaskAsync([first], cutoff.AddDays(-1), published: false);
			await InsertTaskAsync([first], cutoff, published: true);
			await InsertTaskAsync([first], cutoff.AddDays(-7).AddMinutes(-1), published: true);

			var templateIds = new System.Collections.Generic.List<Guid>();
			foreach (var kind in new[] { "Guardian", "ClassTeacher" })
			foreach (var channel in new[] { CommunicationChannelType.InApp, CommunicationChannelType.Email })
			{
				var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = $"{kind} {channel}" };
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, eventKey,
					kind, channel, "en", "Weekly summary", "{{homework_submission_count}}", 1);
				revision.Publish(ActorId, now);
				await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
				await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
				templateIds.Add(header.Id);
			}
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, eventKey, NotificationRuleScope.Create());
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				["ClassTeacher"], [CommunicationChannelType.InApp, CommunicationChannelType.Email], templateIds,
				NotificationRuleTimingConfiguration.Default with { WeeklySummaryDay = DayOfWeek.Friday, QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>()
				.Serialize(configuration, GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);

			async Task InsertTaskAsync(Guid[] students, DateTimeOffset submitted, bool published)
			{
				var row = new HomeworkAssignmentEntity(Guid.NewGuid(), StandardId, SubjectId, "Private task name",
					"Private task instructions", type.Id, SubmissionType.OnlineText, tenantId: TenantId, educationMediumId: AcademicMediumId);
				var opens = submitted.UtcDateTime.AddDays(-1);
				var deadline = submitted.UtcDateTime.AddDays(1);
				var schedule = new HomeworkDivisionSchedule(Guid.NewGuid(), row.Id, DivisionId, opens, deadline, TenantId);
				if (published)
				{
					schedule.Publish();
					schedule.PublishedAt = opens;
				}
				row.AddDivisionSchedule(schedule);
				await GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().InsertAsync(row, autoSave: true);
				foreach (var student in students)
					await GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>().InsertAsync(
						new StudentSubmissionEntity(Guid.NewGuid(), row.Id, student, DivisionId, deadline, TenantId)
						{ Status = SubmissionStatus.Submitted, SubmissionDate = submitted }, autoSave: true);
			}
		});

		await WithTransactionalUnitOfWorkAsync(async () =>
		{
			var catalogue = GetRequiredService<AcademicNotificationCatalogue>();
			catalogue.GetAvailability(eventKey).IsAvailable.ShouldBeTrue();
			catalogue.GetManualAvailability(eventKey).IsAvailable.ShouldBeFalse();
			var due = GetRequiredService<System.Collections.Generic.IEnumerable<INotificationDueSource>>()
				.Single(x => x.ScanKey == eventKey);
			(await due.ReadPageAsync(new(TenantId, now, cutoff.AddMinutes(-1), null, 10), default)).Candidates.ShouldBeEmpty();
			var candidate = (await due.ReadPageAsync(new(TenantId, now, now, null, 10), default)).Candidates.ShouldHaveSingleItem();
			candidate.Scope.ShouldBe(scope);
			candidate.OccurrenceAt.ShouldBe(cutoff);
			var rule = (await GetRequiredService<IRepository<NotificationRule, Guid>>().GetListAsync(x => x.EventKey == eventKey)).ShouldHaveSingleItem();
			var command = new NotificationEventInput(eventKey, due.SourceType, DivisionId, candidate.SourceRevision,
				now, [], DueAt: cutoff, ExpiresAt: candidate.ExpiresAt,
				DueOccurrence: new(scope, cutoff, rule.Id, rule.Revision));
			var writer = GetRequiredService<INotificationIntentWriter>();
			(await writer.WriteWithOutcomeAsync(command, default)).Created.ShouldBeTrue();
			(await writer.WriteWithOutcomeAsync(command, default)).Created.ShouldBeFalse();

			var rules = GetRequiredService<IRepository<NotificationRule, Guid>>();
			var codec = GetRequiredService<NotificationRuleConfigurationCodec>();
			var definition = catalogue.Get(eventKey);
			var config = codec.Deserialize(rule.ConfigurationJson, definition);
			rule.Configure(true, codec.Serialize(config with { Timing = config.Timing with { ReminderLocalTime = new(18, 0) } },
				definition), rule.ConcurrencyStamp);
			await rules.UpdateAsync(rule, autoSave: true);
			var later = now.AddHours(1);
			var rescheduled = (await due.ReadPageAsync(new(TenantId, later, later, null, 10), default)).Candidates.ShouldHaveSingleItem();
			(await writer.WriteWithOutcomeAsync(command with
			{
				SourceRevision = rescheduled.SourceRevision, OccurredAt = later, DueAt = rescheduled.OccurrenceAt,
				ExpiresAt = rescheduled.ExpiresAt, DueOccurrence = new(scope, rescheduled.OccurrenceAt, rule.Id, rule.Revision)
			}, default)).Created.ShouldBeFalse();
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.EventKey == eventKey))
				.ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, now, default);
			source.ExclusionCode.ShouldBeNull();
			var snapshot = source.Snapshot.ShouldNotBeNull();
			var ownContent = snapshot.RecipientContent!.ToDictionary(x => x.Recipient.StudentId!.Value);
			ownContent[first].Variables["homework_submission_count"].Value.ShouldBe("1");
			ownContent[second].Variables["homework_submission_count"].Value.ShouldBe("2");
			ownContent.Values.ShouldAllBe(x => !x.Variables.ContainsKey("task_title"));
			snapshot.Variables["homework_submission_count"].Value.ShouldBe("4");
			snapshot.EligibleRecipients.ShouldContain(x => x.Kind == "ClassTeacher" && x.EmployeeId == OldTeacher);
			var resolver = GetRequiredService<System.Collections.Generic.IEnumerable<INotificationRuntimeRecipientResolver>>()
				.Single(x => x.Supports(eventKey));
			var resolved = await resolver.ResolveAsync(source, default);
			resolved.ShouldContain(x => x.IsEligible && x.SourceRecipient.StudentId == first && x.Recipient.UserId == guardian &&
				x.Channel == CommunicationChannelType.Email);
			resolved.ShouldNotContain(x => x.IsEligible && x.SourceRecipient.StudentId == first && x.Recipient.UserId == secondGuardian);
			resolved.ShouldNotContain(x => x.IsEligible && x.SourceRecipient.StudentId == unlinked);
			resolved.ShouldContain(x => x.Recipient.UserId == secondGuardian && x.Channel == CommunicationChannelType.Email &&
				x.ExclusionCode == "VerifiedEmailRequired" && x.Destination == null);
			var targeted = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, now, default, new("Student", first));
			targeted.CurrentRecipients.ShouldBe([new("Student", first)]);
			targeted.Snapshot!.RecipientContent!.ShouldHaveSingleItem().Variables["homework_submission_count"].Value.ShouldBe("1");
		});
	}
}
