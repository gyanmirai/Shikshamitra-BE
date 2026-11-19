using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class LearningTaskNotificationEligibility_Tests
{
	private const string AssignmentPublication = AcademicNotificationEventKeys.AssignmentPublished;

	[Fact]
	public async Task Assignment_publication_only_captures_the_division_that_became_visible()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var seed = await SeedAsync();
		var assignmentId = Guid.NewGuid();
		var immediateScheduleId = Guid.NewGuid();
		var scheduledScheduleId = Guid.NewGuid();
		var allocationId = Guid.NewGuid();
		var laterDivision = Guid.Empty;
		var classTeacher = default(NotificationRecipientRefDto)!;
		var laterTeacher = default(NotificationRecipientRefDto)!;
		var deadline = DateTime.UtcNow.Date.AddDays(4).AddHours(12);
		await InSchoolAsync(async services =>
		{
			var homework = await services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>().GetAsync(seed.TaskId);
			var classId = homework.StandardId;
			var medium = Factory.SeededMediumId;
			var year = Factory.SeededAcademicYearId;
			var division = await services.GetRequiredService<DivisionManager>().CreateAsync(classId, $"Scheduled {Guid.NewGuid():N}"[..21], 32);
			laterDivision = (await InsertAsync(services, division)).Id;
			var type = await services.GetRequiredService<AssignmentTypeManager>().CreateAsync(medium, $"Publication {Guid.NewGuid():N}"[..25],
				defaultMarks: 10, defaultEvaluationMethod: EvaluationMethod.Marks);
			var assignment = new AssignmentEntity(assignmentId, classId, homework.SubjectId, "Division release", "", type.Id,
				SubmissionType.OnlineText, 10, TenantId, year, educationBoardId: Factory.SeededBoardId, educationMediumId: medium,
				educationMediumStageId: Factory.SeededMediumStageId);
			assignment.AddDivisionSchedule(new(immediateScheduleId, assignmentId, Factory.SeededDivisionId, null, deadline, TenantId));
			assignment.AddDivisionSchedule(new(scheduledScheduleId, assignmentId, laterDivision, deadline.AddHours(-2), deadline, TenantId));
			await InsertAsync(services, assignment);

			var teachers = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.UserId.HasValue && x.EmployeeStatusType == EmployeeStatusType.Active))
				.DistinctBy(x => x.UserId).Take(2).ToArray();
			classTeacher = new("AssignedTeacher", UserId: teachers[0].UserId, EmployeeId: teachers[0].Id);
			laterTeacher = new("AssignedTeacher", UserId: teachers[1].UserId, EmployeeId: teachers[1].Id);
			var subjects = services.GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var subject = (await subjects.WithDetailsAsync(x => x.Teachers)).Single(x => x.SubjectId == homework.SubjectId && x.StandardId == classId);
			subject.Teachers.Add(new AssignedSubjectTeacher(subject.Id, homework.SubjectId, teachers[0].Id, SubjectTeacherType.Primary, TenantId));
			await subjects.UpdateAsync(subject, autoSave: true);
			var allocation = new TeacherSubjectAllocation(allocationId, classId, laterDivision, homework.SubjectId);
			allocation.Teachers.Add(new AllocatedTeacher(allocation.Id, teachers[1].Id));
			await InsertAsync(services, allocation);

			var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(AssignmentPublication);
			var templateIds = new List<Guid>();
			foreach (var kind in new[] { "Guardian", "AssignedTeacher" })
			{
				var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = kind + " assignment publication" });
				templateIds.Add(template.Id);
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, AssignmentPublication,
					kind, CommunicationChannelType.InApp, "en", "Assignment published", "A new assignment is available.", definition.SchemaVersion);
				revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
				await InsertAsync(services, revision);
			}
			var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], templateIds, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, AssignmentPublication,
				NotificationRuleScope.Create(year, Factory.SeededBoardId, medium, classId));
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(configuration, definition), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);
		});

		const string publish = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add;
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, SourceView + "," + publish);
		using var response = await Client.PatchAsJsonAsync($"/api/academic/assignments/{assignmentId}/status",
			new AssignmentStatusUpdateDto { Status = (int)AssignmentStatus.Published }, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		Client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, StaffActions + "," + AssignmentView);
		using var denied = await PreviewPublicationAsync(immediateScheduleId);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		Client.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, StaffActions + "," + SourceView);
		using var allowed = await PreviewPublicationAsync(immediateScheduleId);
		allowed.StatusCode.ShouldBe(HttpStatusCode.OK, await allowed.Content.ReadAsStringAsync());
		using var unpublished = await PreviewPublicationAsync(scheduledScheduleId);
		unpublished.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		await InSchoolAsync(async services =>
		{
			var sources = await ReadSourcesAsync(services, immediateScheduleId);
			sources.Length.ShouldBe(2);
			sources.Single(x => x.Runtime.SourceScope.DivisionId is null).CurrentRecipients.ShouldBe([classTeacher]);
			var family = sources.Single(x => x.Runtime.SourceScope.DivisionId == Factory.SeededDivisionId);
			family.CurrentRecipients.Select(x => x.StudentId).ShouldBe([seed.Active, seed.Legacy, seed.Submitted, seed.AwaitingReview], ignoreOrder: true);
			(await ReadSourcesAsync(services, scheduledScheduleId)).ShouldBeEmpty();
			var schedules = services.GetRequiredService<IRepository<AssignmentDivisionSchedule, Guid>>();
			var schedule = await schedules.GetAsync(scheduledScheduleId);
			schedule.IsPublished.ShouldBeFalse();
			schedule.UpdateSchedule(DateTime.UtcNow.AddMinutes(-1), deadline);
			await schedules.UpdateAsync(schedule, autoSave: true);
		});

		using (var scope = Factory.Services.CreateScope())
		{
			using var school = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId, "Synthetic school");
			var scheduler = scope.ServiceProvider.GetRequiredService<AssignmentScheduledPublicationService>();
			(await scheduler.PublishDueSchedulesAsync(DateTime.UtcNow)).ShouldBeGreaterThanOrEqualTo(1);
			(await scheduler.PublishDueSchedulesAsync(DateTime.UtcNow)).ShouldBe(0);
		}

		await InSchoolAsync(async services =>
		{
			(await ReadSourcesAsync(services, immediateScheduleId)).Length.ShouldBe(2);
			var source = (await ReadSourcesAsync(services, scheduledScheduleId)).ShouldHaveSingleItem();
			source.Runtime.SourceScope.DivisionId.ShouldBe(laterDivision);
			source.CurrentRecipients.ShouldBe([laterTeacher]);
			source.Snapshot!.Variables["due_date"].Type.ShouldBe(NotificationVariableType.DateTime);

			await services.GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>().DeleteAsync(allocationId, autoSave: true);
			(await ReadSourcesAsync(services, scheduledScheduleId)).ShouldHaveSingleItem().CurrentRecipients.ShouldBeEmpty();
		});

		Task<System.Net.Http.HttpResponseMessage> PreviewPublicationAsync(Guid sourceId) =>
			Client.PostAsJsonAsync("/api/academic/notifications/manual/preview",
				new PreviewAcademicNotificationManualDto(AssignmentPublication, AssignmentSource, sourceId,
					[new("Student", seed.Active)], [CommunicationChannelType.InApp]), JsonOptions);
	}

	private static async Task<NotificationCurrentSource[]> ReadSourcesAsync(IServiceProvider services, Guid sourceId)
	{
		var intents = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
			x.TenantId == TenantId && x.EventKey == AssignmentPublication && x.SourceId == sourceId);
		var reader = services.GetRequiredService<NotificationIntentSourceReader>();
		var sources = new NotificationCurrentSource[intents.Count];
		for (var index = 0; index < intents.Count; index++)
		{
			intents[index].Status.ShouldBe(NotificationIntentState.Ready);
			sources[index] = await reader.LoadAsync(intents[index], DateTimeOffset.UtcNow, CancellationToken.None);
		}
		return sources;
	}
}
