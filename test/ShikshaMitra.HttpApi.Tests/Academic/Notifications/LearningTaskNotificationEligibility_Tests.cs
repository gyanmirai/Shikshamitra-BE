using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

[Collection(ApiTestCollection.Name)]
public class LearningTaskNotificationEligibility_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string SourceType = "Academic.Homework";
	private const string EventKey = AcademicNotificationEventKeys.HomeworkDue;
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private const string StaffActions = ShikshaMitraSubmodulePermissions.Academics.Notification.View + "," +
		ShikshaMitraSubmodulePermissions.Academics.Notification.Send;
	private const string SourceView = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View;

	[Fact]
	public async Task Upcoming_homework_targets_only_saved_eligible_learners_and_rechecks_the_roster()
	{
		var seed = await SeedAsync();
		var snapshot = await ReadAsync(seed.TaskId);
		snapshot.IsEligible.ShouldBeTrue();
		snapshot.EligibleRecipients.Select(x => x.StudentId!.Value).ToHashSet().SetEquals([seed.Active, seed.Legacy]).ShouldBeTrue();
		var slice = snapshot.AudienceSlices!.ShouldHaveSingleItem();
		slice.Scope.ShouldBe(new(Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
			Factory.SeededStandardId, Factory.SeededDivisionId));
		snapshot.RecipientContent!.All(x => x.Variables.ContainsKey("due_date") && x.RelativeLink == "/dashboard?openNotifications=1").ShouldBeTrue();
		(await ReadAsync(seed.DraftId)).IsEligible.ShouldBeFalse();
		(await ReadAsync(seed.PastId)).IsEligible.ShouldBeFalse();
		(await ReadAsync(seed.TaskId, Guid.NewGuid())).IsEligible.ShouldBeFalse();

		await InSchoolAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
			var active = await repository.GetAsync(x => x.StudentId == seed.Active && x.TenantId == TenantId);
			active.Deactivate();
			await repository.UpdateAsync(active);
		});
		var changed = await ReadAsync(seed.TaskId);
		changed.SourceRevision.ShouldBe(snapshot.SourceRevision);
		changed.EligibleRecipients.ShouldHaveSingleItem().StudentId.ShouldBe(seed.Legacy);
	}

	[Fact]
	public async Task Manual_review_uses_the_real_homework_adapter_and_requires_source_access()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var seed = await SeedAsync();
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, StaffActions);
		using var catalogue = await Client.GetAsync("/api/academic/notifications/catalogue");
		catalogue.StatusCode.ShouldBe(HttpStatusCode.OK, await catalogue.Content.ReadAsStringAsync());
		var events = (await catalogue.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("events");
		var due = events.EnumerateArray().Single(x => x.GetProperty("definition").GetProperty("eventKey").GetString() == EventKey);
		due.GetProperty("manualAvailability").GetProperty("isAvailable").GetBoolean().ShouldBeTrue();

		using var denied = await PreviewAsync(seed.TaskId, seed.Active);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await denied.Content.ReadAsStringAsync());

		Client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{StaffActions},{SourceView}");
		using var authorized = await PreviewAsync(seed.TaskId, seed.Active);
		authorized.StatusCode.ShouldBe(HttpStatusCode.OK, await authorized.Content.ReadAsStringAsync());
		using var outside = await PreviewAsync(seed.TaskId, seed.Unselected);
		outside.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await outside.Content.ReadAsStringAsync());
	}

	private Task<HttpResponseMessage> PreviewAsync(Guid taskId, Guid studentId) =>
		Client.PostAsJsonAsync("/api/academic/notifications/manual/preview",
			new PreviewAcademicNotificationManualDto(EventKey, SourceType, taskId,
				[new("Student", studentId)], [CommunicationChannelType.InApp]), JsonOptions);

	private async Task<NotificationSourceSnapshot> ReadAsync(Guid taskId, Guid? contextTenant = null)
	{
		NotificationSourceSnapshot snapshot = null!;
		await InSchoolAsync(async services =>
		{
			var adapter = services.GetServices<INotificationSourceAdapter>().Single(x => x.EventKeys.Contains(EventKey));
			snapshot = await adapter.LoadAsync(new(Guid.Empty, contextTenant ?? TenantId, EventKey, SourceType, taskId,
				string.Empty, DateTimeOffset.UtcNow, []), CancellationToken.None);
		});
		return snapshot;
	}

	private sealed record Seed(Guid TaskId, Guid DraftId, Guid PastId, Guid Active, Guid Legacy, Guid Unselected);

	private async Task<Seed> SeedAsync()
	{
		var seed = new Seed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		await InSchoolAsync(async services =>
		{
			var medium = Factory.SeededMediumId;
			var standard = Factory.SeededStandardId;
			var division = Factory.SeededDivisionId;
			var year = Factory.SeededAcademicYearId;
			var subject = new Subject(Guid.NewGuid(), TenantId, "Reminder subject", "#24597a", medium);
			var group = new ElectiveGroup(Guid.NewGuid(), TenantId, "Reminder electives", "#24597a", medium);
			await InsertAsync(services, subject);
			await InsertAsync(services, group);
			await InsertAsync(services, new AssignedSubject(Guid.NewGuid(), TenantId, standard, subject.Id,
				$"REM-{Guid.NewGuid():N}"[..20], SubjectStatus.Published, electiveGroupId: group.Id));
			var type = await services.GetRequiredService<HomeworkTypeManager>().CreateAsync(medium, $"Reminder {Guid.NewGuid():N}"[..28]);
			await SaveHomeworkAsync(seed.TaskId, DateTime.UtcNow.AddDays(3), true);
			await SaveHomeworkAsync(seed.DraftId, DateTime.UtcNow.AddDays(3), false);
			await SaveHomeworkAsync(seed.PastId, DateTime.UtcNow.AddDays(-1), true);

			var withdrawn = Guid.NewGuid();
			var submitted = Guid.NewGuid();
			var needsReview = Guid.NewGuid();
			var unlocked = Guid.NewGuid();
			var deletedEnrollment = Guid.NewGuid();
			foreach (var id in new[] { seed.Active, seed.Legacy, seed.Unselected, withdrawn, submitted, needsReview, unlocked, deletedEnrollment })
			{
				await InsertAsync(services, new Student(id, TenantId, "Sample", null, "Learner", year, Factory.SeededBoardId,
					medium, Factory.SeededMediumStageId, standard, division, status: StudentLifeCycleStatusEnum.Enrolled));
				if (id != seed.Legacy)
				{
					var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TenantId, id, year, standard, division);
					if (id == withdrawn) enrollment.Deactivate();
					if (id == deletedEnrollment) enrollment.IsDeleted = true;
					await InsertAsync(services, enrollment);
				}
				if (id == seed.Unselected) continue;
				var selection = new StudentElectiveSelection(Guid.NewGuid(), TenantId, id, group.Id, subject.Id, medium);
				if (id != unlocked) selection.LockSelection();
				await InsertAsync(services, selection);
			}
			await InsertAsync(services, new StudentSubmissionEntity(Guid.NewGuid(), seed.TaskId, submitted, division,
				DateTime.UtcNow.AddDays(3), TenantId) { Status = SubmissionStatus.Submitted, SubmissionDate = DateTimeOffset.UtcNow });
			await InsertAsync(services, new StudentSubmissionEntity(Guid.NewGuid(), seed.TaskId, needsReview, division,
				DateTime.UtcNow.AddDays(3), TenantId) { Status = SubmissionStatus.NeedsReview });

			async Task SaveHomeworkAsync(Guid id, DateTime due, bool published)
			{
				var task = new HomeworkAssignmentEntity(id, standard, subject.Id, "Practice", "", type.Id,
					SubmissionType.OnlineText, TenantId, educationMediumId: medium);
				task.AddDivisionSchedule(new(Guid.NewGuid(), id, division, null, due, TenantId));
				if (published) task.PublishAll();
				await InsertAsync(services, task);
			}
		});
		return seed;
	}

	private async Task InSchoolAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await action(services);
		await uow.CompleteAsync();
	}

	private static Task<T> InsertAsync<T>(IServiceProvider services, T entity) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
		services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);
}
