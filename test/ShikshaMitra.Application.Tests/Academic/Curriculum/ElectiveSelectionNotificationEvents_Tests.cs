using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public sealed class ElectiveSelectionNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task A_successful_lock_only_notifies_newly_confirmed_selections()
	{
		using var actor = AsAdmin();
		using var school = Tenant.Change(TenantId, "Synthetic school");
		const string confirmed = AcademicNotificationEventKeys.ElectiveSelectionConfirmed;
		var studentId = Guid.NewGuid();
		var selectionId = Guid.NewGuid();
		var groupId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var group = new ElectiveGroup(groupId, TenantId, "Selection notice elective", "#24597a", medium.Id);
			group.AddSubject(SubjectId);
			await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await assignments.GetAsync(AssignmentId);
			assignment.SubjectType = SubjectType.Elective;
			assignment.ElectiveGroupId = groupId;
			assignment.ChangeStatus(SubjectStatus.Published);
			await assignments.UpdateAsync(assignment, autoSave: true);
			foreach (var (student, rowId, isLocked) in new[] { (Guid.NewGuid(), Guid.NewGuid(), true), (studentId, selectionId, false) })
			{
				await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(new Student(student, TenantId, "Synthetic",
					null, "Learner", board.AcademicYearId, board.Id, medium.Id, standard.EducationMediumStageId, StandardId, DivisionId,
					status: StudentLifeCycleStatusEnum.Enrolled), autoSave: true);
				await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(
					StudentEnrollment.Create(Guid.NewGuid(), TenantId, student, board.AcademicYearId, StandardId, DivisionId), autoSave: true);
				var selection = new StudentElectiveSelection(rowId, TenantId, student, groupId, SubjectId, medium.Id);
				if (isLocked) selection.LockSelection();
				await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().InsertAsync(selection, autoSave: true);
			}

			var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Elective confirmation" };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
			var template = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, confirmed, "Guardian",
				CommunicationChannelType.InApp, "en", "Elective confirmed", "Open your school inbox.", 1);
			template.Publish(ActorId, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(template, autoSave: true);
			var rule = new NotificationRule(Guid.NewGuid(), TenantId, confirmed, NotificationRuleScope.Create());
			var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
				[CommunicationChannelType.InApp], [header.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
			rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
				config, GetRequiredService<AcademicNotificationCatalogue>().Get(confirmed)), rule.ConcurrencyStamp);
			await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
		});

		var lockInput = new LockStudentElectiveAssignmentsDto
		{
			AcademicMediumId = AcademicMediumId, ClassId = StandardId, DivisionId = DivisionId, ElectiveGroupId = groupId
		};
		var service = GetRequiredService<IStudentElectiveAssignmentAppService>();
		for (var attempt = 0; attempt < 2; attempt++)
			await WithTransactionalUnitOfWorkAsync(async () => (await service.LockStudentElectiveAssignmentsAsync(lockInput)).Success.ShouldBeTrue());
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().GetListAsync(x => x.ElectiveGroupId == groupId))
				.All(x => x.IsLocked).ShouldBeTrue();
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.TenantId == TenantId && x.EventKey == confirmed)).ShouldHaveSingleItem();
			intent.SourceId.ShouldBe(selectionId);
			var source = await GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
			source.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
			source.CurrentRecipients.ShouldBe([new("Student", StudentId: studentId)]);
		});
	}
}
