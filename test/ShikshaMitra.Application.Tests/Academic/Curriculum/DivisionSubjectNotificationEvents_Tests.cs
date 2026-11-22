using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassManagement.TeacherAssignment;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class DivisionSubjectNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Division_teacher_changes_notify_only_the_affected_division_accounts(bool assignReplacement)
	{
		using var actor = AsAdmin();
		using var school = Tenant.Change(TenantId, "Synthetic school");
		const string changed = AcademicNotificationEventKeys.SubjectTeacherChanged;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignment = await GetRequiredService<IRepository<AssignedSubject, Guid>>().GetAsync(AssignmentId);
			assignment.ChangeStatus(SubjectStatus.Published);
			await GetRequiredService<IRepository<AssignedSubject, Guid>>().UpdateAsync(assignment, autoSave: true);
			var allocation = new TeacherSubjectAllocation(Guid.NewGuid(), StandardId, DivisionId, SubjectId);
			allocation.Teachers.Add(new(allocation.Id, OldTeacher));
			await GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>().InsertAsync(allocation, autoSave: true);
			var headers = new Guid[2];
			var kinds = new[] { "AssignedTeacher", "PreviousTeacher" };
			for (var i = 0; i < kinds.Length; i++)
			{
				var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = changed + " " + kinds[i] };
				await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
				var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, changed, kinds[i],
					CommunicationChannelType.InApp, "en", "Subject allocation", "Open the school inbox.", 1);
				revision.Publish(ActorId, DateTimeOffset.UtcNow);
				await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
				headers[i] = header.Id;
			}

			var rule = new NotificationRule(Guid.NewGuid(), TenantId, changed, NotificationRuleScope.Create());
			var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
				kinds, [CommunicationChannelType.InApp], headers,
				NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				settings, GetRequiredService<AcademicNotificationCatalogue>().Get(changed)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		var service = GetRequiredService<ITeacherAssignmentAppService>();
		var input = new SaveTeacherAssignmentsDto
		{
			ClassId = StandardId, AcademicMediumId = AcademicMediumId,
			Assignments = [new TeacherAssignmentUpdateDto { DivisionId = DivisionId, SubjectId = SubjectId, TeacherIds = assignReplacement ? [NewTeacher] : [] }]
		};
		await WithTransactionalUnitOfWorkAsync(async () => { (await service.SaveTeacherAssignmentsForClassAsync(input)).Success.ShouldBeTrue(); });
		await WithTransactionalUnitOfWorkAsync(async () => { (await service.SaveTeacherAssignmentsForClassAsync(input)).Success.ShouldBeTrue(); });

		await WithUnitOfWorkAsync(async () =>
		{
			var notice = (await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.TenantId == TenantId && x.EventKey == changed)).ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(notice, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			source.CurrentRecipients.Select(x => (x.Kind, x.EmployeeId!.Value)).ToHashSet()
				.SetEquals(assignReplacement ? [("AssignedTeacher", NewTeacher), ("PreviousTeacher", OldTeacher)] : [("PreviousTeacher", OldTeacher)])
				.ShouldBeTrue();
		});
	}
}
