
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.Exams.Dtos;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService_ScheduleStatus_Tests
{
	[Fact]
	public async Task Marks_notifications_follow_review_transitions_and_keep_private_details_in_the_record()
	{
		using var school = _currentTenant.Change(TestTenantId, "Synthetic school");
		var manager = ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider);
		using var unit = manager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		var (exam, students) = await CreateBulkMarksFixtureAsync();
		var subjectId = exam.ExamSubjects.First().SubjectId;
		var divisionId = exam.ExamDivisions.Single().DivisionId;
		var reviewerId = Guid.NewGuid();
		var teacherId = Guid.NewGuid();
		const string submitted = AcademicNotificationEventKeys.MarksSubmitted;
		const string changes = AcademicNotificationEventKeys.MarksChangesRequested;
		await _identityUserRepository.InsertAsync(new IdentityUser(reviewerId, "marks-reviewer", "reviewer@example.test", TestTenantId), autoSave: true);
		GetRequiredService<IConfiguration>()[$"Notifications:Academic:Tenants:{TestTenantId:N}:MarksReviewerUserId"] = reviewerId.ToString();
		var teacher = await CreateTeacherAsync(teacherId, Guid.NewGuid().ToString("N"));
		foreach (var staffId in new[] { reviewerId, teacherId })
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: staffId), autoSave: true);
		var assignment = new AssignedSubject(Guid.NewGuid(), TestTenantId, exam.StandardId, subjectId, "REVIEW", SubjectStatus.Published);
		assignment.SetPrimaryTeacher(teacher.Id);
		await _assignedSubjectRepository.InsertAsync(assignment, autoSave: true);
		await EnableMarksNoticeAsync(submitted, "ConfiguredReviewer");
		await EnableMarksNoticeAsync(changes, "AssignedTeacher");

		var action = new MarksWorkflowActionInput { DivisionId = divisionId, SubjectId = subjectId };
		var bulk = new BulkSaveMarksInputDto
		{
			DivisionId = divisionId, SubjectId = subjectId,
			Rows = students.Select(s => new StudentMarkRowInputDto { StudentId = s.Id, AttendanceStatus = 0, ObtainedMarks = 81, Feedback = "Private feedback" }).ToList()
		};
		var intents = GetRequiredService<IRepository<NotificationIntent, Guid>>();
		var sourceReader = GetRequiredService<NotificationIntentSourceReader>();
		await _examAppService.SaveDraftAsync(exam.Id, action);
		await _examAppService.BulkSaveMarksAsync(exam.Id, bulk);
		(await intents.GetListAsync(x => x.EventKey == submitted || x.EventKey == changes)).ShouldBeEmpty();

		var submission = await _examAppService.SubmitMarksAsync(exam.Id, action);
		submission.WorkflowStatus.ShouldBe((int)ExamMarksWorkflowStatus.Submitted);
		var first = (await intents.GetListAsync(x => x.EventKey == submitted)).ShouldHaveSingleItem();
		var review = await sourceReader.LoadAsync(first, DateTimeOffset.UtcNow, CancellationToken.None);
		review.CurrentRecipients.ShouldBe([new("ConfiguredReviewer", UserId: reviewerId)]);
		review.Snapshot!.Variables.Keys.Order().ShouldBe(new[] { "exam_name", "school_name", "subject_name" });
		review.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);

		await _examAppService.RequestChangesAsync(exam.Id, new() { DivisionId = divisionId, SubjectId = subjectId, ReviewerNote = "Private correction note" });
		(await sourceReader.LoadAsync(first, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		var correction = (await intents.GetListAsync(x => x.EventKey == changes)).ShouldHaveSingleItem();
		var returned = await sourceReader.LoadAsync(correction, DateTimeOffset.UtcNow, CancellationToken.None);
		returned.CurrentRecipients.ShouldBe([new("AssignedTeacher", UserId: teacherId, EmployeeId: teacher.Id)]);
		returned.Snapshot!.Variables.Keys.Order().ShouldBe(new[] { "exam_name", "school_name", "subject_name" });

		await _examAppService.BulkSaveMarksAsync(exam.Id, bulk);
		(await intents.GetListAsync(x => x.EventKey == changes)).ShouldHaveSingleItem();
		(await sourceReader.LoadAsync(correction, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldHaveSingleItem();
		await _examAppService.SubmitMarksAsync(exam.Id, action);
		var submissions = await intents.GetListAsync(x => x.EventKey == submitted);
		submissions.Count.ShouldBe(2);
		submissions.Select(x => x.SourceRevision).Distinct().Count().ShouldBe(2);
		var latest = submissions.Single(x => x.Id != first.Id);
		(await sourceReader.LoadAsync(correction, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();

		await _examAppService.VerifyMarksAsync(exam.Id, action);
		(await sourceReader.LoadAsync(latest, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		var catalogue = GetRequiredService<AcademicNotificationCatalogue>();
		catalogue.GetAvailability(AcademicNotificationEventKeys.MarksEntryOpened).IsAvailable.ShouldBeFalse();
		catalogue.GetAvailability(AcademicNotificationEventKeys.MarksEntryDue).IsAvailable.ShouldBeFalse();
		await unit.CompleteAsync();
	}

	private async Task EnableMarksNoticeAsync(string eventKey, string audience)
	{
		var templateId = Guid.NewGuid();
		await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(
			new(templateId, TestTenantId) { TemplateName = eventKey }, autoSave: true);
		var variant = new CommunicationTemplateRevision(Guid.NewGuid(), TestTenantId, templateId, 1, eventKey, audience,
			CommunicationChannelType.InApp, "en", "Marks workflow", "Open Record Marks.", 1);
		variant.Publish(FakeAdminUserId, DateTimeOffset.UtcNow);
		await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(variant, autoSave: true);
		var rule = new NotificationRule(Guid.NewGuid(), TestTenantId, eventKey, NotificationRuleScope.Create());
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [audience],
			[CommunicationChannelType.InApp], [templateId], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
		rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>()
			.Serialize(config, GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey)), rule.ConcurrencyStamp);
		await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
	}
}
