using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Performance;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
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

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

[Collection(ApiTestCollection.Name)]
public sealed class ElectiveCorrectionNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Event = AcademicNotificationEventKeys.ElectiveLockedSelectionCorrected;
	private const string Route = "/api/class-management/student-elective-assignments/corrections";
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task Confirmed_elective_corrections_queue_only_the_saved_learner_and_assigned_staff() => RunAsync();

	internal async Task RunAsync()
	{
		var adminId = Guid.NewGuid();
		var student = Guid.NewGuid();
		var selectionId = Guid.NewGuid();
		var request = new ConfirmStudentElectiveCorrectionInput { AcademicMediumId = Factory.SeededMediumId, StudentId = student };
		Guid oldSubjectId = default;
		NotificationRecipientRefDto[] expected = [];
		await InSchoolAsync(async services =>
		{
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
			var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(Factory.SeededMediumStageId, "Elective notice class", 12, null));
			var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>()
				.CreateAsync(standard.Id, "A", 25));
			request.ClassId = standard.Id;
			request.DivisionId = division.Id;
			var oldSubject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Old elective notice subject", "#24597a", medium.Id));
			var newSubject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "New elective notice subject", "#34597a", medium.Id));
			var group = new ElectiveGroup(Guid.NewGuid(), School, "Elective notice group", "#24597a", medium.Id);
			group.AddSubject(oldSubject.Id);
			group.AddSubject(newSubject.Id);
			await InsertAsync(services, group);
			request.ElectiveGroupId = group.Id;
			request.SelectedSubjectId = newSubject.Id;
			oldSubjectId = oldSubject.Id;
			var role = await services.GetRequiredService<IIdentityRoleRepository>().FindByNormalizedNameAsync("ADMIN");
			role.ShouldNotBeNull();
			var admin = new IdentityUser(adminId, $"elective-notice-{adminId:N}"[..31], "elective-notice-admin@example.test", School);
			admin.AddRole(role.Id);
			await InsertAsync(services, admin);
			await InsertAsync(services, new UserScopeAssignment(Guid.NewGuid(), School, userId: adminId));
			var teacherRole = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Actor))!.RoleId;
			var teachers = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
				x.TenantId == School && x.UserId.HasValue && x.RoleId == teacherRole &&
				x.EmployeeStatusType == EmployeeStatusType.Active && x.EmployeeGroup == EmployeeGroupType.Teaching)).Take(3).ToArray();
			teachers.Length.ShouldBe(3);
			var first = new AssignedSubject(Guid.NewGuid(), School, standard.Id, oldSubject.Id, "OLD", SubjectStatus.Published, SubjectType.Elective, group.Id);
			first.SetPrimaryTeacher(teachers[0].Id);
			var second = new AssignedSubject(Guid.NewGuid(), School, standard.Id, newSubject.Id, "NEW", SubjectStatus.Published, SubjectType.Elective, group.Id);
			second.SetPrimaryTeacher(teachers[1].Id);
			await InsertAsync(services, first);
			await InsertAsync(services, second);
			await InsertAsync(services, new ClassTeacherRecord(Guid.NewGuid(), School, standard.Id, division.Id, teachers[2].Id,
				medium.Id, educationMediumStageId: Factory.SeededMediumStageId));
			await InsertAsync(services, new Student(student, School, "Synthetic", null, "Learner", board.AcademicYearId,
				board.Id, medium.Id, Factory.SeededMediumStageId, standard.Id, division.Id, status: StudentLifeCycleStatusEnum.Enrolled));
			await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), School, student, board.AcademicYearId, standard.Id, division.Id));
			var selection = new StudentElectiveSelection(selectionId, School, student, group.Id, oldSubject.Id, medium.Id);
			selection.LockSelection();
			await InsertAsync(services, selection);
			expected =
			[
				new("Student", StudentId: student), new("AssignedTeacher", UserId: teachers[1].UserId, EmployeeId: teachers[1].Id),
				new("PreviousTeacher", UserId: teachers[0].UserId, EmployeeId: teachers[0].Id),
				new("ClassTeacher", UserId: teachers[2].UserId, EmployeeId: teachers[2].Id)
			];
			string[] staff = ["AssignedTeacher", "PreviousTeacher", "ClassTeacher"];
			var kinds = staff.Append("Guardian").ToArray();
			var headers = new Guid[kinds.Length];
			for (var i = 0; i < kinds.Length; i++)
			{
				var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
					{ TemplateName = "Elective notice " + kinds[i] });
				headers[i] = header.Id;
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1, Event, kinds[i],
					CommunicationChannelType.InApp, "en", "Elective allocation changed", "Open the school inbox.", 1);
				revision.Publish(adminId, DateTimeOffset.UtcNow);
				await InsertAsync(services, revision);
			}

			var rule = new NotificationRule(Guid.NewGuid(), School, Event,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id));
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				staff, [CommunicationChannelType.InApp], headers, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				configuration, services.GetRequiredService<AcademicNotificationCatalogue>().Get(Event)), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
		});

		using var adminClient = Factory.CreateClient();
		adminClient.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, adminId.ToString());
		adminClient.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");

		async Task PreviewAsync()
		{
			using var response = await adminClient.PostAsJsonAsync(Route + "/preview", request, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			request.ReviewToken = (await response.Content.ReadFromJsonAsync<StudentElectiveCorrectionReviewDto>(JsonOptions))!.ReviewToken;
		}

		async Task ConfirmAsync()
		{
			using var response = await adminClient.PostAsJsonAsync(Route + "/confirm", request, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
		}

		await PreviewAsync();
		await InSchoolAsync(async services =>
			(await services.GetRequiredService<IRepository<StudentElectiveCorrection, Guid>>().GetListAsync(x => x.StudentId == student)).ShouldBeEmpty());
		await ConfirmAsync();
		NotificationIntent original = null!;
		await InSchoolAsync(async services =>
		{
			var audit = (await services.GetRequiredService<IRepository<StudentElectiveCorrection, Guid>>()
				.GetListAsync(x => x.StudentId == student)).ShouldHaveSingleItem();
			original = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.SourceId == audit.Id && x.EventKey == Event)).ShouldHaveSingleItem();
			var source = await services.GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(original, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.ToHashSet().SetEquals(expected).ShouldBeTrue();
			source.Runtime.SourceScope.DivisionId.ShouldBe(request.DivisionId);
			source.Snapshot!.RecipientContent!.ShouldHaveSingleItem().Variables.Keys.ShouldBe(["school_name"]);
		});

		var operation = $"/api/academic/notifications/operations/{original.CampaignId}";
		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, adminId.ToString());
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await viewer.GetAsync(operation);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var allowed = await adminClient.GetAsync(operation);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());

		request.SelectedSubjectId = oldSubjectId;
		await PreviewAsync();
		await ConfirmAsync();
		await InSchoolAsync(async services =>
		{
			var saved = await services.GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().GetAsync(selectionId);
			saved.SelectedSubjectId.ShouldBe(oldSubjectId);
			saved.IsLocked.ShouldBeTrue();
			(await services.GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(original, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		});
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
public sealed class ElectiveCorrectionNotificationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Corrections_and_atomic_notice_capture_persist_in_postgresql()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new ElectiveCorrectionNotificationDelivery_Tests(fixture).RunAsync();
	}
}
