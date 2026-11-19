using System;
using System.Linq;
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
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

[Collection(ApiTestCollection.Name)]
public sealed class SubjectAllocationNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);
	private const string Published = AcademicNotificationEventKeys.SubjectAllocated;
	private const string Withdrawn = AcademicNotificationEventKeys.SubjectWithdrawn;

	[Fact]
	public Task Subject_status_and_archive_routes_queue_scoped_notices() => RunWorkflowAsync();

	internal async Task RunWorkflowAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var assignment = Guid.NewGuid();
		var division = Guid.Empty;
		var teacher = Guid.Empty;
		var learner = Guid.NewGuid();
		await InSchoolAsync(async services =>
		{
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(Factory.SeededMediumStageId, "Subject notices class", 11, null));
			division = (await InsertAsync(services, await services.GetRequiredService<DivisionManager>()
				.CreateAsync(standard.Id, "A", 25))).Id;
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
			var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Subject notices example", "#24597a", medium.Id));
			teacher = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Actor))!.Id;
			var row = new AssignedSubject(assignment, School, standard.Id, subject.Id, "NOTICES");
			row.SetPrimaryTeacher(teacher);
			await InsertAsync(services, row);
			await InsertAsync(services, new Student(learner, School, "Synthetic", null, "Learner", board.AcademicYearId,
				board.Id, medium.Id, Factory.SeededMediumStageId, standard.Id, division, status: StudentLifeCycleStatusEnum.Enrolled));
			await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), School, learner, board.AcademicYearId, standard.Id, division));
			foreach (var (eventKey, kind) in new[] { (Published, "AssignedTeacher"), (Withdrawn, "Guardian") })
			{
				var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School) { TemplateName = eventKey });
				var version = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1,
					eventKey, kind, CommunicationChannelType.InApp, "en", "Subject allocation", "Open your school inbox.", 1);
				version.Publish(Actor, DateTimeOffset.UtcNow);
				await InsertAsync(services, version);
				var rule = new NotificationRule(Guid.NewGuid(), School, eventKey, NotificationRuleScope.Create());
				var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
					kind == "Guardian" ? [] : [kind], [CommunicationChannelType.InApp], [header.Id],
					NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
				rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
					settings, services.GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey)), rule.ConcurrencyStamp);
				await InsertAsync(services, rule);
			}
		});

		var route = $"/api/academics/curriculum/subject-assignments/{assignment}";
		for (var attempt = 0; attempt < 2; attempt++)
		{
			using var result = await Client.PatchAsJsonAsync($"{route}/status", new { status = (int)SubjectStatus.Published }, JsonOptions);
			result.StatusCode.ShouldBe(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
		}
		var publication = await ReadAsync(Published, new("AssignedTeacher", UserId: Actor, EmployeeId: teacher));
		for (var attempt = 0; attempt < 2; attempt++)
		{
			using var archived = await Client.PostAsync($"{route}/archive", null);
			archived.StatusCode.ShouldBe(HttpStatusCode.OK, await archived.Content.ReadAsStringAsync());
		}
		var withdrawal = await ReadAsync(Withdrawn, new("Student", StudentId: learner), division);
		await InSchoolAsync(async services =>
			(await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(publication, DateTimeOffset.UtcNow, CancellationToken.None))
				.CurrentRecipients.ShouldBeEmpty());

		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await viewer.GetAsync($"/api/academic/notifications/operations/{withdrawal.CampaignId}");
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var allowed = await Client.GetAsync($"/api/academic/notifications/operations/{withdrawal.CampaignId}");
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());

		async Task<NotificationIntent> ReadAsync(string eventKey, NotificationRecipientRefDto expected, Guid? divisionId = null)
		{
			NotificationIntent intent = null!;
			await InSchoolAsync(async services =>
			{
				intent = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
					x.TenantId == School && x.SourceId == assignment && x.EventKey == eventKey)).ShouldHaveSingleItem();
				var outcome = await services.GetRequiredService<NotificationIntentSourceReader>()
					.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
				outcome.CurrentRecipients.ShouldBe([expected]);
				outcome.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
			});
			return intent;
		}
	}

	private static Task<TEntity> InsertAsync<TEntity>(IServiceProvider services, TEntity row) where TEntity : class, IEntity<Guid> =>
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
public sealed class SubjectAllocationNotificationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Subject_notices_follow_persisted_publication_and_withdrawal()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new SubjectAllocationNotificationDelivery_Tests(fixture).RunWorkflowAsync();
	}
}
