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
using ShikshaMitra.Common;
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

	[Fact]
	public Task Division_teacher_save_queues_only_the_changed_division() => RunDivisionWorkflowAsync(concurrent: false);

	internal async Task RunDivisionWorkflowAsync(bool concurrent)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		const string changed = AcademicNotificationEventKeys.SubjectTeacherChanged;
		var classId = Guid.Empty;
		var divisionId = Guid.Empty;
		var subjectId = Guid.Empty;
		var oldTeacher = Guid.Empty;
		var newTeacher = Guid.Empty;
		var newUser = Guid.Empty;
		var allocationId = Guid.NewGuid();
		var siblingAllocationId = Guid.NewGuid();
		await InSchoolAsync(async services =>
		{
			classId = (await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(Factory.SeededMediumStageId, "Division teacher notices class", 12, null))).Id;
			var divisions = services.GetRequiredService<DivisionManager>();
			divisionId = (await InsertAsync(services, await divisions.CreateAsync(classId, "A", 25))).Id;
			var sibling = await InsertAsync(services, await divisions.CreateAsync(classId, "B", 25));
			subjectId = (await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Division teacher notices subject",
				"#24597a", Factory.SeededMediumId))).Id;
			var actor = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Actor))!;
			oldTeacher = actor.Id;
			var teacher = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
				x.TenantId == School && x.UserId.HasValue && x.UserId != Actor && x.RoleId == actor.RoleId &&
				x.EmployeeStatusType == EmployeeStatusType.Active && x.EmployeeGroup == EmployeeGroupType.Teaching)).First();
			newTeacher = teacher.Id;
			newUser = teacher.UserId!.Value;
			var assignment = new AssignedSubject(Guid.NewGuid(), School, classId, subjectId, "DIVNOTICE");
			assignment.SetPrimaryTeacher(oldTeacher);
			assignment.ChangeStatus(SubjectStatus.Published);
			await InsertAsync(services, assignment);
			foreach (var (id, division) in new[] { (allocationId, divisionId), (siblingAllocationId, sibling.Id) })
			{
				var row = new TeacherSubjectAllocation(id, classId, division, subjectId);
				row.Teachers.Add(new(id, oldTeacher));
				await InsertAsync(services, row);
			}

			var kinds = new[] { "AssignedTeacher", "PreviousTeacher" };
			var headers = new Guid[kinds.Length];
			for (var i = 0; i < kinds.Length; i++)
			{
				var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School) { TemplateName = changed + " " + kinds[i] });
				headers[i] = header.Id;
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1,
					changed, kinds[i], CommunicationChannelType.InApp, "en", "Subject allocation changed", "Open your school inbox.", 1);
				revision.Publish(Actor, DateTimeOffset.UtcNow);
				await InsertAsync(services, revision);
			}

			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
			var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var rule = new NotificationRule(Guid.NewGuid(), School, changed,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, classId, divisionId));
			var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, kinds,
				[CommunicationChannelType.InApp], headers, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				settings, services.GetRequiredService<AcademicNotificationCatalogue>().Get(changed)), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
		});

		async Task SaveAsync(Guid[]? teacherIds, HttpStatusCode expected = HttpStatusCode.OK)
		{
			using var response = await Client.PutAsJsonAsync("/api/class-management/teacher-assignments", new
			{
				classId, academicMediumId = Factory.SeededMediumId, assignments = new[] { new { divisionId, subjectId, teacherIds } }
			}, JsonOptions);
			response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
		}

		if (concurrent) await Task.WhenAll(SaveAsync([newTeacher]), SaveAsync([newTeacher]));
		else
		{
			await SaveAsync([newTeacher]);
			await SaveAsync([newTeacher]);
		}
		await SaveAsync(null, HttpStatusCode.BadRequest);
		NotificationIntent replacement = null!;
		await InSchoolAsync(async services =>
		{
			replacement = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.EventKey == changed && (x.SourceId == allocationId || x.SourceId == siblingAllocationId))).ShouldHaveSingleItem();
			var source = await services.GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(replacement, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
			source.CurrentRecipients.ToHashSet().SetEquals(
			[
				new("AssignedTeacher", UserId: newUser, EmployeeId: newTeacher),
				new("PreviousTeacher", UserId: Actor, EmployeeId: oldTeacher)
			]).ShouldBeTrue();
			source.Snapshot!.RecipientContent!.ShouldHaveSingleItem().Variables.Keys.ShouldBe(["school_name"]);
			var sibling = await services.GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>()
				.GetAsync(siblingAllocationId, includeDetails: true);
			sibling.Teachers.Where(x => !x.IsDeleted).Select(x => x.TeacherId).ShouldBe([oldTeacher]);
		});

		var operation = $"/api/academic/notifications/operations/{replacement.CampaignId}";
		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await viewer.GetAsync(operation);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var allowed = await Client.GetAsync(operation);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());

		await SaveAsync([]);
		await SaveAsync([]);
		await InSchoolAsync(async services =>
		{
			var removal = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.EventKey == changed && x.SourceId == allocationId && x.Id != replacement.Id)).ShouldHaveSingleItem();
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(removal, DateTimeOffset.UtcNow, CancellationToken.None))
				.CurrentRecipients.ShouldBe([new("PreviousTeacher", UserId: newUser, EmployeeId: newTeacher)]);
			(await reader.LoadAsync(replacement, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		});
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

[Collection(ApiTestCollection.Name)]
public sealed class DivisionSubjectNotificationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Concurrent_replacements_queue_one_notice_for_the_saved_division()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new SubjectAllocationNotificationDelivery_Tests(fixture).RunDivisionWorkflowAsync(concurrent: true);
	}
}
