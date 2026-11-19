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
	public async Task Overdue_learning_task_queues_one_scoped_summary_and_rechecks_missing_work(bool isAssignment)
	{
		var schoolClass = Factory.SeededStandardId;
		var division = Guid.Empty;
		await InSchoolAsync(async services =>
		{
			var created = await services.GetRequiredService<DivisionManager>().CreateAsync(schoolClass, $"Overdue {Guid.NewGuid():N}"[..22], 32);
			division = (await InsertAsync(services, created)).Id;
		});
		var seed = await SeedAsync(division);
		var eventKey = isAssignment ? AcademicNotificationEventKeys.AssignmentOverdue : AcademicNotificationEventKeys.HomeworkOverdue;
		var sourceType = isAssignment ? AssignmentSource : SourceType;
		var start = DateTimeOffset.UtcNow;
		var deadline = new DateTimeOffset(start.UtcDateTime.Date.AddHours(start.Hour)).AddHours(-2);
		var sourceId = seed.TaskId;
		var hiddenId = seed.PastId;
		var teacher = default(NotificationRecipientRefDto)!;
		await InSchoolAsync(async services =>
		{
			var tasks = services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
			var task = (await tasks.WithDetailsAsync(x => x.DivisionSchedules)).Single(x => x.Id == seed.TaskId);
			if (isAssignment)
			{
				var type = await services.GetRequiredService<AssignmentTypeManager>().CreateAsync(Factory.SeededMediumId,
					$"Overdue {Guid.NewGuid():N}"[..24], defaultMarks: 10, defaultEvaluationMethod: EvaluationMethod.Marks);
				sourceId = await CreateAssignmentAsync(published: true);
				hiddenId = await CreateAssignmentAsync(published: false);
				await AddSubmissionAsync(services, seed.Submitted, SubmissionStatus.Submitted);
				await AddSubmissionAsync(services, seed.AwaitingReview, SubmissionStatus.NeedsReview);

				async Task<Guid> CreateAssignmentAsync(bool published)
				{
					var id = Guid.NewGuid();
					var assignment = new AssignmentEntity(id, schoolClass, task.SubjectId, "Practice assignment", "", type.Id,
						SubmissionType.OnlineText, 10, TenantId, Factory.SeededAcademicYearId, educationBoardId: Factory.SeededBoardId,
						educationMediumId: Factory.SeededMediumId, educationMediumStageId: Factory.SeededMediumStageId);
					var schedule = new AssignmentDivisionSchedule(Guid.NewGuid(), id, division,
						published ? null : start.AddDays(1).UtcDateTime, deadline.UtcDateTime, TenantId);
					if (published) schedule.Publish(deadline.AddHours(-6).UtcDateTime);
					assignment.AddDivisionSchedule(schedule);
					assignment.ChangeStatus(AssignmentStatus.Published);
					return (await InsertAsync(services, assignment)).Id;
				}
			}
			else
			{
				var schedule = task.DivisionSchedules.Single();
				schedule.DueDate = deadline.UtcDateTime;
				schedule.PublishedAt = deadline.AddHours(-6).UtcDateTime;
				await tasks.UpdateAsync(task, autoSave: true);
			}

			var employee = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.UserId.HasValue && x.EmployeeStatusType == EmployeeStatusType.Active)).First();
			teacher = new("AssignedTeacher", UserId: employee.UserId, EmployeeId: employee.Id);
			var subjects = services.GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assigned = (await subjects.WithDetailsAsync(x => x.Teachers)).Single(x => x.SubjectId == task.SubjectId && x.StandardId == schoolClass);
			assigned.Teachers.Add(new AssignedSubjectTeacher(assigned.Id, task.SubjectId, employee.Id, SubjectTeacherType.Primary, TenantId));
			await subjects.UpdateAsync(assigned, autoSave: true);

			var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey);
			var groups = new[] { "Guardian", "AssignedTeacher" };
			var templates = new Guid[groups.Length];
			for (var index = 0; index < groups.Length; index++)
			{
				var kind = groups[index];
				var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = $"Overdue {kind} {sourceId:N}" });
				templates[index] = template.Id;
				var defaults = services.GetRequiredService<NotificationDefaultTemplateFactory>().Create(definition, kind, CommunicationChannelType.InApp, "en");
				defaults.Body.Contains("{{missing_submission_count}}", StringComparison.Ordinal).ShouldBe(kind == "AssignedTeacher");
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, eventKey, kind,
					CommunicationChannelType.InApp, "en", defaults.Subject, defaults.Body, definition.SchemaVersion);
				revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
				await InsertAsync(services, revision);
			}
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, eventKey, NotificationRuleScope.Create(
				Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, schoolClass, division));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], templates, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(config, definition), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
			await ResetScanCursorsAsync(services);
		});

		await InSchoolAsync(async services =>
		{
			var scanner = services.GetServices<INotificationDueSource>().Single(x => x.SourceType == sourceType);
			var page = await scanner.ReadPageAsync(new(TenantId, start, start, null, 100), CancellationToken.None);
			var occurrence = page.Candidates.SingleOrDefault(x => x.EventKey == eventKey && x.SourceId == sourceId);
			occurrence.ShouldNotBeNull();
			occurrence.OccurrenceAt.ShouldBe(deadline);
			occurrence.Scope.DivisionId.ShouldBe(division);
			occurrence.TimeMode.ShouldBe(NotificationDueTimeMode.FixedOccurrence);
			page.Candidates.ShouldNotContain(x => x.SourceId == hiddenId);
		});

		using (var scope = Factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var scheduler = scope.ServiceProvider.GetRequiredService<INotificationDueScheduler>();
			(await ScanDueSourcesAsync(scheduler, start)).ShouldBeGreaterThan(0);
			(await ScanDueSourcesAsync(scheduler, start.AddMinutes(2))).ShouldBe(0);
		}

		await InSchoolAsync(async services =>
		{
			var intent = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetAsync(x =>
				x.TenantId == TenantId && x.SourceId == sourceId && x.EventKey == eventKey);
			intent.Status.ShouldBe(NotificationIntentState.Ready);
			intent.ExpiresAt.ShouldBe(deadline.AddDays(1));
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			await VerifyAsync([new("Student", seed.Active), new("Student", seed.Legacy), teacher], 2);
			await AddSubmissionAsync(services, seed.Active, SubmissionStatus.Late);
			await VerifyAsync([new("Student", seed.Legacy), teacher], 1);
			await AddSubmissionAsync(services, seed.Legacy, SubmissionStatus.Excused);
			(await reader.LoadAsync(intent, start, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
			(await reader.LoadAsync(intent, deadline.AddDays(1), CancellationToken.None)).ExclusionCode.ShouldBe("SourceExpired");

			async Task VerifyAsync(NotificationRecipientRefDto[] expected, int missing)
			{
				var source = await reader.LoadAsync(intent, start, CancellationToken.None);
				source.CurrentRecipients.ShouldBe(expected, ignoreOrder: true);
				source.Runtime.SourceScope.DivisionId.ShouldBe(division);
				var content = source.Snapshot!.RecipientContent!;
				var digest = content.Single(x => x.Recipient == teacher);
				digest.Variables["missing_submission_count"].ShouldBe(new(NotificationVariableType.Number, missing.ToString(CultureInfo.InvariantCulture)));
				content.Where(x => x.Recipient.Kind == "Student").Select(x => x.Variables["missing_submission_count"])
					.ShouldAllBe(x => x.Type == NotificationVariableType.Number && x.Value == "1");
			}
		});

		async Task AddSubmissionAsync(IServiceProvider services, Guid studentId, SubmissionStatus status)
		{
			var submission = isAssignment
				? new StudentSubmissionEntity(Guid.NewGuid(), sourceId, studentId, division, deadline.UtcDateTime, TenantId, isAssignment: true)
				: new StudentSubmissionEntity(Guid.NewGuid(), sourceId, studentId, division, deadline.UtcDateTime, TenantId);
			submission.Status = status;
			await InsertAsync(services, submission);
		}
	}
}
