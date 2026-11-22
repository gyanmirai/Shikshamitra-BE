using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Seating_notifications_use_both_releases_and_each_learners_own_seat(bool seatingFirst)
	{
		await WithSeatingNotificationTransactionAsync(async () =>
		{
			using var school = _currentTenant.Change(TestTenantId, "Synthetic school");
			await EnsureAdminUserExistsAsync();
			var (exam, _, _, students) = await CreateAllocatedSessionPlanAsync();
			await PublishSeatingSubjectsAsync(exam);
			const string eventKey = AcademicNotificationEventKeys.SeatingPublished;
			await AddSeatingRuleAsync(new[] { ("Guardian", CommunicationChannelType.InApp) });
			if (seatingFirst)
			{
				exam.ChangeStatus(ExamStatus.Pending);
				var mediumId = students.First().AcademicMediumId;
				var scale = await GetRequiredService<IRepository<GradingScale, Guid>>().InsertAsync(
					new(Guid.NewGuid(), TestTenantId, "Seating scale", "100", mediumId), autoSave: true);
				foreach (var subject in exam.ExamSubjects)
					subject.UpdateSchedule(subject.ExamDate, subject.StartTime, subject.EndTime, subject.TotalMarks,
						subject.PassingMarks, subject.Notes, gradeScalingId: scale.Id);
				await _examRepository.UpdateAsync(exam, autoSave: true);
			}

			var intents = GetRequiredService<IRepository<NotificationIntent, Guid>>();
			(await intents.GetListAsync(x => x.SourceId == exam.Id)).ShouldBeEmpty();
			var workspace = await _seatAllocationAppService.PublishExaminationSeatingAsync(exam.Id, new() { NotifyParents = true });
			workspace.Readiness.IsPublished.ShouldBeTrue();
			await _seatAllocationAppService.PublishExaminationSeatingAsync(exam.Id, new() { NotifyParents = true });
			if (seatingFirst)
			{
				(await intents.GetListAsync(x => x.SourceId == exam.Id)).ShouldBeEmpty();
				await GetRequiredService<ExamAppService>().ChangeStatusAsync(exam.Id, ExamStatus.Published);
				await GetRequiredService<ExamAppService>().ChangeStatusAsync(exam.Id, ExamStatus.Published);
			}

			var intent = (await intents.GetListAsync(x => x.TenantId == TestTenantId && x.SourceId == exam.Id && x.EventKey == eventKey))
				.ShouldHaveSingleItem();
			var current = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
			current.CurrentRecipients.Select(x => x.StudentId!.Value).ToHashSet().SetEquals(students.Select(x => x.Id)).ShouldBeTrue();
			current.Definition.RecipientSelectors.ShouldBe(["PrimaryGuardian", "AdditionalGuardians"]);
			current.Snapshot!.RecipientContent!.Count.ShouldBe(students.Count);
			var assigned = (await _seatAllocationRepository.GetListAsync(x => x.TenantId == TestTenantId && x.ExamId == exam.Id))
				.ToDictionary(x => x.StudentId, x => x.SeatNumber);
			foreach (var content in current.Snapshot!.RecipientContent!)
				content.Variables["seat_number"].Value.ShouldBe(assigned[content.Recipient.StudentId!.Value].ToString(CultureInfo.InvariantCulture));
		});
	}

	[Fact]
	public async Task Student_only_scheduled_publication_excludes_guardian_and_email_templates()
	{
		await WithSeatingNotificationTransactionAsync(async () =>
		{
			using var school = _currentTenant.Change(TestTenantId, "Synthetic school");
			await EnsureAdminUserExistsAsync();
			var (exam, _, _, students) = await CreateAllocatedSessionPlanAsync();
			await PublishSeatingSubjectsAsync(exam);
			await AddSeatingRuleAsync(new[]
			{
				("Guardian", CommunicationChannelType.InApp), ("Guardian", CommunicationChannelType.Email),
				("StudentAccount", CommunicationChannelType.InApp), ("StudentAccount", CommunicationChannelType.Email)
			}, includeStudent: true);
			var scheduledAt = DateTimeOffset.UtcNow.AddDays(1);
			await _seatAllocationAppService.PublishExaminationSeatingAsync(exam.Id, new()
			{
				NotifyStudents = true,
				ScheduleAt = scheduledAt.UtcDateTime
			});

			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>()
				.GetListAsync(x => x.SourceId == exam.Id && x.EventKey == AcademicNotificationEventKeys.SeatingPublished)).ShouldHaveSingleItem();
			intent.DueAt.ShouldBe(scheduledAt);
			var pinned = JsonSerializer.Deserialize<Guid[]>(intent.TemplateRevisionIdsJson)!.ShouldHaveSingleItem();
			var variant = await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().GetAsync(pinned);
			variant.RecipientKind.ShouldBe("StudentAccount");
			variant.Channel.ShouldBe(CommunicationChannelType.InApp);
			var current = await GetRequiredService<NotificationIntentSourceReader>()
				.LoadAsync(intent, scheduledAt, CancellationToken.None);
			current.CurrentRecipients.Count.ShouldBe(students.Count);
			current.Runtime.Configuration.GuardianMode.ShouldBe(NotificationGuardianMode.PrimaryOnly);
			current.Runtime.Configuration.AdditionalGuardianRelationships.ShouldBeEmpty();
			current.Runtime.Configuration.IncludeStudentAccount.ShouldBeTrue();
			current.Runtime.Configuration.Channels.ShouldBe([CommunicationChannelType.InApp]);
			current.Definition.RecipientSelectors.ShouldBe(["StudentAccount"]);
			NotificationTemplateSnapshotSelector.RecipientKinds(current.Runtime.Configuration, current.Definition)
				.ShouldBe(["StudentAccount"]);
		});
	}

	[Fact]
	public async Task Exam_staff_flag_does_not_guess_an_invigilator_audience_or_replay_the_release()
	{
		await WithSeatingNotificationTransactionAsync(async () =>
		{
			using var school = _currentTenant.Change(TestTenantId, "Synthetic school");
			await EnsureAdminUserExistsAsync();
			var (exam, _, _, _) = await CreateAllocatedSessionPlanAsync();
			await PublishSeatingSubjectsAsync(exam);
			await AddSeatingRuleAsync(new[] { ("Guardian", CommunicationChannelType.InApp) });
			await _seatAllocationAppService.PublishExaminationSeatingAsync(exam.Id, new() { NotifyExamStaff = true });
			await _seatAllocationAppService.PublishExaminationSeatingAsync(exam.Id, new() { NotifyParents = true });
			(await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == exam.Id)).ShouldBeEmpty();
			GetRequiredService<AcademicNotificationCatalogue>().GetAvailability(AcademicNotificationEventKeys.SeatingChanged)
				.IsAvailable.ShouldBeFalse();
		});
	}

	private async Task WithSeatingNotificationTransactionAsync(Func<Task> action)
	{
		// The shared application fixture forces its test manager to begin nontransactional work.
		var manager = ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider);
		using var unit = manager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		await action();
		await unit.CompleteAsync();
	}

	private async Task PublishSeatingSubjectsAsync(Exam exam)
	{
		foreach (var subject in exam.ExamSubjects)
			await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(new(Guid.NewGuid(),
				TestTenantId, exam.StandardId, subject.SubjectId, $"SEAT-{subject.SubjectId:N}"[..15], SubjectStatus.Published), autoSave: true);
	}

	private async Task AddSeatingRuleAsync((string Recipient, CommunicationChannelType Channel)[] variants, bool includeStudent = false)
	{
		const string eventKey = AcademicNotificationEventKeys.SeatingPublished;
		var definitions = GetRequiredService<AcademicNotificationCatalogue>();
		var headers = GetRequiredService<IRepository<CommunicationTemplate, Guid>>();
		var revisions = GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>();
		var ids = new List<Guid>();
		foreach (var (recipient, channel) in variants)
		{
			var templateId = Guid.NewGuid();
			ids.Add(templateId);
			await headers.InsertAsync(new(templateId, TestTenantId)
				{ TemplateName = $"Seating {recipient} {channel}" }, autoSave: true);
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TestTenantId, templateId, 1, eventKey, recipient,
				channel, "en", "Exam seating", "Your seat is {{seat_number}}.", 1);
			revision.Publish(FakeAdminUserId, DateTimeOffset.UtcNow);
			await revisions.InsertAsync(revision, autoSave: true);
		}
		var rule = new NotificationRule(Guid.NewGuid(), TestTenantId, eventKey, NotificationRuleScope.Create());
		var settings = new NotificationRuleRuntimeConfiguration(1,
			includeStudent ? NotificationGuardianMode.PrimaryAndSelected : NotificationGuardianMode.PrimaryOnly,
			includeStudent ? ["mother"] : [], includeStudent, [], variants.Select(x => x.Channel).Distinct().ToArray(), ids,
			NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
		rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>()
			.Serialize(settings, definitions.Get(eventKey)), rule.ConcurrencyStamp);
		await GetRequiredService<IRepository<NotificationRule, Guid>>().InsertAsync(rule, autoSave: true);
	}
}
