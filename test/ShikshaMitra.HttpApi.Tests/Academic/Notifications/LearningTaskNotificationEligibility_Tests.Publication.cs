using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public partial class LearningTaskNotificationEligibility_Tests
{
	private const string Publication = AcademicNotificationEventKeys.HomeworkPublished;

	[Fact]
	public async Task Homework_publication_captures_eligible_families_and_currently_assigned_teachers_once()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var seed = await SeedAsync();
		var divisionId = Factory.SeededDivisionId;
		var classId = Factory.SeededStandardId;
		var classScope = NotificationRuleScope.Create(Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, classId);
		var teacherReferences = Array.Empty<NotificationRecipientRefDto>();
		var allocationId = Guid.NewGuid();
		await InSchoolAsync(async services =>
		{
			var homeworks = services.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
			var task = await homeworks.GetAsync(seed.DraftId);
			task.SubmissionType = SubmissionType.NoSubmissionRequired;
			await homeworks.UpdateAsync(task);
			var assignments = services.GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = (await assignments.WithDetailsAsync(x => x.Teachers))
				.Single(x => x.SubjectId == task.SubjectId && x.StandardId == classId);
			var teachers = (await services.GetRequiredService<IRepository<Employee, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.UserId.HasValue && x.EmployeeStatusType == EmployeeStatusType.Active))
				.DistinctBy(x => x.UserId).Take(3).ToArray();
			teachers.Length.ShouldBe(3);
			teacherReferences = teachers.Select(x => new NotificationRecipientRefDto("AssignedTeacher", UserId: x.UserId, EmployeeId: x.Id)).ToArray();
			assignment.Teachers.Add(new AssignedSubjectTeacher(assignment.Id, task.SubjectId, teachers[0].Id, SubjectTeacherType.Primary, TenantId));
			await assignments.UpdateAsync(assignment, autoSave: true);

			var allocation = new TeacherSubjectAllocation(allocationId, classId, divisionId, task.SubjectId);
			allocation.Teachers.Add(new AllocatedTeacher(allocationId, teachers[1].Id));
			await InsertAsync(services, allocation);
			var sibling = await services.GetRequiredService<DivisionManager>().CreateAsync(classId, $"Sibling {Guid.NewGuid():N}"[..20], 32);
			await InsertAsync(services, sibling);
			var outside = new TeacherSubjectAllocation(Guid.NewGuid(), classId, sibling.Id, task.SubjectId);
			outside.Teachers.Add(new AllocatedTeacher(outside.Id, teachers[2].Id));
			await InsertAsync(services, outside);

			var definition = services.GetRequiredService<AcademicNotificationCatalogue>().Get(Publication);
			var templateIds = new Guid[2];
			var recipientKinds = new[] { "Guardian", "AssignedTeacher" };
			for (var index = 0; index < recipientKinds.Length; index++)
			{
				var kind = recipientKinds[index];
				var template = await InsertAsync(services, new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = kind + " publication" });
				templateIds[index] = template.Id;
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, Publication, kind,
					CommunicationChannelType.InApp, "en", "Homework published", "New homework is available.", definition.SchemaVersion);
				revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
				await InsertAsync(services, revision);
			}
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, Publication, classScope);
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, ["AssignedTeacher"],
				[CommunicationChannelType.InApp], templateIds, NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, services.GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(config, definition), rule.ConcurrencyStamp);
			await InsertAsync(services, rule);

			var intents = services.GetRequiredService<IRepository<NotificationIntent, Guid>>();
			(await intents.GetListAsync(x => x.SourceId == seed.DraftId)).ShouldBeEmpty();
		});

		const string publish = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add;
		Client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, SourceView + "," + publish);
		var route = $"/api/academic/homeworks/{seed.DraftId}/status";
		var status = new HomeworkStatusUpdateDto { Status = (int)HomeworkStatus.Published };
		using var result = await Client.PatchAsJsonAsync(route, status, JsonOptions);
		result.StatusCode.ShouldBe(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
		using var repeated = await Client.PatchAsJsonAsync(route, status, JsonOptions);
		repeated.IsSuccessStatusCode.ShouldBeFalse();

		await InSchoolAsync(async services =>
		{
			var intents = await services.GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.TenantId == TenantId && x.SourceId == seed.DraftId && x.EventKey == Publication);
			intents.Count.ShouldBe(2);
			intents.Select(x => x.CampaignId).Distinct().ShouldHaveSingleItem().ShouldNotBeNull();
			intents.ShouldAllBe(x => x.Status == NotificationIntentState.Ready);
			var reader = services.GetRequiredService<NotificationIntentSourceReader>();
			var now = DateTimeOffset.UtcNow;
			var sources = new List<(NotificationIntent Intent, NotificationCurrentSource Source)>();
			foreach (var intent in intents) sources.Add((intent, await reader.LoadAsync(intent, now, CancellationToken.None)));
			var classTeacher = sources.Single(x => !x.Source.Runtime.SourceScope.DivisionId.HasValue).Source;
			classTeacher.CurrentRecipients.ShouldHaveSingleItem().ShouldBe(teacherReferences[0]);
			classTeacher.Snapshot!.RecipientContent!.Single(x => x.Recipient == teacherReferences[0])
				.Variables["due_date"].Type.ShouldBe(NotificationVariableType.DateTime);
			var (divisionIntent, division) = sources.Single(x => x.Source.Runtime.SourceScope.DivisionId == divisionId);
			division.CurrentRecipients.ShouldBe(
				[new("Student", seed.Active), new("Student", seed.Legacy), new("Student", seed.Submitted),
					new("Student", seed.AwaitingReview), teacherReferences[1]], ignoreOrder: true);
			division.Snapshot!.RecipientContent!.Any(x => x.Recipient.StudentId == seed.Active && x.Variables.ContainsKey("due_date")).ShouldBeTrue();

			var allocations = services.GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>();
			await allocations.DeleteAsync(allocationId, autoSave: true);
			var changed = await reader.LoadAsync(divisionIntent, now, CancellationToken.None);
			changed.CurrentRecipients.ShouldNotContain(teacherReferences[1]);
		});
	}
}
