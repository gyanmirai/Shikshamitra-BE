using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class TimetableReassignmentNotifications_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Curriculum_reassignment_queues_the_published_timetable_change_for_both_affected_teachers()
	{
		using var actor = AsAdmin();
		using var school = Tenant.Change(TenantId, "Test school");
		var timetableId = Guid.Empty;
		const string key = AcademicNotificationEventKeys.TimetableChanged;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			timetableId = (await AddTimetableAsync()).Id;
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
			var medium = standard.EducationMediumStage.EducationMedium;
			var board = medium.EducationBoard;
			var templateIds = new List<Guid>();
			foreach (var kind in new[] { "AssignedTeacher", "Guardian" })
			{
				var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Timetable change " + kind };
				await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, key, kind,
					CommunicationChannelType.InApp, "en", "Timetable update", "A timetable period changed.", 1);
				revision.Publish(ActorId, DateTimeOffset.UtcNow);
				await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
				templateIds.Add(template.Id);
			}
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, key,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], templateIds, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(configuration,
				GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		await WithTransactionalUnitOfWorkAsync(async () => { await ChangeTeacherAsync(); });

		await WithUnitOfWorkAsync(async () =>
		{
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.SourceId == timetableId && x.EventKey == key)).ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.Select(x => x.EmployeeId!.Value).ToHashSet().SetEquals([OldTeacher, NewTeacher]).ShouldBeTrue();
			source.Snapshot!.RecipientContent!.ShouldHaveSingleItem().Recipient.EmployeeId.ShouldBe(OldTeacher);
		});
	}
}
