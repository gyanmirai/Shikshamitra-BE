using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
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
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;
using ProgressRecord = ShikshaMitra.Academic.Syllabus.ChapterProgress;

namespace ShikshaMitra.Academic.Curriculum;

[Collection(ApiTestCollection.Name)]
public sealed class AcademicProgressDigestDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Teacher = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task Due_progress_is_queued_once_for_staff_in_the_saved_division() => RunAsync(milestone: false);

	[Fact]
	public Task First_milestone_is_queued_once_and_rechecks_current_progress() => RunAsync(milestone: true);

	internal async Task RunAsync(bool milestone)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var eventKey = milestone ? AcademicNotificationEventKeys.ProgressMilestoneReached : AcademicNotificationEventKeys.ProgressBehindSchedule;
		var label = milestone ? "Milestone digest" : "Progress digest";
		var config = Factory.Services.GetRequiredService<IConfiguration>();
		var key = $"Notifications:Academic:Tenants:{School:N}:ProgressReviewerUserId";
		var previous = config[key];
		var reviewer = Guid.NewGuid();
		config[key] = reviewer.ToString();
		var run = new DateTimeOffset(2026, 11, 20, 12, 30, 0, TimeSpan.Zero);
		var sourceId = Guid.NewGuid();
		var firstChapterId = Guid.NewGuid();
		var secondChapterId = Guid.NewGuid();
		var firstProgressId = Guid.NewGuid();
		Guid divisionId = default, classTeacher = default;
		NotificationRule rule = null!;
		try
		{
			await InSchoolAsync(async services =>
			{
				var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
				var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
				var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
					.CreateAsync(Factory.SeededMediumStageId, label + " grade", 12, null));
				var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 25));
				divisionId = division.Id;
				await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "B", 25));
				var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, label + " subject", "#24597a", medium.Id));
				var assigned = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Teacher))!;
				var staff = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
					x.TenantId == School && x.UserId.HasValue && x.UserId != Teacher &&
					x.EmployeeStatusType == EmployeeStatusType.Active && x.RoleId == assigned.RoleId))
					.OrderBy(x => x.Id).First();
				classTeacher = staff.UserId!.Value;
				var allocation = new AssignedSubject(sourceId, School, standard.Id, subject.Id, "DIGEST", SubjectStatus.Published);
				allocation.SetPrimaryTeacher(assigned.Id);
				await InsertAsync(services, allocation);
				await InsertAsync(services, new ClassTeacherRecord(Guid.NewGuid(), School, standard.Id, division.Id, staff.Id, medium.Id));
				await InsertAsync(services, new AssignedSubjectChapter(firstChapterId, sourceId, 1, "First saved target", run.AddDays(-5), run.AddHours(-2))
					{ TenantId = School, AssignedSubjectId = sourceId, PublishStatus = "Published" });
				await InsertAsync(services, new AssignedSubjectChapter(secondChapterId, sourceId, 2, "Second saved target", run.AddDays(-3), run.AddHours(-3))
					{ TenantId = School, AssignedSubjectId = sourceId, PublishStatus = "Published" });
				await InsertAsync(services, new AssignedSubjectChapter(Guid.NewGuid(), sourceId, 3, "Draft target", run.AddDays(-3), run.AddHours(-3))
					{ TenantId = School, AssignedSubjectId = sourceId, PublishStatus = "Draft" });
				if (milestone)
				{
					await RecordMilestoneAsync(firstChapterId, firstProgressId);
					await RecordMilestoneAsync(secondChapterId, Guid.NewGuid());
				}

				async Task RecordMilestoneAsync(Guid chapterId, Guid progressId)
				{
					var record = new ProgressRecord(progressId, chapterId, division.Id);
					record.UpdateProgress(75);
					await InsertAsync(services, record);
					var log = new ChapterProgressLog(Guid.NewGuid(), chapterId, division.Id, 75, "Synthetic private audit");
					typeof(ChapterProgressLog).GetProperty(nameof(ChapterProgressLog.CreationTime))!.SetValue(log, run.AddHours(-2).UtcDateTime);
					await InsertAsync(services, log);
				}

				var reviewerRole = await services.GetRequiredService<IIdentityRoleRepository>().FindByNormalizedNameAsync("ADMIN");
				reviewerRole.ShouldNotBeNull();
				var reviewerAccount = new IdentityUser(reviewer, $"progress-review-{reviewer:N}"[..31], "digest-reviewer@example.test", School);
				reviewerAccount.AddRole(reviewerRole.Id);
				await InsertAsync(services, reviewerAccount);
				await InsertAsync(services, new UserScopeAssignment(Guid.NewGuid(), School, userId: reviewer, divisionId: divisionId));

				var kinds = milestone ? new[] { "AssignedTeacher", "ClassTeacher" } : new[] { "AssignedTeacher", "ClassTeacher", "ConfiguredReviewer" };
				var headers = new Guid[kinds.Length];
				for (var i = 0; i < kinds.Length; i++)
				{
					var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School) { TemplateName = label + " " + kinds[i] });
					headers[i] = header.Id;
					var version = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1, eventKey, kinds[i],
						CommunicationChannelType.InApp, "en", "Chapter progress", "{{chapter_count}} chapters to review", 1);
					version.Publish(reviewer, run.AddDays(-2));
					await InsertAsync(services, version);
				}
				rule = new NotificationRule(Guid.NewGuid(), School, eventKey,
					NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id));
				var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, kinds,
					[CommunicationChannelType.InApp], headers, NotificationRuleTimingConfiguration.Default with
					{
						QuietHoursEnabled = false,
						ProgressMilestonePercentage = milestone ? 65 : null
					}, "en");
				rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(settings,
					services.GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey)), rule.ConcurrencyStamp);
				await InsertAsync(services, rule);
			});

			await InSchoolAsync(async services =>
			{
				var source = services.GetServices<INotificationDueSource>().Single(x => x.ScheduledEventKeys.Contains(eventKey));
				var page = await source.ReadPageAsync(new(School, run, run, null, 100), CancellationToken.None);
				var candidate = page.Candidates.ShouldHaveSingleItem();
				candidate.Scope.DivisionId.ShouldBe(divisionId);
				var input = new NotificationEventInput(eventKey, source.SourceType, sourceId, candidate.SourceRevision, run, [],
					DueAt: candidate.OccurrenceAt, ExpiresAt: candidate.ExpiresAt,
					DueOccurrence: new(candidate.Scope, candidate.OccurrenceAt, rule.Id, rule.Revision));
				var writer = services.GetRequiredService<INotificationIntentWriter>();
				(await writer.WriteWithOutcomeAsync(input, CancellationToken.None)).Created.ShouldBeTrue();
				(await writer.WriteWithOutcomeAsync(input, CancellationToken.None)).Created.ShouldBeFalse();
			});

			Guid operationId = default;
			await InSchoolAsync(async services =>
			{
				var intent = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
					.GetListAsync(x => x.TenantId == School && x.EventKey == eventKey && x.SourceId == sourceId)).ShouldHaveSingleItem();
				operationId = intent.CampaignId!.Value;
				var current = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, run, CancellationToken.None);
				var expected = milestone ? new[] { ("AssignedTeacher", Teacher), ("ClassTeacher", classTeacher) }
					: new[] { ("AssignedTeacher", Teacher), ("ClassTeacher", classTeacher), ("ConfiguredReviewer", reviewer) };
				current.CurrentRecipients.Select(x => (x.Kind, x.UserId!.Value)).ShouldBe(expected, ignoreOrder: true);
				current.Snapshot!.Variables["chapter_count"].Value.ShouldBe("2");
				if (milestone)
				{
					current.Snapshot.Variables["milestone_percentage"].Value.ShouldBe("65");
					current.Snapshot.Variables.Values.ShouldNotContain(x => x.Value == "Synthetic private audit");
					var progressRepository = services.GetRequiredService<IRepository<ProgressRecord, Guid>>();
					var progress = await progressRepository.GetAsync(firstProgressId);
					progress.UpdateProgress(45);
					await progressRepository.UpdateAsync(progress, autoSave: true);
				}
				else
				{
					var chapterRepository = services.GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>();
					var chapter = await chapterRepository.GetAsync(firstChapterId);
					chapter.UpdateDates(run.AddDays(-2), run.AddDays(2));
					await chapterRepository.UpdateAsync(chapter, autoSave: true);
				}
				(await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, run, CancellationToken.None))
					.Snapshot!.Variables["chapter_count"].Value.ShouldBe("1");
			});

			var operation = $"/api/academic/notifications/operations/{operationId}";
			using var viewOnly = Factory.CreateClient();
			viewOnly.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
				ShikshaMitraSubmodulePermissions.Academics.Notification.View);
			using var denied = await viewOnly.GetAsync(operation);
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
			using var response = await Client.GetAsync(operation);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		}
		finally { config[key] = previous; }
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
public sealed class AcademicProgressDigestPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Progress_digest_revalidates_targets_and_recipients_on_Postgres()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		var workflow = new AcademicProgressDigestDelivery_Tests(fixture);
		await workflow.RunAsync(milestone: false);
		await workflow.RunAsync(milestone: true);
	}
}
