using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[Collection(ApiTestCollection.Name)]
public sealed class AcademicWeeklySummaryPostgresProbe_Tests
{
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);
	private const string Event = AcademicNotificationEventKeys.SummaryWeekly;

	[AcademicPerformancePostgresFact]
	public async Task The_weekly_worker_queues_one_operation_with_separate_child_totals()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		using var client = fixture.CreateClient();
		using var scope = fixture.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var school = services.GetRequiredService<ICurrentTenant>().Change(School, "Synthetic school");
		var units = services.GetRequiredService<IUnitOfWorkManager>();
		var cutoff = new DateTimeOffset(2026, 11, 20, 17, 0, 0, TimeSpan.FromMinutes(330)).ToUniversalTime();
		var now = cutoff.AddMinutes(10);
		var first = Guid.NewGuid();
		var second = Guid.NewGuid();
		var standardId = Guid.Empty;
		var divisionId = Guid.Empty;
		var ruleId = Guid.NewGuid();
		var catalogue = services.GetRequiredService<AcademicNotificationCatalogue>();
		var codec = services.GetRequiredService<NotificationRuleConfigurationCodec>();
		var rules = services.GetRequiredService<IRepository<NotificationRule, Guid>>();
		using (var unit = units.Begin(requiresNew: true, isTransactional: true))
		{
			var standard = await services.GetRequiredService<StandardManager>().CreateAsync(fixture.SeededMediumStageId,
				$"Weekly {Guid.NewGuid():N}"[..20], 12, null);
			standardId = (await InsertAsync(standard)).Id;
			var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standardId, "A", 30);
			divisionId = (await InsertAsync(division)).Id;
			foreach (var learner in new[] { first, second })
			{
				await InsertAsync(new Student(learner, School, "Learner", null, "Summary", fixture.SeededAcademicYearId,
					fixture.SeededBoardId, fixture.SeededMediumId, fixture.SeededMediumStageId, standardId, divisionId,
					status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), School, learner, fixture.SeededAcademicYearId, standardId, divisionId));
				var guardianId = Guid.NewGuid();
				var email = $"guardian-{guardianId:N}@example.invalid";
				var family = new StudentFamilyDetail(learner, School);
				family.UpdateMotherDetails(new("Guardian", null, null, email, null, null, null, null, null, null, null, null, null));
				await InsertAsync(family);
				var guardian = new IdentityUser(guardianId, $"guardian-{guardianId:N}", email, School);
				guardian.SetEmailConfirmed(true);
				await services.GetRequiredService<IIdentityUserRepository>().InsertAsync(guardian, autoSave: true);
				await InsertAsync(new StudentPortalAccountLink(Guid.NewGuid(), School, learner, guardianId, "guardian", "mother", Actor));
			}
			var subject = await InsertAsync(new Subject(Guid.NewGuid(), School, "Weekly summary practice", "#224466", fixture.SeededMediumId));
			await InsertAsync(new AssignedSubject(Guid.NewGuid(), School, standardId, subject.Id, "WEEKLY", SubjectStatus.Published));
			var taskType = await services.GetRequiredService<HomeworkTypeManager>().CreateAsync(fixture.SeededMediumId, "Summary practice");
			await AddHomeworkAsync(taskType.Id, subject.Id, cutoff.AddDays(-1), first, second);
			await AddHomeworkAsync(taskType.Id, subject.Id, cutoff.AddDays(-2), second);
			await AddHomeworkAsync(taskType.Id, subject.Id, cutoff.AddDays(-8), first);

			var template = await InsertAsync(new CommunicationTemplate(Guid.NewGuid(), School) { TemplateName = "Weekly parent summary" });
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), School, template.Id, 1, Event,
				"Guardian", CommunicationChannelType.InApp, "en", "Weekly summary", "{{homework_submission_count}}", 1);
			revision.Publish(Actor, cutoff);
			await InsertAsync(revision);
			var rule = new NotificationRule(ruleId, School, Event, NotificationRuleScope.Create(fixture.SeededAcademicYearId,
				fixture.SeededBoardId, fixture.SeededMediumId, standardId, divisionId));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
				[CommunicationChannelType.InApp], [template.Id],
				NotificationRuleTimingConfiguration.Default with { WeeklySummaryDay = DayOfWeek.Friday, QuietHoursEnabled = false }, "en");
			rule.Configure(true, codec.Serialize(config, catalogue.Get(Event)), rule.ConcurrencyStamp);
			await InsertAsync(rule);
			await unit.CompleteAsync();
		}

		await ScanAsync(now);
		await ScanAsync(now.AddMinutes(1));
		using (var unit = units.Begin(requiresNew: true, isTransactional: true))
		{
			var rule = await rules.GetAsync(ruleId);
			var definition = catalogue.Get(Event);
			var config = codec.Deserialize(rule.ConfigurationJson, definition);
			rule.Configure(true, codec.Serialize(config with { Timing = config.Timing with { ReminderLocalTime = new(18, 0) } },
				definition), rule.ConcurrencyStamp);
			await rules.UpdateAsync(rule, autoSave: true);
			await unit.CompleteAsync();
		}
		await ScanAsync(now.AddHours(1));

		using (var unit = units.Begin(requiresNew: true))
		{
			var rows = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == School && x.SourceId == divisionId && x.EventKey == Event);
			var intent = rows.ShouldHaveSingleItem();
			intent.DueAt.ShouldBe(cutoff);
			var current = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, now, default);
			current.CurrentRecipients.Select(x => x.StudentId).ShouldBe([(Guid?)first, second], ignoreOrder: true);
			var content = current.Snapshot!.RecipientContent!.ToDictionary(x => x.Recipient.StudentId!.Value);
			content[first].Variables["homework_submission_count"].Value.ShouldBe("1");
			content[second].Variables["homework_submission_count"].Value.ShouldBe("2");
			await unit.CompleteAsync();
		}

		Task<TEntity> InsertAsync<TEntity>(TEntity item) where TEntity : class, IEntity<Guid> =>
			services.GetRequiredService<IRepository<TEntity, Guid>>().InsertAsync(item, autoSave: true);

		async Task AddHomeworkAsync(Guid typeId, Guid subjectId, DateTimeOffset submitted, params Guid[] learners)
		{
			var task = new HomeworkAssignmentEntity(Guid.NewGuid(), standardId, subjectId, "Private homework", "Private instructions",
				typeId, SubmissionType.OnlineText, tenantId: School, educationMediumId: fixture.SeededMediumId);
			var open = submitted.UtcDateTime.AddDays(-1);
			var due = submitted.UtcDateTime.AddDays(1);
			var schedule = new HomeworkDivisionSchedule(Guid.NewGuid(), task.Id, divisionId, open, due, School);
			schedule.Publish();
			schedule.PublishedAt = open;
			task.AddDivisionSchedule(schedule);
			await InsertAsync(task);
			foreach (var learner in learners)
				await InsertAsync(new StudentSubmissionEntity(Guid.NewGuid(), task.Id, learner, divisionId, due, School)
				{ Status = SubmissionStatus.Submitted, SubmissionDate = submitted });
		}

		async Task ScanAsync(DateTimeOffset at)
		{
			var sources = services.GetServices<INotificationDueSource>().Where(x => x.ScheduledEventKeys.Contains(Event)).ToList();
			var scheduler = ActivatorUtilities.CreateInstance<NotificationDueScheduler>(services, sources);
			for (var page = 0; page < 10; page++)
				if (!(await scheduler.ScanAsync(at, 100, CancellationToken.None)).HasMore) return;
			throw new InvalidOperationException("The weekly summary scan exceeded the fixture's page limit.");
		}
	}
}
