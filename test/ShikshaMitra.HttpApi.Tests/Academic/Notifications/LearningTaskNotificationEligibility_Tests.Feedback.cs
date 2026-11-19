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
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class LearningTaskNotificationEligibility_Tests
{
	private const string FeedbackReleased = AcademicNotificationEventKeys.HomeworkFeedbackReleased;
	private const string ConfidentialFeedback = "Check the denominator in exercise three.";

	[Fact]
	public async Task Homework_feedback_only_notifies_the_affected_learner_after_review_or_rubric_approval()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var seed = await SeedAsync();
		var feedbackId = Guid.NewGuid();
		var rubricSubmissionId = Guid.NewGuid();
		var subjectId = Guid.Empty;
		await InSchoolAsync(async services =>
		{
			var task = await services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().GetAsync(seed.TaskId);
			subjectId = task.SubjectId;
			foreach (var (id, student) in new[] { (feedbackId, seed.Active), (rubricSubmissionId, seed.Legacy) })
				await InsertAsync(services, new StudentSubmissionEntity(id, task.Id, student, Factory.SeededDivisionId,
					DateTime.UtcNow.AddDays(3), TenantId) { Status = SubmissionStatus.Submitted, SubmissionDate = DateTimeOffset.UtcNow });
			var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(FeedbackReleased);
			var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Homework feedback availability" });
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, FeedbackReleased, "Guardian",
				CommunicationChannelType.InApp, "en", "Feedback available", "Sign in to read the feedback.", definition.SchemaVersion);
			revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
			await InsertAsync(services, revision);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, FeedbackReleased, NotificationRuleScope.Create(
				Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
				[CommunicationChannelType.InApp], [template.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(configuration, definition), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
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
			AcademicMediumId = Factory.SeededMediumId, HomeworkId = seed.TaskId, ScoringModel = RubricScoringModel.PointsBased,
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

		var history = $"/api/academic/notifications/history?eventKey={FeedbackReleased}&sourceType={SourceType}&sourceId=";
		using var releaseHistory = await Client.GetAsync(history + feedbackId);
		releaseHistory.StatusCode.ShouldBe(HttpStatusCode.OK, await releaseHistory.Content.ReadAsStringAsync());
		(await releaseHistory.Content.ReadAsStringAsync()).ShouldNotContain(ConfidentialFeedback);
		using var taskHistory = await Client.GetAsync(history + seed.TaskId);
		taskHistory.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await taskHistory.Content.ReadAsStringAsync());

		async Task WriteFeedbackAsync(SubmissionStatus status)
		{
			using var response = await Client.PatchAsJsonAsync(
				$"/api/homework/{seed.TaskId}/submissions/{seed.Active}/feedback?academicMediumId={Factory.SeededMediumId}",
				new GiveFeedbackDto { Status = status, Feedback = ConfidentialFeedback, Remarks = "Review note" }, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		}

		async Task VerifyReleasesAsync(int expectedFeedback, int expectedRubric)
		{
			await InSchoolAsync(async services =>
			{
				var intents = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == TenantId && x.EventKey == FeedbackReleased && (x.SourceId == feedbackId || x.SourceId == rubricSubmissionId));
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
