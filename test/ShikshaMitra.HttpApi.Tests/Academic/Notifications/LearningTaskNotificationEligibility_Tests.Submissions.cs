using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Common;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class LearningTaskNotificationEligibility_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Submission_digest_uses_one_school_local_window_and_one_scoped_teacher_summary(bool isAssignment)
	{
		var division = Guid.Empty;
		await InSchoolAsync(async services =>
		{
			var created = await services.GetRequiredService<DivisionManager>().CreateAsync(Factory.SeededStandardId,
				$"Digest {Guid.NewGuid():N}"[..22], 32);
			division = (await InsertAsync(services, created)).Id;
		});

		var seed = await SeedAsync(division);
		var eventKey = isAssignment ? AcademicNotificationEventKeys.AssignmentSubmitted : AcademicNotificationEventKeys.HomeworkSubmitted;
		var sourceType = isAssignment ? AssignmentSource : SourceType;
		var taskId = seed.PastId;
		var recipient = default(NotificationRecipientRefDto)!;
		var now = DateTimeOffset.UtcNow.AddMinutes(2);
		var cutoff = new TimeOnly(14, 35);
		var schoolZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
		var localNow = TimeZoneInfo.ConvertTime(now, schoolZone);
		var cutoffDay = DateOnly.FromDateTime(localNow.DateTime);
		if (TimeOnly.FromDateTime(localNow.DateTime) < cutoff) cutoffDay = cutoffDay.AddDays(-1);
		var windowEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(cutoffDay.ToDateTime(cutoff), schoolZone));
		var windowStart = windowEnd.AddDays(-1);
		var published = windowStart.AddHours(-2);
		var deadline = now.AddDays(1);

		await InSchoolAsync(async services =>
		{
			var homeworks = services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
			var task = (await homeworks.WithDetailsAsync(x => x.DivisionSchedules)).Single(x => x.Id == seed.PastId);
			if (isAssignment)
			{
				var type = await services.GetRequiredService<AssignmentTypeManager>().CreateAsync(Factory.SeededMediumId,
					$"Digest {Guid.NewGuid():N}"[..21], defaultMarks: 10, defaultEvaluationMethod: EvaluationMethod.Marks);
				taskId = Guid.NewGuid();
				var assignment = new AssignmentEntity(taskId, task.StandardId, task.SubjectId, "Digest assignment", "", type.Id,
					SubmissionType.OnlineText, 10, TenantId, Factory.SeededAcademicYearId, educationBoardId: Factory.SeededBoardId,
					educationMediumId: Factory.SeededMediumId, educationMediumStageId: Factory.SeededMediumStageId);
				var schedule = new AssignmentDivisionSchedule(Guid.NewGuid(), taskId, division, null, deadline.UtcDateTime, TenantId);
				schedule.Publish(published.UtcDateTime);
				assignment.AddDivisionSchedule(schedule);
				assignment.ChangeStatus(AssignmentStatus.Published);
				await InsertAsync(services, assignment);
			}
			else
			{
				var schedule = task.DivisionSchedules.Single();
				schedule.PublishedAt = published.UtcDateTime;
				schedule.DueDate = deadline.UtcDateTime;
				await homeworks.UpdateAsync(task, autoSave: true);
			}

			var employee = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.UserId.HasValue && x.EmployeeStatusType == EmployeeStatusType.Active)).First();
			recipient = new("AssignedTeacher", UserId: employee.UserId, EmployeeId: employee.Id);
			var subjects = services.GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assigned = (await subjects.WithDetailsAsync(x => x.Teachers)).Single(x => x.SubjectId == task.SubjectId && x.StandardId == task.StandardId);
			assigned.Teachers.Add(new AssignedSubjectTeacher(assigned.Id, task.SubjectId, employee.Id, SubjectTeacherType.Primary, TenantId));
			await subjects.UpdateAsync(assigned, autoSave: true);

			await AddSubmissionAsync(seed.Active, windowStart, SubmissionStatus.Submitted);
			await AddSubmissionAsync(seed.Legacy, windowEnd.AddMinutes(-1), SubmissionStatus.Late);
			await AddSubmissionAsync(seed.Unselected, windowEnd.AddHours(-1), SubmissionStatus.Submitted);
			await AddSubmissionAsync(seed.Submitted, windowEnd, SubmissionStatus.Submitted);
			await AddSubmissionAsync(seed.AwaitingReview, windowEnd.AddHours(-1), SubmissionStatus.Draft);

			var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey);
			var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = $"Digest {taskId:N}" });
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, eventKey, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "Submission summary", "{{submitted_count}} submissions recorded.", definition.SchemaVersion);
			revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
			await InsertAsync(services, revision);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, eventKey, NotificationRuleScope.Create(
				Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, task.StandardId, division));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], [template.Id],
				NotificationRuleTimingConfiguration.Default with { ReminderLocalTime = cutoff, QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(config, definition), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
			await ResetScanCursorsAsync(services);

			async Task AddSubmissionAsync(Guid student, DateTimeOffset at, SubmissionStatus status)
			{
				var submission = isAssignment
					? new StudentSubmissionEntity(Guid.NewGuid(), taskId, student, division, deadline.UtcDateTime, TenantId, isAssignment: true)
					: new StudentSubmissionEntity(Guid.NewGuid(), taskId, student, division, deadline.UtcDateTime, TenantId);
				submission.SubmissionDate = at;
				submission.Status = status;
				submission.SubmittedContent = "Private learner work";
				await InsertAsync(services, submission);
			}
		});

		using (var scope = Factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var scheduler = scope.ServiceProvider.GetRequiredService<INotificationDueScheduler>();
			(await ScanDueSourcesAsync(scheduler, now)).ShouldBeGreaterThan(0);
			(await ScanDueSourcesAsync(scheduler, now.AddMinutes(2))).ShouldBe(0);
		}

		await InSchoolAsync(async services =>
		{
			var intent = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetAsync(x =>
				x.TenantId == TenantId && x.SourceId == taskId && x.EventKey == eventKey);
			intent.SourceType.ShouldBe(sourceType);
			intent.Status.ShouldBe(NotificationIntentState.Ready);
			intent.ExpiresAt.ShouldBe(windowEnd.AddDays(1));
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			await VerifySummaryAsync(2);

			var enrollments = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
			var enrolled = await enrollments.GetAsync(x => x.TenantId == TenantId && x.StudentId == seed.Active);
			enrolled.Deactivate();
			await enrollments.UpdateAsync(enrolled, autoSave: true);
			await VerifySummaryAsync(1);

			async Task VerifySummaryAsync(int count)
			{
				var source = await reader.LoadAsync(intent, now, CancellationToken.None);
				source.Runtime.SourceScope.DivisionId.ShouldBe(division);
				source.Runtime.DueOccurrenceAt.ShouldBe(windowEnd);
				source.CurrentRecipients.ShouldHaveSingleItem().ShouldBe(recipient);
				var variables = source.Snapshot!.RecipientContent!.Single(x => x.Recipient == recipient).Variables;
				variables["submitted_count"].ShouldBe(new(NotificationVariableType.Number, count.ToString(CultureInfo.InvariantCulture)));
				variables.Values.ShouldNotContain(x => x.Value == "Private learner work");
			}
		});
	}
}
