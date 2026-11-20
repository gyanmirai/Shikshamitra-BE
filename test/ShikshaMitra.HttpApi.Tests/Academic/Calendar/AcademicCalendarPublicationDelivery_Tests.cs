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

namespace ShikshaMitra.Academic.Calendar;

[Collection(ApiTestCollection.Name)]
public sealed class AcademicCalendarPublicationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string EventKey = AcademicNotificationEventKeys.CalendarPublished;
	private const string Path = "/api/academic/calendars";
	private const string PrivateDescription = "Internal bell scheduling notes";
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Teacher = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task Published_calendars_target_the_saved_division_and_archive_invalidates_the_notice() => RunAsync();

	internal async Task RunAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var learner = Guid.NewGuid();
		var sibling = Guid.NewGuid();
		Guid schoolYear = default, divisionId = default, teacherId = default;
		await InSchoolAsync(async services =>
		{
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
			var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			schoolYear = board.AcademicYearId;
			var standard = await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(Factory.SeededMediumStageId, "Calendar notice class", 12, null));
			var division = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 25));
			var other = await InsertAsync(services, await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "B", 25));
			divisionId = division.Id;
			var subject = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Calendar notice subject", "#24597a", medium.Id));
			teacherId = (await services.GetRequiredService<IEmployeeRepository>().GetEmployeeByUserIdAsync(Teacher))!.Id;
			var allocation = new AssignedSubject(Guid.NewGuid(), School, standard.Id, subject.Id, "CAL", SubjectStatus.Published);
			allocation.SetPrimaryTeacher(teacherId);
			await InsertAsync(services, allocation);
			foreach (var (id, section) in new[] { (learner, division.Id), (sibling, other.Id) })
			{
				await InsertAsync(services, new Student(id, School, "Synthetic", null, "Learner", board.AcademicYearId,
					board.Id, medium.Id, Factory.SeededMediumStageId, standard.Id, section, status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), School, id, schoolYear, standard.Id, section));
			}

			var kinds = new[] { "AssignedTeacher", "Guardian" };
			var headers = new Guid[kinds.Length];
			for (var i = 0; i < kinds.Length; i++)
			{
				var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
					{ TemplateName = "Calendar publication " + kinds[i] });
				headers[i] = header.Id;
				var version = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1, EventKey, kinds[i],
					CommunicationChannelType.InApp, "en", "Calendar publication", "{{event_title}} is available.", 1);
				version.Publish(Teacher, DateTimeOffset.UtcNow);
				await InsertAsync(services, version);
			}
			var rule = new NotificationRule(Guid.NewGuid(), School, EventKey,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				["AssignedTeacher"], [CommunicationChannelType.InApp], headers,
				NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config, services.GetRequiredService<AcademicNotificationCatalogue>().Get(EventKey)), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
		});

		var start = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
		using var created = await Client.PostAsJsonAsync(Path, new CreateCalendarDto
		{
			AcademicMediumId = Factory.SeededMediumId, AcademicYearId = schoolYear, Name = "Synthetic term calendar",
			StartDate = start, EndDate = start.AddDays(30), Description = PrivateDescription,
		}, JsonOptions);
		created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		var draft = (await created.Content.ReadFromJsonAsync<AcademicCalendarDto>(JsonOptions))!;
		draft.Status.ShouldBe(CalendarStatus.Draft);
		var calendarId = draft.Id;
		await InSchoolAsync(async services => (await ReadNoticesAsync(services)).ShouldBeEmpty());

		for (var i = 0; i < 2; i++)
		{
			using var published = await Client.PostAsync($"{Path}/{calendarId}/publish", null);
			published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
		}
		NotificationIntent notice = null!;
		await InSchoolAsync(async services =>
		{
			notice = (await ReadNoticesAsync(services)).ShouldHaveSingleItem();
			var current = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(notice, DateTimeOffset.UtcNow, CancellationToken.None);
			current.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
			current.Runtime.Configuration.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
			current.Runtime.Configuration.IncludeStudentAccount.ShouldBeFalse();
			current.CurrentRecipients.ShouldBe([new("AssignedTeacher", UserId: Teacher, EmployeeId: teacherId),
				new("Student", StudentId: learner)], ignoreOrder: true);
			current.Snapshot!.Variables["event_title"].Value.ShouldBe("Synthetic term calendar");
			current.Snapshot.Variables.Values.ShouldNotContain(x => x.Value == PrivateDescription);
		});

		var operation = $"/api/academic/notifications/operations/{notice.CampaignId}";
		using var withoutCalendarView = Factory.CreateClient();
		withoutCalendarView.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await withoutCalendarView.GetAsync(operation);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());
		using var allowed = await Client.GetAsync(operation);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());

		using var archived = await Client.PostAsync($"{Path}/{calendarId}/archive", null);
		archived.StatusCode.ShouldBe(HttpStatusCode.OK, await archived.Content.ReadAsStringAsync());
		await InSchoolAsync(async services =>
			(await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(notice, DateTimeOffset.UtcNow, CancellationToken.None))
				.CurrentRecipients.ShouldBeEmpty());

		Task<System.Collections.Generic.List<NotificationIntent>> ReadNoticesAsync(IServiceProvider services) =>
			services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.TenantId == School && x.EventKey == EventKey && x.SourceId == calendarId);
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
public sealed class AcademicCalendarPublicationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Calendar_publication_scope_and_history_work_on_Postgres()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new AcademicCalendarPublicationDelivery_Tests(fixture).RunAsync();
	}
}
