using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public sealed class SyllabusNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public void Syllabus_revision_uses_persisted_timestamp_precision()
	{
		var subject = new AssignedSubject(Guid.NewGuid(), TenantId, StandardId, SubjectId, "SYL", SubjectStatus.Published);
		var start = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.FromMinutes(330));
		var end = start.AddDays(2);
		var chapter = new AssignedSubjectChapter(Guid.NewGuid(), subject.Id, 1, "Synthetic chapter",
			start.AddTicks(7), end.AddTicks(9)) { PublishStatus = "Published", Visibility = "Parents" };
		var original = SyllabusNotificationRevision.Content(subject, chapter).ShouldNotBeNull();

		chapter.ChapterStartTime = start.ToUniversalTime();
		chapter.ChapterEndTime = end.ToUniversalTime();
		SyllabusNotificationRevision.Content(subject, chapter).ShouldBe(original);

		chapter.ChapterEndTime = end.AddTicks(TimeSpan.TicksPerMicrosecond);
		SyllabusNotificationRevision.Content(subject, chapter).ShouldNotBe(original);
	}

	[Fact]
	public async Task Syllabus_only_notifies_for_the_saved_publication_and_material_revision()
	{
		using var actor = AsAdmin();
		using var school = Tenant.Change(TenantId, "Synthetic school");
		string[] events = [AcademicNotificationEventKeys.SyllabusPublished, AcademicNotificationEventKeys.SyllabusRevised];
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var subjects = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await subjects.GetAsync(AssignmentId);
			assignment.ChangeStatus(SubjectStatus.Published);
			await subjects.UpdateAsync(assignment, autoSave: true);
			foreach (var key in events)
			{
				var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = key };
				await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1,
					key, "AssignedTeacher", CommunicationChannelType.InApp, "en", "Syllabus update", "Open the school inbox.", 1);
				revision.Publish(ActorId, DateTimeOffset.UtcNow);
				await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
				var rule = new NotificationRule(Guid.NewGuid(), TenantId, key, NotificationRuleScope.Create());
				var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
					["AssignedTeacher"], [CommunicationChannelType.InApp], [header.Id],
					NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
				rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
					settings, GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
				await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
			}
		});

		var service = GetRequiredService<IChapterAppService>();
		var input = new CreateUpdateChapterDto
		{
			AssignedSubjectId = AssignmentId, ChapterNo = 1, ChapterName = "Draft mathematics chapter",
			StartDate = DateTimeOffset.UtcNow.AddDays(2), EndDate = DateTimeOffset.UtcNow.AddDays(10),
			PublishStatus = "Draft", Visibility = "All", NotifyOnPublish = true
		};
		await SaveAsync();
		(await NoticesAsync()).ShouldBeEmpty();

		input.PublishStatus = "Published";
		await SaveAsync();
		await SaveAsync();
		var published = (await NoticesAsync()).ShouldHaveSingleItem();
		published.EventKey.ShouldBe(events[0]);

		input.CoverageStatus = "inProgress";
		input.ProgressPercentage = 25;
		await SaveAsync();
		(await NoticesAsync()).ShouldHaveSingleItem();

		input.ChapterName = "Revised mathematics chapter";
		await SaveAsync();
		var revision = (await NoticesAsync()).Where(x => x.EventKey == events[1]).ShouldHaveSingleItem();
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = GetRequiredService<NotificationIntentSourceReader>();
			var snapshot = await reader.LoadAsync(revision, DateTimeOffset.UtcNow, CancellationToken.None);
			snapshot.Runtime.SourceScope.ClassId.ShouldBe(StandardId);
			snapshot.CurrentRecipients.Select(x => x.EmployeeId).ToHashSet().SetEquals([OldTeacher, OtherTeacher]).ShouldBeTrue();
			(await reader.LoadAsync(published, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		});

		async Task SaveAsync() => await WithTransactionalUnitOfWorkAsync(async () =>
		{
			(await service.UpdateAsync(ChapterId, input)).ChapterName.ShouldBe(input.ChapterName);
		});

		async Task<NotificationIntent[]> NoticesAsync()
		{
			NotificationIntent[] intents = [];
			await WithUnitOfWorkAsync(async () =>
			{
				intents = (await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == TenantId && x.SourceId == ChapterId)).ToArray();
			});
			return intents;
		}
	}
}
