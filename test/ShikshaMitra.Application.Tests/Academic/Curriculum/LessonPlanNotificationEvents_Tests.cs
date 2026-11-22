using System;
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

namespace ShikshaMitra.Academic.Curriculum;

public sealed class LessonPlanNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData(LessonPlanStatus.Approved, AcademicNotificationEventKeys.LessonPlanApproved)]
	[InlineData(LessonPlanStatus.NeedsChanges, AcademicNotificationEventKeys.LessonPlanChangesRequested)]
	[InlineData(LessonPlanStatus.Rejected, AcademicNotificationEventKeys.LessonPlanRejected)]
	public async Task A_committed_review_notifies_the_plan_owner_once(LessonPlanStatus expected, string eventKey)
	{
		using var actor = AsAdmin();
		using var school = Tenant.Change(TenantId, "Synthetic school");
		var planId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var plan = new LessonPlan(planId, TenantId, board.AcademicYearId, OldTeacher, StandardId, DivisionId, SubjectId,
				ChapterId, DateTime.UtcNow.Date, "Lesson plan for notice test", 30);
			plan.Submit();
			await Plans.InsertAsync(plan, autoSave: true);

			var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = eventKey };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, eventKey, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "Lesson plan reviewed", "Open your school inbox.", 1);
			revision.Publish(ActorId, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, eventKey, NotificationRuleScope.Create());
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], [header.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config, GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		var review = new ReviewRemarksDto { Comment = "Synthetic internal review." };
		for (var attempt = 0; attempt < 2; attempt++)
			await WithTransactionalUnitOfWorkAsync(async () =>
			{
				var result = expected switch
				{
					LessonPlanStatus.Approved => await Service.ApproveAsync(planId, review),
					LessonPlanStatus.NeedsChanges => await Service.RequestChangesAsync(planId, review),
					_ => await Service.RejectAsync(planId, review)
				};
				result.Status.ShouldBe(expected);
			});

		await WithUnitOfWorkAsync(async () =>
		{
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.SourceId == planId && x.EventKey == eventKey)).ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			var recipient = source.CurrentRecipients.ShouldHaveSingleItem();
			recipient.Kind.ShouldBe("AssignedTeacher");
			recipient.EmployeeId.ShouldBe(OldTeacher);
		});
	}
}
