using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class LearningTaskNotificationEligibility_Tests
{
	private const string ConfidentialFeedback = "Check the denominator in exercise three.";

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Learning_task_feedback_only_notifies_the_affected_learner_after_review_or_rubric_approval(bool isAssignment)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var seed = await SeedAsync();
		var feedbackId = Guid.NewGuid();
		var rubricSubmissionId = Guid.NewGuid();
		var subjectId = Guid.Empty;
		var reviewedTaskId = seed.TaskId;
		var rubricTaskId = seed.TaskId;
		var eventKey = isAssignment ? AcademicNotificationEventKeys.AssignmentFeedbackReleased : AcademicNotificationEventKeys.HomeworkFeedbackReleased;
		var sourceType = isAssignment ? AssignmentSource : SourceType;
		await InSchoolAsync(async services =>
		{
			var task = await services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().GetAsync(seed.TaskId);
			subjectId = task.SubjectId;
			var deadline = DateTime.UtcNow.AddDays(3);
			if (isAssignment)
			{
				reviewedTaskId = await CreateFeedbackAssignmentAsync(EvaluationMethod.Marks);
				rubricTaskId = await CreateFeedbackAssignmentAsync(EvaluationMethod.Rubric);
			}
			foreach (var (id, student, parent) in new[] { (feedbackId, seed.Active, reviewedTaskId), (rubricSubmissionId, seed.Legacy, rubricTaskId) })
			{
				var submission = isAssignment
					? new StudentSubmissionEntity(id, parent, student, Factory.SeededDivisionId, deadline, TenantId, isAssignment: true)
					: new StudentSubmissionEntity(id, parent, student, Factory.SeededDivisionId, deadline, TenantId);
				submission.Status = SubmissionStatus.Submitted;
				submission.SubmissionDate = DateTimeOffset.UtcNow;
				await InsertAsync(services, submission);
			}
			var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey);
			var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = eventKey + " availability" });
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, eventKey, "Guardian",
				CommunicationChannelType.InApp, "en", "Feedback available", "Sign in to read the feedback.", definition.SchemaVersion);
			revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
			await InsertAsync(services, revision);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, eventKey, NotificationRuleScope.Create(
				Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
				[CommunicationChannelType.InApp], [template.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(configuration, definition), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);

			async Task<Guid> CreateFeedbackAssignmentAsync(EvaluationMethod method)
			{
				var medium = Factory.SeededMediumId;
				var type = await services.GetRequiredService<AssignmentTypeManager>().CreateAsync(medium, $"Feedback {Guid.NewGuid():N}"[..23],
					defaultMarks: method == EvaluationMethod.Marks ? 10 : null, defaultEvaluationMethod: method);
				var id = Guid.NewGuid();
				var assignment = new AssignmentEntity(id, task.StandardId, subjectId, "Practice feedback", "", type.Id,
					SubmissionType.OnlineText, method == EvaluationMethod.Marks ? 10 : 0,
					TenantId, Factory.SeededAcademicYearId, educationBoardId: Factory.SeededBoardId,
					educationMediumId: medium, educationMediumStageId: Factory.SeededMediumStageId, evaluationRequired: true, evaluationMethod: method);
				var schedule = new AssignmentDivisionSchedule(Guid.NewGuid(), id, Factory.SeededDivisionId, null, deadline, TenantId);
				schedule.Publish();
				assignment.AddDivisionSchedule(schedule);
				assignment.ChangeStatus(AssignmentStatus.Published);
				return (await InsertAsync(services, assignment)).Id;
			}
		});

		await WriteFeedbackAsync(SubmissionStatus.NeedsReview);
		await VerifyReleasesAsync(0, 0);

		await WriteFeedbackAsync(SubmissionStatus.Reviewed);
		await WriteFeedbackAsync(SubmissionStatus.Reviewed);
		await VerifyReleasesAsync(1, 0);

		const string rubrics = "/api/academic/rubrics";
		var rubric = await PostAndReadAsync<AssessmentRubricDto>(rubrics, new CreateUpdateRubricDto
		{
			Title = "Learner feedback", StandardId = Factory.SeededStandardId, SubjectId = subjectId,
			AcademicMediumId = Factory.SeededMediumId, HomeworkId = isAssignment ? null : rubricTaskId,
			AssignmentId = isAssignment ? rubricTaskId : null, ScoringModel = RubricScoringModel.PointsBased,
			Criteria = [new() { Title = "Reasoning", IsRequired = true, DisplayOrder = 1, Points = 10 }],
		});
		using var publication = await Client.PostAsync($"{rubrics}/{rubric.Id}/publish", null);
		publication.IsSuccessStatusCode.ShouldBeTrue(await publication.Content.ReadAsStringAsync());
		var evaluation = await PostAndReadAsync<RubricEvaluationDto>($"{rubrics}/{rubric.Id}/evaluations", new UpsertRubricEvaluationDto
		{
			StudentId = seed.Legacy, StudentSubmissionId = rubricSubmissionId, Remarks = ConfidentialFeedback,
			Scores = [new() { CriterionId = rubric.Criteria.Single().Id, Marks = 7 }],
		});
		await VerifyReleasesAsync(1, 0);
		using var submitted = await Client.PostAsync($"{rubrics}/evaluations/{evaluation.Id}/submit", null);
		submitted.IsSuccessStatusCode.ShouldBeTrue(await submitted.Content.ReadAsStringAsync());
		await VerifyReleasesAsync(1, 0);
		using var approved = await Client.PostAsync($"{rubrics}/evaluations/{evaluation.Id}/approve", null);
		approved.IsSuccessStatusCode.ShouldBeTrue(await approved.Content.ReadAsStringAsync());
		await VerifyReleasesAsync(1, 1);

		if (isAssignment)
		{
			using var closed = await Client.PatchAsJsonAsync($"/api/academic/assignments/{reviewedTaskId}/status",
				new AssignmentStatusUpdateDto { Status = (int)AssignmentStatus.Closed }, JsonOptions);
			closed.StatusCode.ShouldBe(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());
			await VerifyReleasesAsync(1, 1);
		}

		var history = $"/api/academic/notifications/history?eventKey={eventKey}&sourceType={sourceType}&sourceId=";
		if (isAssignment)
		{
			var historyView = ShikshaMitraSubmodulePermissions.Academics.Notification.View;
			Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, historyView + "," + SourceView);
			using var denied = await Client.GetAsync(history + feedbackId);
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			Client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
			Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, historyView + "," + AssignmentView);
		}
		using var releaseHistory = await Client.GetAsync(history + feedbackId);
		releaseHistory.StatusCode.ShouldBe(HttpStatusCode.OK, await releaseHistory.Content.ReadAsStringAsync());
		(await releaseHistory.Content.ReadAsStringAsync()).ShouldNotContain(ConfidentialFeedback);
		using var taskHistory = await Client.GetAsync(history + reviewedTaskId);
		taskHistory.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await taskHistory.Content.ReadAsStringAsync());
		if (isAssignment)
			await VerifyDivisionHistoryAsync(history + feedbackId);

		async Task VerifyDivisionHistoryAsync(string url)
		{
			var inDivision = Guid.NewGuid();
			var otherDivision = Guid.NewGuid();
			await InSchoolAsync(async services =>
			{
				var sibling = await services.GetRequiredService<DivisionManager>().CreateAsync(Factory.SeededStandardId,
					$"Feedback {Guid.NewGuid():N}"[..22], 32);
				await InsertAsync(services, sibling);
				foreach (var (viewerId, divisionId) in new[] { (inDivision, Factory.SeededDivisionId), (otherDivision, sibling.Id) })
				{
					await InsertAsync(services, new IdentityUser(viewerId, $"feedback-viewer-{viewerId:N}", $"{viewerId:N}@example.invalid", TenantId));
					await InsertAsync(services, new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: viewerId,
						academicYearId: Factory.SeededAcademicYearId, educationBoardId: Factory.SeededBoardId,
						educationMediumId: Factory.SeededMediumId, educationMediumStageId: Factory.SeededMediumStageId,
						standardId: Factory.SeededStandardId, divisionId: divisionId));
				}
			});

			foreach (var (viewerId, expected) in new[] { (inDivision, HttpStatusCode.OK), (otherDivision, HttpStatusCode.Forbidden) })
			{
				using var viewer = CreateClientAs(viewerId);
				viewer.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Staff");
				viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
					ShikshaMitraSubmodulePermissions.Academics.Notification.View + "," + AssignmentView);
				using var result = await viewer.GetAsync(url);
				result.StatusCode.ShouldBe(expected, await result.Content.ReadAsStringAsync());
			}
		}

		async Task WriteFeedbackAsync(SubmissionStatus status)
		{
			using var response = await Client.PatchAsJsonAsync(
				$"/api/{(isAssignment ? "assignments" : "homework")}/{reviewedTaskId}/submissions/{seed.Active}/feedback?academicMediumId={Factory.SeededMediumId}",
				new GiveFeedbackDto { Status = status, Feedback = ConfidentialFeedback, Remarks = "Review note",
					Marks = isAssignment ? 7 : null, TotalMarks = isAssignment ? 10 : null }, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		}

		async Task VerifyReleasesAsync(int expectedFeedback, int expectedRubric)
		{
			await InSchoolAsync(async services =>
			{
				var intents = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == TenantId && x.EventKey == eventKey && (x.SourceId == feedbackId || x.SourceId == rubricSubmissionId));
				intents.Count(x => x.SourceId == feedbackId).ShouldBe(expectedFeedback);
				intents.Count(x => x.SourceId == rubricSubmissionId).ShouldBe(expectedRubric);
				foreach (var intent in intents)
				{
					intent.Status.ShouldBe(NotificationIntentState.Ready);
					var source = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
					var student = intent.SourceId == feedbackId ? seed.Active : seed.Legacy;
					source.CurrentRecipients.ShouldHaveSingleItem().ShouldBe(new NotificationRecipientRefDto("Student", student));
					source.Runtime.SourceScope.DivisionId.ShouldBe(Factory.SeededDivisionId);
					source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == ConfidentialFeedback || x.Value == "7");
				}
			});
		}
	}
}
