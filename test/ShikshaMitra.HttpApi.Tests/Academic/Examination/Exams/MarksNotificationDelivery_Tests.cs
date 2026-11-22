using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
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

namespace ShikshaMitra.Academic.Examination.Exams;

[Collection(ApiTestCollection.Name)]
public sealed class MarksNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Submitted = AcademicNotificationEventKeys.MarksSubmitted;
	private const string Changes = AcademicNotificationEventKeys.MarksChangesRequested;
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task Marks_queue_for_current_staff_and_require_record_marks_history_access() => RunWorkflowAsync();

	internal async Task RunWorkflowAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var examId = Guid.NewGuid();
		var learnerId = Guid.NewGuid();
		var reviewerId = Guid.NewGuid();
		var subjectId = Guid.Empty;
		var divisionId = Guid.Empty;
		var registrationKey = $"Notifications:Academic:Tenants:{School:N}:MarksReviewerUserId";
		var config = Factory.Services.GetRequiredService<IConfiguration>();
		var previous = config[registrationKey];
		config[registrationKey] = reviewerId.ToString();
		using var reviewerClient = Factory.CreateClient();
		reviewerClient.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, reviewerId.ToString());

		try
		{
			await InSchoolAsync(async services =>
			{
				var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
					.CreateAsync(Factory.SeededMediumStageId, "Marks notice class", 10, null));
				var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 25));
				divisionId = division.Id;
				var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
				var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
				var type = await InsertAsync(services, new ExamType(Guid.NewGuid(), School, "Marks notice exam", null, medium.Id));
				var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Marks notice subject", "#24597a", medium.Id));
				subjectId = subject.Id;
				var teacher = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Actor))!;
				var assigned = new AssignedSubject(Guid.NewGuid(), School, standard.Id, subject.Id, "NOTICE", SubjectStatus.Published);
				assigned.SetPrimaryTeacher(teacher.Id);
				await InsertAsync(services, assigned);
				await InsertAsync(services, new Student(learnerId, School, "Synthetic", null, "Learner", board.AcademicYearId,
					board.Id, medium.Id, Factory.SeededMediumStageId, standard.Id, division.Id, status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(services, new IdentityUser(reviewerId, $"marks-reviewer-{reviewerId:N}"[..31], "marks-reviewer@example.test", School));
				await InsertAsync(services, new UserScopeAssignment(Guid.NewGuid(), School, userId: reviewerId));
				var exam = new Exam(examId, type.Id, "Test marks workflow", standard.Id, School);
				exam.AddDivision(Guid.NewGuid(), division.Id);
				exam.AddSubject(new(Guid.NewGuid(), examId, subject.Id, null, null, null, 100, 35));
				exam.ChangeStatus(ExamStatus.Published);
				await InsertAsync(services, exam);
				var ruleScope = NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id);
				foreach (var (key, kind) in new[] { (Submitted, "ConfiguredReviewer"), (Changes, "AssignedTeacher") })
				{
					var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School) { TemplateName = key });
					var revision = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1, key, kind,
						CommunicationChannelType.InApp, "en", "Marks workflow", "Open Record Marks.", 1);
					revision.Publish(Actor, DateTimeOffset.UtcNow);
					await InsertAsync(services, revision);
					var rule = new NotificationRule(Guid.NewGuid(), School, key, ruleScope);
					var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [kind],
						[CommunicationChannelType.InApp], [header.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
					rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
						settings, services.GetRequiredService<AcademicNotificationCatalogue>().Get(key)), rule.ConcurrencyStamp);
					await InsertAsync(services, rule);
				}
			});

			var route = $"/api/academics/exams/{examId}/marks";
			var scopeQuery = $"divisionId={divisionId}&subjectId={subjectId}";
			using var saved = await Client.PostAsJsonAsync($"{route}/bulk-save", new
			{
				divisionId, subjectId, rows = new[] { new { studentId = learnerId, attendanceStatus = 0, obtainedMarks = 81, feedback = "Private note" } }
			}, JsonOptions);
			saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
			await InSchoolAsync(async services =>
				(await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.EventKey == Submitted || x.EventKey == Changes)).ShouldBeEmpty());

			await PostAsync(Client, $"{route}/submit?{scopeQuery}");
			var submitted = await ReadNoticeAsync(Submitted, reviewerId);
			using var readerWithOnlyHistory = Factory.CreateClient();
			readerWithOnlyHistory.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
				ShikshaMitraSubmodulePermissions.Academics.Notification.View);
			using var denied = await readerWithOnlyHistory.GetAsync($"/api/academic/notifications/operations/{submitted.CampaignId}");
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			using var allowed = await reviewerClient.GetAsync($"/api/academic/notifications/operations/{submitted.CampaignId}");
			allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());

			await InSchoolAsync(async services =>
			{
				var reviewer = await services.GetRequiredService<IRepository<IdentityUser, Guid>>().GetAsync(reviewerId);
				reviewer.SetIsActive(false);
				await services.GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(reviewer, autoSave: true);
			});
			await CheckCurrentAsync(submitted, shouldHaveRecipients: false);
			await InSchoolAsync(async services =>
			{
				var reviewer = await services.GetRequiredService<IRepository<IdentityUser, Guid>>().GetAsync(reviewerId);
				reviewer.SetIsActive(true);
				await services.GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(reviewer, autoSave: true);
			});
			using var correction = await reviewerClient.PostAsJsonAsync($"{route}/request-changes",
				new { divisionId, subjectId, reviewerNote = "Protected correction note" }, JsonOptions);
			correction.StatusCode.ShouldBe(HttpStatusCode.OK, await correction.Content.ReadAsStringAsync());
			await ReadNoticeAsync(Changes, Actor);
			await CheckCurrentAsync(submitted, shouldHaveRecipients: false);
		}
		finally { config[registrationKey] = previous; }

		static async Task PostAsync(HttpClient client, string path)
		{
			using var response = await client.PostAsync(path, null);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		}

		async Task<NotificationIntent> ReadNoticeAsync(string eventKey, Guid expectedUser)
		{
			NotificationIntent intent = null!;
			await InSchoolAsync(async services =>
			{
				var state = (await services.GetRequiredService<IRepository<ExamMarksWorkflowState, Guid>>().GetListAsync(x =>
					x.TenantId == School && x.ExamId == examId && x.SubjectId == subjectId && x.DivisionId == divisionId)).ShouldHaveSingleItem();
				intent = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == School && x.SourceId == state.Id && x.EventKey == eventKey)).ShouldHaveSingleItem();
				var result = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
				result.CurrentRecipients.ShouldHaveSingleItem().UserId.ShouldBe(expectedUser);
				result.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
				result.Snapshot!.RelativeLink.ShouldBe("/academic/gradeEntry");
				result.Snapshot.Variables.Keys.Order().ShouldBe(new[] { "exam_name", "school_name", "subject_name" });
			});
			return intent;
		}

		Task CheckCurrentAsync(NotificationIntent intent, bool shouldHaveRecipients) => InSchoolAsync(async services =>
		{
			var result = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
			result.CurrentRecipients.Any().ShouldBe(shouldHaveRecipients);
		});
	}

	private static Task<TEntity> InsertAsync<TEntity>(IServiceProvider services, TEntity row)
		where TEntity : class, IEntity<Guid> =>
		services.GetRequiredService<IRepository<TEntity, Guid>>().InsertAsync(row, autoSave: true);

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(School, "Synthetic school");
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}
}

[Collection(ApiTestCollection.Name)]
public sealed class MarksNotificationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Marks_review_notices_reload_the_persisted_workflow_on_postgresql()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		var scenario = new MarksNotificationDelivery_Tests(fixture);
		await scenario.RunWorkflowAsync();
	}
}
