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
public sealed class SyllabusNotificationDelivery_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Published = AcademicNotificationEventKeys.SyllabusPublished;
	private const string ChapterPath = "/api/academic/curriculum/chapters";
	private static readonly Guid School = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);

	[Fact]
	public Task A_published_chapter_targets_the_matching_elective_and_honours_parent_visibility() => RunAsync();

	internal async Task RunAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var assignmentId = Guid.NewGuid();
		var learner = Guid.NewGuid();
		Guid classId = default, divisionId = default, subjectId = default;
		await InSchoolAsync(async services =>
		{
			var medium = await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(Factory.SeededMediumId);
			var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			classId = (await InsertAsync(services, await services.GetRequiredService<StandardManager>()
				.CreateAsync(Factory.SeededMediumStageId, "Syllabus notice class", 12, null))).Id;
			divisionId = (await InsertAsync(services, await services.GetRequiredService<DivisionManager>()
				.CreateAsync(classId, "A", 25))).Id;
			subjectId = (await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Syllabus elective", "#24597a", medium.Id))).Id;
			var other = await InsertAsync(services, new Subject(Guid.NewGuid(), School, "Other syllabus elective", "#64597a", medium.Id));
			var group = new ElectiveGroup(Guid.NewGuid(), School, "Syllabus options", "#24597a", medium.Id);
			group.AddSubject(subjectId);
			group.AddSubject(other.Id);
			await InsertAsync(services, group);
			var allocation = new AssignedSubject(assignmentId, School, classId, subjectId, "SYLLABUS")
				{ SubjectType = SubjectType.Elective, ElectiveGroupId = group.Id };
			allocation.ChangeStatus(SubjectStatus.Published);
			await InsertAsync(services, allocation);
			foreach (var (id, choice, locked) in new[] { (learner, subjectId, true), (Guid.NewGuid(), other.Id, true), (Guid.NewGuid(), subjectId, false) })
			{
				await InsertAsync(services, new Student(id, School, "Synthetic", null, "Learner", board.AcademicYearId,
					board.Id, medium.Id, Factory.SeededMediumStageId, classId, divisionId, status: StudentLifeCycleStatusEnum.Enrolled));
				await InsertAsync(services, StudentEnrollment.Create(Guid.NewGuid(), School, id, board.AcademicYearId, classId, divisionId));
				var selection = new StudentElectiveSelection(Guid.NewGuid(), School, id, group.Id, choice, medium.Id);
				if (locked) selection.LockSelection();
				await InsertAsync(services, selection);
			}

			var headers = new Guid[2];
			var recipients = new[] { "Guardian", "StudentAccount" };
			for (var i = 0; i < recipients.Length; i++)
			{
				var header = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), School)
					{ TemplateName = Published + " " + recipients[i] });
				var template = new CommunicationTemplateRevision(Guid.NewGuid(), School, header.Id, 1, Published,
					recipients[i], CommunicationChannelType.InApp, "en", "Syllabus available", "Open your school inbox.", 1);
				template.Publish(Actor, DateTimeOffset.UtcNow);
				await InsertAsync(services, template);
				headers[i] = header.Id;
			}
			var rule = new NotificationRule(Guid.NewGuid(), School, Published,
				NotificationRuleScope.Create(board.AcademicYearId, board.Id, medium.Id, classId, divisionId));
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], true, [],
				[CommunicationChannelType.InApp], headers, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config, services.GetRequiredService<AcademicNotificationCatalogue>().Get(Published)), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
		});

		var route = $"{ChapterPath}?standardId={classId}&subjectId={subjectId}";
		var startDate = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(2).AddTicks(7));
		var endDate = startDate.AddDays(3);
		using var created = await Client.PostAsJsonAsync(route, new[]
		{
			new { assignedSubjectId = assignmentId, chapterNo = 1, chapterName = "Synthetic chapter", startDate, endDate,
				publishStatus = "Published", visibility = "Parents", teacherNotes = "Syllabus test private notes" }
		}, JsonOptions);
		created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		NotificationIntent publication = null!;
		var chapterId = Guid.Empty;
		await InSchoolAsync(async services =>
		{
			var chapter = (await services.GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>()
				.GetListAsync(x => x.AssignedSubjectId == assignmentId)).ShouldHaveSingleItem();
			chapterId = chapter.Id;
			publication = (await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == School && x.EventKey == Published && x.SourceId == chapter.Id)).ShouldHaveSingleItem();
			var source = await services.GetRequiredService<NotificationIntentSourceReader>().LoadAsync(publication, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
			source.ExclusionCode.ShouldBeNull();
			source.CurrentRecipients.ShouldBe([new("Student", StudentId: learner)]);
			source.Runtime.Configuration.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
			source.Runtime.Configuration.IncludeStudentAccount.ShouldBeFalse();
			source.Snapshot!.Variables.Values.ShouldNotContain(x => x.Value == chapter.TeacherNotes);
		});

		var operation = $"/api/academic/notifications/operations/{publication.CampaignId}";
		using var viewer = Factory.CreateClient();
		viewer.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.Academics.Notification.View);
		using var denied = await viewer.GetAsync(operation);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var allowed = await Client.GetAsync(operation);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());

		using var draft = await Client.PutAsJsonAsync($"{ChapterPath}/{chapterId}", new
		{
			assignedSubjectId = assignmentId, chapterNo = 1, chapterName = "Synthetic chapter", startDate, endDate,
			publishStatus = "Draft", visibility = "Parents", teacherNotes = "Syllabus test private notes"
		}, JsonOptions);
		draft.StatusCode.ShouldBe(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
		await InSchoolAsync(async services =>
			(await services.GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(publication, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty());
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
public sealed class SyllabusNotificationPostgres_Tests
{
	[AcademicPerformancePostgresFact]
	public async Task Published_chapters_use_persisted_elective_and_family_scope()
	{
		await using var fixture = new AcademicPerformancePostgresTestFactory();
		await new SyllabusNotificationDelivery_Tests(fixture).RunAsync();
	}
}
