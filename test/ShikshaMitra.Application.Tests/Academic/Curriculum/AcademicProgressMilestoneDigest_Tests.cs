using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using ProgressEntity = ShikshaMitra.Academic.Syllabus.ChapterProgress;

namespace ShikshaMitra.Academic.Curriculum;

public sealed class AcademicProgressMilestoneDigest_Tests : SubjectTeacherReassignmentTestBase
{
	private const string EventKey = AcademicNotificationEventKeys.ProgressMilestoneReached;

	[Fact]
	public async Task Only_the_first_crossing_of_a_configured_percentage_is_eligible()
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		var run = new DateTimeOffset(2026, 11, 20, 12, 30, 0, TimeSpan.Zero);
		NotificationDueSourceCandidate candidate = null!;
		NotificationRule rule = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await assignments.GetAsync(AssignmentId);
			assignment.ChangeStatus(SubjectStatus.Published);
			await assignments.UpdateAsync(assignment, autoSave: true);

			var chapters = GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>();
			var chapter = await chapters.GetAsync(ChapterId);
			chapter.PublishStatus = "Published";
			await chapters.UpdateAsync(chapter, autoSave: true);
			var progress = new ProgressEntity(ChapterId, DivisionId);
			progress.UpdateProgress(80);
			await GetRequiredService<IRepository<ProgressEntity, Guid>>().InsertAsync(progress, autoSave: true);

			var audit = GetRequiredService<IRepository<ChapterProgressLog, Guid>>();
			await AddLogAsync(40, run.AddHours(-5));
			await AddLogAsync(70, run.AddHours(-3));
			await AddLogAsync(50, run.AddHours(3));
			await AddLogAsync(80, run.AddHours(5));

			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Progress milestone" };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, EventKey, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "Chapter milestone", "Open Chapter Management.", 1);
			revision.Publish(ActorId, run.AddDays(-2));
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
			rule = new NotificationRule(Guid.NewGuid(), TenantId, EventKey,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], [header.Id], NotificationRuleTimingConfiguration.Default with
				{
					ProgressMilestonePercentage = 65,
					ReminderLocalTime = new TimeOnly(17, 0)
				}, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(configuration,
				GetRequiredService<AcademicNotificationCatalogue>().Get(EventKey)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);

			async Task AddLogAsync(int percent, DateTimeOffset time)
			{
				var entry = new ChapterProgressLog(Guid.NewGuid(), ChapterId, DivisionId, percent, "Synthetic audit detail");
				typeof(ChapterProgressLog).GetProperty(nameof(ChapterProgressLog.CreationTime))!.SetValue(entry, time.UtcDateTime);
				await audit.InsertAsync(entry, autoSave: true);
			}
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var source = GetRequiredService<IEnumerable<INotificationDueSource>>()
				.Where(x => x.ScheduledEventKeys.Contains(EventKey)).ShouldHaveSingleItem();
			var request = new NotificationDueScanRequest(TenantId, run, run, null, 10);
			candidate = (await source.ReadPageAsync(request, CancellationToken.None)).Candidates.ShouldHaveSingleItem();
			candidate.Scope.DivisionId.ShouldBe(DivisionId);
			candidate.OccurrenceAt.ShouldBe(run.AddHours(-1));
			var tomorrow = run.AddDays(1);
			(await source.ReadPageAsync(request with { CycleStartedAt = tomorrow, Now = tomorrow }, CancellationToken.None))
				.Candidates.ShouldBeEmpty();
		});
		await WithTransactionalUnitOfWorkAsync(async () =>
		{
			var writer = GetRequiredService<INotificationIntentWriter>();
			var input = new NotificationEventInput(EventKey, "Academic.Progress", candidate.SourceId, candidate.SourceRevision,
				run, [], DueAt: candidate.OccurrenceAt, ExpiresAt: candidate.ExpiresAt,
				DueOccurrence: new(candidate.Scope, candidate.OccurrenceAt, rule.Id, rule.Revision));
			(await writer.WriteWithOutcomeAsync(input, CancellationToken.None)).Created.ShouldBeTrue();
			(await writer.WriteWithOutcomeAsync(input, CancellationToken.None)).Created.ShouldBeFalse();
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.SourceId == candidate.SourceId && x.EventKey == EventKey)).ShouldHaveSingleItem();
			var current = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, run, CancellationToken.None);
			current.CurrentRecipients.Select(x => x.EmployeeId!.Value).ShouldBe([OldTeacher, OtherTeacher], ignoreOrder: true);
			current.Snapshot!.Variables["chapter_count"].Value.ShouldBe("1");
			current.Snapshot.Variables["milestone_percentage"].Value.ShouldBe("65");
			current.Snapshot.Variables.Values.ShouldNotContain(x => x.Value == "Synthetic audit detail");
		});
	}
}
