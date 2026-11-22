using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Notifications;

public sealed partial class AcademicNotificationEndToEndApi_Tests
{
	private static async Task VerifyResultPublicationDeliveryAsync(DeliveryApiFactory fixture, HttpClient staff,
		SchoolSample sample, AcademicNotificationScopeDto scope, HttpClient primary, HttpClient learner, HttpClient withheldGuardian)
	{
		const string eventKey = AcademicNotificationEventKeys.ResultPublished;
		const string resultBody = "A result is available.";
		const string privateGrade = "PRIVATE-A";
		const string publishRoute = "/api/academics/results/publish";
		var resultId = Guid.NewGuid();

		await InSchoolAsync(fixture, async services =>
		{
			var type = await InsertAsync(services, new ExamType(Guid.NewGuid(), School, "Result delivery exam type", null, fixture.SeededMediumId));
			var exam = new Exam(Guid.NewGuid(), type.Id, "Result delivery exam", fixture.SeededStandardId, School);
			exam.AddDivision(Guid.NewGuid(), sample.Division);
			exam.ChangeStatus(ExamStatus.Completed);
			await InsertAsync(services, exam);
			await InsertAsync(services, new ExamResultHeader(resultId, exam.Id, sample.Division, Actor, School));
			await InsertAsync(services, new ExamResultDetail(Guid.NewGuid(), resultId, exam.Id, sample.Learner, sample.Division,
				100, 78, 78, privateGrade, 2, 2, ExamStudentResultStatus.Pass, School));
			await InsertAsync(services, new ExamResultDetail(Guid.NewGuid(), resultId, exam.Id, sample.OtherLearner, sample.Division,
				100, 74, 74, privateGrade, 3, 3, ExamStudentResultStatus.Withheld, School));
		});

		var rule = await PublishGuardianInboxRuleAsync(staff, eventKey, scope);
		rule.Configuration!.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
		rule.Configuration.IncludeStudentAccount.ShouldBeFalse();

		await RunQueueAsync(fixture);
		(await InboxAsync(primary)).Items.ShouldNotContain(x => x.Body.Contains(resultBody, StringComparison.Ordinal));

		using var release = await staff.PostAsJsonAsync(publishRoute, new { resultHeaderId = resultId });
		release.IsSuccessStatusCode.ShouldBeTrue(await release.Content.ReadAsStringAsync());
		using var duplicate = await staff.PostAsJsonAsync(publishRoute, new { resultHeaderId = resultId });
		duplicate.IsSuccessStatusCode.ShouldBeFalse();

		await RunQueueAsync(fixture);
		await RunQueueAsync(fixture);
		var notice = (await InboxAsync(primary)).Items.Where(x => x.Body.Contains(resultBody, StringComparison.Ordinal)).ShouldHaveSingleItem();
		notice.RelativeLink.ShouldBe("/dashboard?openNotifications=1");
		notice.Body.ShouldNotContain(privateGrade);
		notice.Body.ShouldNotContain("78");
		(await InboxAsync(learner)).Items.ShouldNotContain(x => x.Body.Contains(resultBody, StringComparison.Ordinal));
		(await InboxAsync(withheldGuardian)).Items.ShouldBeEmpty();

		var history = await staff.GetFromJsonAsync<PagedResultDto<AcademicNotificationHistoryItemDto>>(
			$"{Root}/history?eventKey={eventKey}&sourceType=Academic.Result&sourceId={resultId}&maxResultCount=20");
		var delivery = history!.Items.ShouldHaveSingleItem();
		delivery.RecipientUserId.ShouldBe(sample.Primary);
		delivery.Status.ShouldBe(NotificationDispatchState.Delivered);
		var recent = await staff.GetFromJsonAsync<AcademicNotificationRecentDeliveriesDto>($"{Root}/history/recent?eventKey={eventKey}");
		var listed = recent!.Items.Single(item => item.SourceId == resultId);
		listed.OperationId.ShouldBe(delivery.OperationId);
		listed.CanViewSummary.ShouldBeTrue();
	}

	private static async Task<AcademicNotificationRuleDto> PublishGuardianInboxRuleAsync(HttpClient staff, string eventKey,
		AcademicNotificationScopeDto scope)
	{
		var published = await PublishInboxTemplateAsync(staff, eventKey, "Guardian");
		return await SaveRuleAsync(staff, new()
		{
			EventKey = eventKey, Scope = scope, Enabled = true, Channels = [CommunicationChannelType.InApp],
			TemplateIds = [published.TemplateId], QuietHours = new(false)
		});
	}

	private static async Task<NotificationTemplateDto> PublishInboxTemplateAsync(HttpClient staff, string eventKey,
		string recipient)
	{
		await PostAsync<int>(staff, $"{Root}/templates/initialize-defaults", new { eventKey });
		var templates = await staff.GetFromJsonAsync<PagedResultDto<NotificationTemplateDto>>(
			$"{Root}/templates?eventKey={eventKey}&maxResultCount=100");
		var draft = templates!.Items.Single(x => x.RecipientKind == recipient && x.Channel == CommunicationChannelType.InApp);
		return await PostAsync<NotificationTemplateDto>(staff, $"{Root}/templates/{draft.TemplateId}/publish",
			new NotificationTemplateRevisionCommandDto(draft.RevisionId, draft.ConcurrencyStamp));
	}
}
