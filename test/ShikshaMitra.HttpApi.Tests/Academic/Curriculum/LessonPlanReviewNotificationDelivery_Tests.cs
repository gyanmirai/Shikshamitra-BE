using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
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
public sealed class LessonPlanReviewNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string EventKey = AcademicNotificationEventKeys.LessonPlanChangesRequested;
	private const string Comment = "Private review text for the synthetic workflow.";
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid TeacherUser = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task A_saved_review_has_one_owner_notice_and_respects_history_permissions() => RunAsync();

	internal async Task RunAsync()
	{
		var reviewer = Guid.NewGuid();
		var planId = Guid.NewGuid();
		Guid divisionId = default, teacherId = default, chapterId = default;
		await InSchoolAsync(async services =>
		{
			var role = await services.GetRequiredService<IIdentityRoleRepository>().FindByNormalizedNameAsync("ADMIN");
			role.ShouldNotBeNull();
			var account = new IdentityUser(reviewer, $"plan-review-{reviewer:N}"[..31], "lesson-plan-review@example.test", School);
			account.AddRole(role.Id);
			await InsertAsync(services, account);
			await InsertAsync(services, new UserScopeAssignment(Guid.NewGuid(), School, userId: reviewer));
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
			var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(Factory.SeededMediumStageId, "Lesson plan notice class", 10, null));
			divisionId = (await InsertAsync(services, await services.GetRequiredService<DivisionManager>()
				.CreateAsync(standard.Id, "A", 24))).Id;
			var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Lesson plan notice subject", "#24597a", medium.Id));
			teacherId = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(TeacherUser))!.Id;
			var assignment = new AssignedSubject(Guid.NewGuid(), School, standard.Id, subject.Id, "PLAN", SubjectStatus.Published);
			assignment.SetPrimaryTeacher(teacherId);
			await InsertAsync(services, assignment);
			var now = DateTimeOffset.UtcNow;
			var chapter = await InsertAsync(services, new AssignedSubjectChapter(Guid.NewGuid(), assignment.Id, 1, "Lesson plan notice chapter", now, now.AddDays(5))
				{ TenantId = School, AssignedSubjectId = assignment.Id });
			chapterId = chapter.Id;
			var plan = new LessonPlan(planId, School, board.AcademicYearId, teacherId, standard.Id, divisionId, subject.Id,
				chapter.Id, now.UtcDateTime.Date, "Lesson plan review workflow", 30);
			plan.Submit();
			await InsertAsync(services, plan);
			var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School) { TemplateName = EventKey });
			var version = new CommunicationTemplateRevision(Guid.NewGuid(), School, template.Id, 1, EventKey, "AssignedTeacher",
				CommunicationChannelType.InApp, "en", "Lesson plan reviewed", "Open the school inbox.", 1);
			version.Publish(reviewer, now);
			await InsertAsync(services, version);
			var rule = new NotificationRule(Guid.NewGuid(), School, EventKey,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, standard.Id, divisionId));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], [template.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				configuration, services.GetRequiredService<AcademicNotificationCatalogue>().Get(EventKey)), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
		});

		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, reviewer.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		for (var i = 0; i < 2; i++)
		{
			using var response = await client.PostAsJsonAsync($"/api/academic/curriculum/lesson-plans/{planId}/request-changes",
				new ReviewRemarksDto { Comment = Comment }, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		}
		NotificationIntent intent = null!;
		await InSchoolAsync(async services =>
		{
			intent = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.EventKey == EventKey && x.SourceId == planId)).ShouldHaveSingleItem();
			var source = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
			source.CurrentRecipients.ShouldBe([new("AssignedTeacher", UserId: TeacherUser, EmployeeId: teacherId)]);
			source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == Comment);
		});

		var operation = $"/api/academic/notifications/operations/{intent.CampaignId}";
		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, reviewer.ToString());
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await viewer.GetAsync(operation);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var allowed = await client.GetAsync(operation);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());

		await InSchoolAsync(async services =>
		{
			var plans = services.GetRequiredService<IRepository<LessonPlan, Guid>>();
			var plan = await plans.GetAsync(planId);
			plan.UpdatePlan(chapterId, "Updated lesson plan scope notes", null, null, null, 30);
			await plans.UpdateAsync(plan, autoSave: true);
		});
		await InSchoolAsync(async services =>
			(await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None))
				.CurrentRecipients.ShouldBeEmpty());
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
public sealed class LessonPlanReviewNotificationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Lesson_plan_feedback_and_notice_commit_together()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new LessonPlanReviewNotificationDelivery_Tests(fixture).RunAsync();
	}
}
