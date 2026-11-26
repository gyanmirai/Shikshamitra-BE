using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAllocationNotificationEvents_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Subject_allocation_notices_use_the_saved_publication_and_only_changed_teachers()
	{
		using var actor = AsAdmin();
		using var school = Tenant.Change(TenantId, "Synthetic school");
		const string published = AcademicNotificationEventKeys.SubjectAllocated;
		const string changed = AcademicNotificationEventKeys.SubjectTeacherChanged;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var repository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await repository.GetAsync(AssignmentId);
			assignment.ChangeStatus(SubjectStatus.Draft);
			await repository.UpdateAsync(assignment, autoSave: true);
			await AddRuleAsync(published, ["AssignedTeacher"]);
			await AddRuleAsync(changed, ["AssignedTeacher", "PreviousTeacher"]);
		});

		var intents = GetRequiredService<IRepository<NotificationIntent, Guid>>();
		await WithUnitOfWorkAsync(async () => (await intents.GetListAsync(x => x.SourceId == AssignmentId)).ShouldBeEmpty());

		await WithTransactionalUnitOfWorkAsync(async () => { await Allocations.PublishAsync([AssignmentId]); });
		await WithTransactionalUnitOfWorkAsync(async () =>
		{
			await Allocations.ChangeStatusAsync(AssignmentId, new ChangeStatusRequestDto { Status = (int)SubjectStatus.Published });
		});
		NotificationIntent publication = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			publication = (await intents.GetListAsync(x => x.SourceId == AssignmentId && x.EventKey == published)).ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(publication, DateTimeOffset.UtcNow, CancellationToken.None);
			source.CurrentRecipients.Select(x => x.EmployeeId!.Value).ToHashSet().SetEquals([OldTeacher, OtherTeacher]).ShouldBeTrue();
		});

		await WithTransactionalUnitOfWorkAsync(async () => { await ChangeTeacherAsync(); });
		await WithTransactionalUnitOfWorkAsync(async () => { await ChangeTeacherAsync(); });
		await WithUnitOfWorkAsync(async () =>
		{
			var reader = GetRequiredService<NotificationIntentSourceReader>();
			(await reader.LoadAsync(publication, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients
				.ShouldNotContain(x => x.EmployeeId == OldTeacher);
			var change = (await intents.GetListAsync(x => x.SourceId == AssignmentId && x.EventKey == changed)).ShouldHaveSingleItem();
			var current = await reader.LoadAsync(change, DateTimeOffset.UtcNow, CancellationToken.None);
			current.CurrentRecipients.Select(x => (x.Kind, x.EmployeeId!.Value)).ToHashSet()
				.SetEquals([("AssignedTeacher", NewTeacher), ("PreviousTeacher", OldTeacher)]).ShouldBeTrue();
		});
	}

	[Theory]
	[InlineData(AcademicNotificationEventKeys.SubjectAllocated, false)]
	[InlineData(AcademicNotificationEventKeys.SubjectTeacherChanged, false)]
	[InlineData(AcademicNotificationEventKeys.SubjectAllocated, true)]
	[InlineData(AcademicNotificationEventKeys.SubjectTeacherChanged, true)]
	public async Task Overlapping_subject_staff_receive_one_notice_per_effective_rule(string eventKey, bool separateDivisionRule)
	{
		using var actor = AsAdmin();
		using var school = Tenant.Change(TenantId, "Synthetic school");
		var isPublication = eventKey == AcademicNotificationEventKeys.SubjectAllocated;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await assignments.GetAsync(AssignmentId);
			assignment.ChangeStatus(isPublication ? SubjectStatus.Draft : SubjectStatus.Published);
			await assignments.UpdateAsync(assignment, autoSave: true);
			await GetRequiredService<IRepository<ClassTeacherRecord, Guid>>().InsertAsync(new(
				Guid.NewGuid(), TenantId, StandardId, DivisionId, OldTeacher, AcademicMediumId), autoSave: true);
			await AddRuleAsync(eventKey, isPublication
				? ["AssignedTeacher", "ClassTeacher"]
				: ["AssignedTeacher", "PreviousTeacher", "ClassTeacher"]);
			if (separateDivisionRule)
			{
				var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
				var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
				await AddRuleAsync(eventKey, ["ClassTeacher"], NotificationRuleScope.Create(
					board.AcademicYearId, board.Id, medium.Id, StandardId, DivisionId));
			}
		});

		await WithTransactionalUnitOfWorkAsync(async () =>
		{
			if (isPublication) await Allocations.PublishAsync([AssignmentId]);
			else await ChangeTeacherAsync();
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var intents = await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(
				x => x.SourceId == AssignmentId && x.EventKey == eventKey);
			var reader = GetRequiredService<NotificationIntentSourceReader>();
			var recipients = new List<NotificationRecipientRefDto>();
			foreach (var intent in intents)
				recipients.AddRange((await reader.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None)).CurrentRecipients);

			var directKind = isPublication ? "AssignedTeacher" : "PreviousTeacher";
			recipients.Where(x => x.EmployeeId == OldTeacher).Select(x => x.Kind).OrderBy(x => x).ToArray()
				.ShouldBe(separateDivisionRule
					? new[] { "ClassTeacher", directKind }.OrderBy(x => x).ToArray()
					: [directKind]);
			recipients.ShouldContain(x => x.Kind == "AssignedTeacher" && x.EmployeeId == (isPublication ? OtherTeacher : NewTeacher));
			recipients.Count.ShouldBe(separateDivisionRule ? 3 : 2);
		});
	}

	[Fact]
	public async Task Withdrawing_an_elective_notifies_only_the_enrolled_learners_with_the_locked_choice()
	{
		using var actor = AsAdmin();
		using var school = Tenant.Change(TenantId, "Synthetic school");
		const string withdrawn = AcademicNotificationEventKeys.SubjectWithdrawn;
		var winningStudent = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var row = await assignments.GetAsync(AssignmentId);
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			var group = new ElectiveGroup(Guid.NewGuid(), TenantId, "Notice electives", "#24597a", AcademicMediumId);
			var otherSubject = new Subject(Guid.NewGuid(), TenantId, "Other elective", "#34567a", AcademicMediumId);
			await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(otherSubject, autoSave: true);
			group.AddSubject(SubjectId);
			group.AddSubject(otherSubject.Id);
			await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
			row.ElectiveGroupId = group.Id;
			row.SubjectType = SubjectType.Elective;
			await assignments.UpdateAsync(row, autoSave: true);
			foreach (var (id, choice, locked) in new[]
			{
				(winningStudent, SubjectId, true), (Guid.NewGuid(), otherSubject.Id, true), (Guid.NewGuid(), SubjectId, false)
			})
			{
				await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(new(id, TenantId, "Synthetic", null,
					"Learner", board.AcademicYearId, board.Id, AcademicMediumId, standard.EducationMediumStageId, StandardId, DivisionId,
					status: StudentLifeCycleStatusEnum.Enrolled), autoSave: true);
				await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(
					StudentEnrollment.Create(Guid.NewGuid(), TenantId, id, board.AcademicYearId, StandardId, DivisionId), autoSave: true);
				var selection = new StudentElectiveSelection(Guid.NewGuid(), TenantId, id, group.Id, choice, AcademicMediumId);
				if (locked) selection.LockSelection();
				await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().InsertAsync(selection, autoSave: true);
			}
			await AddRuleAsync(withdrawn, []);
		});

		await WithTransactionalUnitOfWorkAsync(async () => { await Allocations.PublishAsync([AssignmentId]); });
		await WithTransactionalUnitOfWorkAsync(async () => { await Allocations.ArchiveAsync(AssignmentId); });
		await WithTransactionalUnitOfWorkAsync(async () => { await Allocations.ArchiveAsync(AssignmentId); });

		NotificationIntent withdrawal = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			withdrawal = (await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x =>
				x.SourceId == AssignmentId && x.EventKey == withdrawn)).ShouldHaveSingleItem();
			var current = await GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(withdrawal, DateTimeOffset.UtcNow, CancellationToken.None);
			current.CurrentRecipients.ShouldBe([new("Student", StudentId: winningStudent)]);
			current.Runtime.SourceScope.DivisionId.ShouldBe(DivisionId);
		});

		await WithTransactionalUnitOfWorkAsync(async () => { await Allocations.RestoreAsync(AssignmentId); });
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(withdrawal, DateTimeOffset.UtcNow, CancellationToken.None))
				.CurrentRecipients.ShouldBeEmpty());
	}

	private async Task AddRuleAsync(string eventKey, string[] kinds, NotificationRuleScope? scope = null)
	{
		var headers = new List<Guid>();
		foreach (var kind in eventKey == AcademicNotificationEventKeys.SubjectWithdrawn ? new[] { "Guardian" } : kinds)
		{
			var header = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = eventKey + " " + kind };
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(header, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, header.Id, 1, eventKey, kind,
				CommunicationChannelType.InApp, "en", "Subject allocation", "Open the school inbox.", 1);
			revision.Publish(ActorId, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
			headers.Add(header.Id);
		}
		var rule = new NotificationRule(Guid.NewGuid(), TenantId, eventKey, scope ?? NotificationRuleScope.Create());
		var settings = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false,
			kinds, [CommunicationChannelType.InApp], headers,
			NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
		rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
			settings, GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey)), rule.ConcurrencyStamp);
		await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
	}
}
