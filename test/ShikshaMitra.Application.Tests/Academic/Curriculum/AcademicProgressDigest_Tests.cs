using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using ProgressEntity = ShikshaMitra.Academic.Syllabus.ChapterProgress;

namespace ShikshaMitra.Academic.Curriculum;

public sealed class AcademicProgressDigest_Tests : SubjectTeacherReassignmentTestBase
{
	private const string EventKey = AcademicNotificationEventKeys.ProgressBehindSchedule;

	[Fact]
	public async Task Behind_schedule_scans_the_saved_scope_once_per_closed_window_and_needs_a_saved_target()
	{
		using var school = Tenant.Change(TenantId, "Synthetic school");
		var run = new DateTimeOffset(2026, 11, 20, 18, 0, 0, TimeSpan.FromMinutes(330));
		var cutoff = new TimeOnly(17, 0);
		NotificationDueSourceCandidate candidate = null!;
		NotificationRule rule = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var allocation = await assignments.GetAsync(AssignmentId);
			allocation.ChangeStatus(SubjectStatus.Published);
			await assignments.UpdateAsync(allocation, autoSave: true);

			var chapters = GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>();
			var chapter = await chapters.GetAsync(ChapterId);
			chapter.PublishStatus = "Published";
			chapter.UpdateDates(run.AddDays(-7), run.AddHours(-3));
			await chapters.UpdateAsync(chapter, autoSave: true);
			var progress = new ProgressEntity(ChapterId, DivisionId);
			progress.UpdateProgress(40);
			await GetRequiredService<IRepository<ProgressEntity, Guid>>().InsertAsync(progress, autoSave: true);

			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Progress digest" };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, EventKey, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "Progress to review", "Open the school inbox.", 1);
			revision.Publish(ActorId, run.AddDays(-1));
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);

			rule = new NotificationRule(Guid.NewGuid(), TenantId, EventKey,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], [header.Id], NotificationRuleTimingConfiguration.Default with { ReminderLocalTime = cutoff }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(config,
				GetRequiredService<AcademicNotificationCatalogue>().Get(EventKey)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var sources = GetRequiredService<IEnumerable<INotificationDueSource>>();
			var due = sources.Where(x => x.ScheduledEventKeys.Contains(EventKey)).ShouldHaveSingleItem();
			var request = new NotificationDueScanRequest(TenantId, run, run, null, 10);
			candidate = (await due.ReadPageAsync(request, CancellationToken.None)).Candidates.ShouldHaveSingleItem();
			candidate.Scope.DivisionId.ShouldBe(DivisionId);
			candidate.OccurrenceAt.ShouldBe(run.AddHours(-1));
			var repeatAt = run.AddMinutes(10);
			var repeat = (await due.ReadPageAsync(request with { CycleStartedAt = repeatAt, Now = repeatAt },
				CancellationToken.None)).Candidates.ShouldHaveSingleItem();
			repeat.SourceId.ShouldBe(candidate.SourceId);
			repeat.SourceRevision.ShouldBe(candidate.SourceRevision);
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
			var snapshot = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, run, CancellationToken.None);
			snapshot.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			snapshot.CurrentRecipients.Select(x => x.EmployeeId!.Value).ShouldBe([OldTeacher, OtherTeacher], ignoreOrder: true);
			snapshot.Snapshot!.Variables["chapter_count"].Value.ShouldBe("1");
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var chapters = GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>();
			var chapter = await chapters.GetAsync(ChapterId);
			chapter.UpdateDates(default, default);
			await chapters.UpdateAsync(chapter, autoSave: true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var due = GetRequiredService<IEnumerable<INotificationDueSource>>()
				.Single(x => x.ScheduledEventKeys.Contains(EventKey));
			(await due.ReadPageAsync(new(TenantId, run, run, null, 10), CancellationToken.None)).Candidates.ShouldBeEmpty();
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.SourceId == candidate.SourceId && x.EventKey == EventKey)).ShouldHaveSingleItem();
			(await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, run, CancellationToken.None))
				.CurrentRecipients.ShouldBeEmpty();
		});
	}
}
