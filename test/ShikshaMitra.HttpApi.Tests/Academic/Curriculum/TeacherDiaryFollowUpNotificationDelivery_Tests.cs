using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

[Collection(ApiTestCollection.Name)]
public sealed class TeacherDiaryFollowUpNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string EventKey = AcademicNotificationEventKeys.DiaryFollowUpRequested;
	private const string ManualPath = "/api/academic/notifications/manual";
	private const string PrivateNote = "Private synthetic learner note";
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid TeacherUser = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task A_submitted_diary_supports_reviewed_sends_to_its_exact_staff() => RunAsync();

	internal async Task RunAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var diaryId = Guid.NewGuid();
		var reviewer = Guid.NewGuid();
		var templateId = Guid.NewGuid();
		Guid teacherId = default;
		await InSchoolAsync(async services =>
		{
			var role = await services.GetRequiredService<IIdentityRoleRepository>().FindByNormalizedNameAsync("ADMIN");
			role.ShouldNotBeNull();
			var account = new IdentityUser(reviewer, $"diary-review-{reviewer:N}"[..31], "diary-review@example.test", School);
			account.AddRole(role.Id);
			await InsertAsync(services, account);
			await InsertAsync(services, new UserScopeAssignment(Guid.NewGuid(), School, userId: reviewer));

			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
			var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(Factory.SeededMediumStageId, "Diary notice class", 11, null));
			var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>()
				.CreateAsync(standard.Id, "A", 24));
			var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Diary notice subject", "#24597a", medium.Id));
			teacherId = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(TeacherUser))!.Id;
			var assignment = new AssignedSubject(Guid.NewGuid(), School, standard.Id, subject.Id, "DIARY", SubjectStatus.Published);
			assignment.SetPrimaryTeacher(teacherId);
			await InsertAsync(services, assignment);
			var now = DateTimeOffset.UtcNow;
			var chapter = await InsertAsync(services, new AssignedSubjectChapter(Guid.NewGuid(), assignment.Id, 1, "Diary notice chapter", now, now.AddDays(5))
				{ TenantId = School, AssignedSubjectId = assignment.Id });
			var plan = new LessonPlan(Guid.NewGuid(), School, board.AcademicYearId, teacherId, standard.Id, division.Id,
				subject.Id, chapter.Id, now.UtcDateTime.Date, "Diary notice lesson", 30);
			plan.Submit();
			plan.Approve(reviewer, null);
			await InsertAsync(services, plan);
			var diary = new TeacherDiary(diaryId, School, plan.Id, teacherId, now.UtcDateTime.Date);
			diary.Update(now.UtcDateTime.Date, true, "Synthetic topic", null, 25, null, null, null, null, false,
				null, null, null, PrivateNote, null, null, null, null, null, false, null, PrivateNote);
			await InsertAsync(services, diary);
			await InsertAsync(services, new CommunicationTemplate(templateId, School) { TemplateName = "Diary follow-up test" });
			var version = new CommunicationTemplateRevision(Guid.NewGuid(), School, templateId, 1, EventKey, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "Diary follow-up", "Review {{lesson_plan_title}} in the school inbox.", 1);
			version.Publish(reviewer, now);
			await InsertAsync(services, version);
		});

		var configuration = Factory.Services.GetRequiredService<IConfiguration>();
		var key = $"Notifications:Academic:Tenants:{School:N}:LessonPlanReviewerUserId";
		var previous = configuration[key];
		const string clientUrlKey = "App:ClientUrl";
		var previousClientUrl = configuration[clientUrlKey];
		configuration[key] = reviewer.ToString();
		configuration[clientUrlKey] = "https://school.example.invalid";
		try
		{
			var recipientsPath = $"{ManualPath}/diary-follow-up-recipients/{diaryId}";
			using var operatorClient = Factory.CreateClient();
			operatorClient.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, reviewer.ToString());
			operatorClient.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
			using var draft = await operatorClient.GetAsync(recipientsPath);
			var draftProblem = await draft.Content.ReadAsStringAsync();
			draft.StatusCode.ShouldBe(HttpStatusCode.BadRequest, draftProblem);
			draftProblem.ShouldContain(NotificationErrorCodes.SourceUnavailable);

			using var submitted = await Client.PostAsync($"/api/academic/curriculum/teacher-diaries/{diaryId}/submit", null);
			submitted.StatusCode.ShouldBe(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
			using var viewOnly = Factory.CreateClient();
			viewOnly.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
				ShikshaMitraSubmodulePermissions.Academics.Notification.View);
			using var denied = await viewOnly.GetAsync(recipientsPath);
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

			using var recipientsResponse = await operatorClient.GetAsync(recipientsPath);
			recipientsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await recipientsResponse.Content.ReadAsStringAsync());
			var recipients = (await recipientsResponse.Content.ReadFromJsonAsync<List<NotificationRecipientRefDto>>(JsonOptions))!;
			var teacher = new NotificationRecipientRefDto("AssignedTeacher", UserId: TeacherUser, EmployeeId: teacherId);
			recipients.ShouldBe([teacher, new("ConfiguredReviewer", UserId: reviewer)]);

			var previewRequest = new PreviewAcademicNotificationManualDto(EventKey, "Academic.Diary", diaryId, [teacher],
				[CommunicationChannelType.InApp], templateId);
			using var spoofed = await operatorClient.PostAsJsonAsync($"{ManualPath}/preview",
				previewRequest with { SelectedRecipients = [teacher with { UserId = Guid.NewGuid() }] }, JsonOptions);
			spoofed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			using var preview = await operatorClient.PostAsJsonAsync($"{ManualPath}/preview", previewRequest, JsonOptions);
			var body = await preview.Content.ReadAsStringAsync();
			preview.StatusCode.ShouldBe(HttpStatusCode.OK, body);
			var review = (await preview.Content.ReadFromJsonAsync<AcademicNotificationManualPreviewDto>(JsonOptions))!;
			review.BlockingIssues.ShouldBeEmpty(body);
			review.ReviewToken.ShouldNotBeNull(body);
			body.ShouldContain("Example lesson plan title");
			body.ShouldNotContain("Diary notice lesson");
			body.ShouldNotContain(PrivateNote);
			review.Samples.ShouldHaveSingleItem().IsSynthetic.ShouldBeTrue();
			review.EligibleRecipientCount.ShouldBe(1);
			await InSchoolAsync(async services =>
				(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == diaryId))
					.ShouldBeEmpty());

			using var sent = await operatorClient.PostAsJsonAsync($"{ManualPath}/send",
				new ConfirmAcademicNotificationManualDto(review.ReviewToken.Value, $"diary-test-{diaryId:N}"), JsonOptions);
			sent.StatusCode.ShouldBe(HttpStatusCode.Accepted, await sent.Content.ReadAsStringAsync());
			var operation = (await sent.Content.ReadFromJsonAsync<AcademicNotificationOperationDto>(JsonOptions))!;
			operation.Status.ShouldBe(CommunicationCampaignStatus.Queued);
			await InSchoolAsync(async services =>
			{
				var queued = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
					.GetListAsync(x => x.SourceId == diaryId && x.CampaignId == operation.OperationId)).ShouldHaveSingleItem();
				queued.Origin.ShouldBe(NotificationIntentOrigin.ReviewedManual);
				var source = await services.GetRequiredService<NotificationIntentSourceReader>()
					.LoadAsync(queued, DateTimeOffset.UtcNow, CancellationToken.None);
				source.CurrentRecipients.ShouldBe([teacher]);
				source.Snapshot!.Variables["lesson_plan_title"].Value.ShouldBe("Diary notice lesson");
			});
		}
		finally
		{
			configuration[clientUrlKey] = previousClientUrl;
			configuration[key] = previous;
		}
	}

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(School, "Synthetic school");
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}

	private static Task<TEntity> InsertAsync<TEntity>(IServiceProvider services, TEntity row) where TEntity : class, IEntity<Guid> =>
		services.GetRequiredService<IRepository<TEntity, Guid>>().InsertAsync(row, autoSave: true);
}

[Collection(ApiTestCollection.Name)]
public sealed class TeacherDiaryFollowUpNotificationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Diary_follow_up_uses_the_same_review_and_send_contract_on_Postgres()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new TeacherDiaryFollowUpNotificationDelivery_Tests(fixture).RunAsync();
	}
}
