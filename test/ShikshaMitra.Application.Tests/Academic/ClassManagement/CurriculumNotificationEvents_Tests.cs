
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Common;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;

public partial class ClassTeacherAssignmentAppService_Tests
{
	[Fact]
	public async Task A_confirmed_class_teacher_replacement_uses_old_and_new_assignees_without_draft_notices()
	{
		using var school = _currentTenant.Change(TestTenantId, "Synthetic school");
		var manager = ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider);
		using var unit = manager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var standard = await _standardRepository.InsertAsync(new Standard(Guid.NewGuid(), TestTenantId,
			"Notification class", 7, stageId, shiftId: null), autoSave: true);
		var division = await _divisionRepository.InsertAsync(new Division(Guid.NewGuid(), TestTenantId, "A", standard.Id, 30, "D-NOTICE"), autoSave: true);
		var former = await CreateAssignedAccountAsync("Former");
		var current = await CreateAssignedAccountAsync("Current");
		const string assigned = AcademicNotificationEventKeys.ClassTeacherAssigned;
		const string replaced = AcademicNotificationEventKeys.ClassTeacherReplaced;
		const string removed = AcademicNotificationEventKeys.ClassTeacherRemoved;
		await EnableClassTeacherNoticeAsync(assigned, ["AssignedTeacher"]);
		await EnableClassTeacherNoticeAsync(replaced, ["AssignedTeacher", "PreviousTeacher"]);
		await EnableClassTeacherNoticeAsync(removed, ["PreviousTeacher"]);

		var input = new SaveClassTeacherAssignmentsDto
		{
			OrganizationId = stageId, SaveAsDraft = true,
			Assignments = [new() { ClassId = standard.Id, DivisionId = division.Id, TeacherId = former.Id }]
		};
		var intents = GetRequiredService<IRepository<NotificationIntent, Guid>>();
		var reader = GetRequiredService<NotificationIntentSourceReader>();

		await _service.SaveClassTeacherAssignmentsAsync(input);
		(await intents.GetListAsync(x => x.EventKey == assigned || x.EventKey == replaced)).ShouldBeEmpty();
		input.SaveAsDraft = false;
		await _service.SaveClassTeacherAssignmentsAsync(input);
		await _service.SaveClassTeacherAssignmentsAsync(input);
		var first = (await intents.GetListAsync(x => x.EventKey == assigned)).ShouldHaveSingleItem();
		var firstSource = await reader.LoadAsync(first, DateTimeOffset.UtcNow, CancellationToken.None);
		firstSource.CurrentRecipients.ShouldBe([new("AssignedTeacher", UserId: former.UserId, EmployeeId: former.Id)]);

		input.SaveAsDraft = true;
		input.Assignments[0].TeacherId = current.Id;
		await _service.SaveClassTeacherAssignmentsAsync(input);
		(await intents.GetListAsync(x => x.EventKey == replaced)).ShouldBeEmpty();

		input.SaveAsDraft = false;
		await _service.SaveClassTeacherAssignmentsAsync(input);
		await _service.SaveClassTeacherAssignmentsAsync(input);
		(await intents.GetListAsync(x => x.EventKey == assigned)).ShouldHaveSingleItem();
		var replacement = (await intents.GetListAsync(x => x.EventKey == replaced)).ShouldHaveSingleItem();
		var source = await reader.LoadAsync(replacement, DateTimeOffset.UtcNow, CancellationToken.None);
		source.CurrentRecipients.ToHashSet().SetEquals(
			[new("AssignedTeacher", UserId: current.UserId, EmployeeId: current.Id), new("PreviousTeacher", UserId: former.UserId, EmployeeId: former.Id)])
			.ShouldBeTrue();
		source.Runtime.SourceScope.DivisionId.ShouldBe(division.Id);
		(await reader.LoadAsync(first, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		source.Snapshot!.RecipientContent!.ShouldHaveSingleItem().Variables.Keys.ShouldBe(["school_name"]);

		var otherDivision = await _divisionRepository.InsertAsync(new Division(Guid.NewGuid(), TestTenantId, "B", standard.Id, 30, "D-SECOND"),
			autoSave: true);
		input.SaveAsDraft = true;
		input.Assignments = [new() { ClassId = standard.Id, DivisionId = otherDivision.Id, TeacherId = former.Id }];
		await _service.SaveClassTeacherAssignmentsAsync(input);
		(await intents.GetListAsync(x => x.SourceId == division.Id && x.EventKey == removed)).ShouldBeEmpty();

		input.SaveAsDraft = false;
		await _service.SaveClassTeacherAssignmentsAsync(input);
		await _service.SaveClassTeacherAssignmentsAsync(input);
		var removal = (await intents.GetListAsync(x => x.SourceId == division.Id && x.EventKey == removed)).ShouldHaveSingleItem();
		var removedSource = await reader.LoadAsync(removal, DateTimeOffset.UtcNow, CancellationToken.None);
		removedSource.CurrentRecipients.ShouldBe([new("PreviousTeacher", UserId: current.UserId, EmployeeId: current.Id)]);
		removedSource.Snapshot!.RecipientContent!.ShouldHaveSingleItem().Variables.Keys.ShouldBe(["school_name"]);
		(await reader.LoadAsync(replacement, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients.ShouldBeEmpty();
		await unit.CompleteAsync();
	}

	private async Task<Employee> CreateAssignedAccountAsync(string displayName)
	{
		var userId = Guid.NewGuid();
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new(userId,
			$"{displayName}-{userId:N}"[..26], $"{userId:N}@example.test", TestTenantId), autoSave: true);
		var teacher = await CreateEmployeeAsync(displayName, EmployeeStatusType.Active, EmployeeGroupType.Teaching);
		teacher.UpdateUserId(userId);
		return await _employeeRepository.UpdateAsync(teacher, autoSave: true);
	}

	private async Task EnableClassTeacherNoticeAsync(string eventKey, string[] recipients)
	{
		var templates = new List<Guid>();
		foreach (var kind in recipients)
		{
			var header = new CommunicationTemplate(Guid.NewGuid(), TestTenantId) { TemplateName = eventKey + " " + kind };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TestTenantId, header.Id, 1, eventKey, kind,
				CommunicationChannelType.InApp, "en", "Class teacher update", "Open the school site.", 1);
			revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
			templates.Add(header.Id);
		}
		var rule = new NotificationRule(Guid.NewGuid(), TestTenantId, eventKey, NotificationRuleScope.Create());
		var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
			recipients, [CommunicationChannelType.InApp], templates,
			NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
		rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>()
			.Serialize(settings, GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey)), rule.ConcurrencyStamp);
		await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
	}
}
