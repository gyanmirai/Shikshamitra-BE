using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Follow_up_note_should_support_inquiries_without_optional_intake_settings(bool legacyInquiry) =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));
			var states = GetRequiredService<IRepository<AdmissionInquiryManagement, Guid>>();
			if (legacyInquiry)
			{
				// Historical rows predate management state; seed that shape without deleting a current aggregate.
				var historical = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, inquiry.InquiryNumber + "-OLD",
					inquiry.AdmissionCycleId, offering.Id, inquiry.ApplicantName, inquiry.NormalizedPhone, inquiry.Email,
					AdmissionInquirySource.Website, null);
				historical.LinkIdentity(inquiry.FamilyId!.Value, inquiry.ApplicantId!.Value);
				await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(historical, autoSave: true);
				inquiry = await inquiries.GetAsync(historical.Id);
			}
			(await states.GetListAsync(item => item.InquiryId == inquiry.Id)).Count.ShouldBe(legacyInquiry ? 0 : 1);
			inquiry = await inquiries.GetAsync(inquiry.Id);
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await service.GetAsync(inquiry.Id);
			// Initialization is a persisted aggregate change; read the inquiry stamp after management is ready.
			inquiry = await inquiries.GetAsync(inquiry.Id);
			state.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace();
			state.Policy!.FirstContactDueInHours.ShouldBeNull();
			state.Policy.QualificationDueInDays.ShouldBeNull();
			state.Policy.RestrictActionsToAssignedCounsellor.ShouldBeFalse();
			(await service.GetAsync(inquiry.Id)).ConcurrencyStamp.ShouldBe(state.ConcurrencyStamp);
			(await states.GetListAsync(item => item.InquiryId == inquiry.Id)).Count.ShouldBe(1);
			var reminder = DateTimeOffset.UtcNow.AddDays(2);
			await service.RecordFollowUpNoteAsync(inquiry.Id, NoteInput(inquiry, state, AdmissionInquiryStatus.Interested, reminder));
			state = await service.GetAsync(inquiry.Id);
			state.Notes.Single().Status.ShouldBe(AdmissionInquiryStatus.Interested);
			state.NextReminderAt.ShouldBe(reminder);
			state.FollowUps.Single().AssignedCounsellorId.ShouldBeNull();
			inquiry = await inquiries.GetAsync(inquiry.Id);
			inquiry.Status.ShouldBe(AdmissionInquiryStatus.Interested);
			await service.RecordFollowUpNoteAsync(inquiry.Id, NoteInput(inquiry, state, AdmissionInquiryStatus.NotInterested, null));
			state = await service.GetAsync(inquiry.Id);
			state.Notes.Count.ShouldBe(2);
			state.NextReminderAt.ShouldBeNull();
			state.FollowUps.Single().Status.ShouldBe(AdmissionInquiryFollowUpStatus.Cancelled);
		});

	[Theory]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void Follow_up_note_scheduled_at_converter_should_bind_ist_midnight_as_an_absolute_instant(DateTimeKind kind)
	{
		var selected = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.FromMinutes(330));
		var utc = selected.UtcDateTime;
		var stored = kind == DateTimeKind.Local ? utc.ToLocalTime() : DateTime.SpecifyKind(utc, kind);
		var builder = new ModelBuilder();
		new AdmissionInquiryManagementConfiguration().Configure(builder.Entity<AdmissionInquiryFollowUp>());
		var converter = builder.Entity<AdmissionInquiryFollowUp>().Metadata.FindProperty(nameof(AdmissionInquiryFollowUp.ScheduledAt))!.GetValueConverter()!;
		converter.ProviderClrType.ShouldBe(typeof(DateTimeOffset));
		var provider = (DateTimeOffset)converter.ConvertToProvider(stored)!;
		provider.ShouldBe(selected.ToUniversalTime());
		provider.Offset.ShouldBe(TimeSpan.Zero);
		var readBack = (DateTime)converter.ConvertFromProvider(provider.ToOffset(TimeSpan.FromMinutes(330)))!;
		readBack.ShouldBe(utc);
		readBack.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Theory]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void Follow_up_note_clock_should_allow_next_ist_midnight_after_late_local_evening(DateTimeKind clockKind)
	{
		var evening = new DateTimeOffset(2026, 10, 8, 22, 0, 0, TimeSpan.FromMinutes(330));
		var localNow = evening.UtcDateTime.ToLocalTime();
		var clockNow = DateTime.SpecifyKind(localNow, clockKind);
		var utcNow = AdmissionInquiryManagementAppService.NormalizeFollowUpClockNow(clockNow, clockKind);
		utcNow.ShouldBe(evening.UtcDateTime);
		var nextMidnight = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.FromMinutes(330));
		nextMidnight.UtcDateTime.ShouldBeGreaterThan(utcNow);
	}

	[Theory]
	[InlineData("InquiryFollowUpScheduled")]
	[InlineData("InquiryFollowUpNoteRecorded")]
	public void Follow_up_note_next_reminder_should_recover_only_the_exact_tasks_original_offset_snapshot(string activityType)
	{
		var taskId = Guid.NewGuid();
		var original = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.FromMinutes(330));
		var shifted = original.UtcDateTime.AddMinutes(-330);
		var metadata = activityType == "InquiryFollowUpScheduled"
			? JsonSerializer.Serialize(new { id = taskId, scheduledAt = original.UtcDateTime })
			: JsonSerializer.Serialize(new { followUpId = taskId, reminderAt = original });
		var task = new AdmissionInquiryFollowUpDto { Id = taskId, Status = AdmissionInquiryFollowUpStatus.Scheduled, ScheduledAt = shifted };
		var activity = new AdmissionActivity(Guid.NewGuid(), null, activityType, "Recorded", metadataJson: metadata);
		AdmissionInquiryManagementAppService.FindNextReminderAt([task], [activity]).ShouldBe(original.ToUniversalTime());
		activity = new AdmissionActivity(Guid.NewGuid(), null, activityType, "Recorded", metadataJson: "{\"id\":\"" + taskId + "\",\"scheduledAt\":\"2026-10-08T18:30:00\"}");
		AdmissionInquiryManagementAppService.FindNextReminderAt([task], [activity]).ShouldBe(new DateTimeOffset(shifted));
		task.Id = Guid.NewGuid();
		activity = new AdmissionActivity(Guid.NewGuid(), null, activityType, "Recorded", metadataJson: metadata);
		AdmissionInquiryManagementAppService.FindNextReminderAt([task], [activity]).ShouldBe(new DateTimeOffset(shifted));
		task.Id = taskId;
		task.Status = AdmissionInquiryFollowUpStatus.Cancelled;
		AdmissionInquiryManagementAppService.FindNextReminderAt([task], [activity]).ShouldBeNull();
	}
	[Theory]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void Follow_up_note_next_reminder_should_normalize_all_persisted_datetime_kinds_to_utc(DateTimeKind kind)
	{
		var utc = new DateTime(2026, 10, 7, 18, 30, 0, DateTimeKind.Utc);
		var stored = kind == DateTimeKind.Local ? utc.ToLocalTime() : DateTime.SpecifyKind(utc, kind);
		var next = AdmissionInquiryManagementAppService.FindNextReminderAt([
			new() { Status = AdmissionInquiryFollowUpStatus.Completed, ScheduledAt = utc.AddDays(-1) },
			new() { Status = AdmissionInquiryFollowUpStatus.Cancelled, ScheduledAt = utc.AddHours(-1) },
			new() { Status = AdmissionInquiryFollowUpStatus.Scheduled, ScheduledAt = stored },
			new() { Status = AdmissionInquiryFollowUpStatus.Scheduled, ScheduledAt = utc.AddDays(1) }
		]);
		next.ShouldBe(new DateTimeOffset(utc));
		next!.Value.Offset.ShouldBe(TimeSpan.Zero);
		next.Value.ToOffset(TimeSpan.FromMinutes(330)).Date.ShouldBe(new DateTime(2026, 10, 8));
	}

	[Fact]
	public Task Follow_up_note_should_accept_a_near_future_absolute_reminder_with_the_default_local_clock() =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var reminder = DateTimeOffset.UtcNow.AddMinutes(30);
			var state = await service.RecordFollowUpNoteAsync(inquiry.Id,
				NoteInput(inquiry, await service.GetAsync(inquiry.Id), AdmissionInquiryStatus.New, reminder));
			state.NextReminderAt.ShouldBe(reminder);
			state.FollowUps.Single().ScheduledAt.ShouldBe(reminder.UtcDateTime);
		});

	[Fact]
	public Task Follow_up_note_next_reminder_should_roundtrip_selected_ist_midnight_and_clear_on_closure() =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await service.GetAsync(inquiry.Id);
			var selected = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2), DateTimeKind.Unspecified), TimeSpan.FromMinutes(330));
			state = await service.RecordFollowUpNoteAsync(inquiry.Id, NoteInput(inquiry, state, AdmissionInquiryStatus.New, selected));
			state.NextReminderAt.ShouldBe(selected.ToUniversalTime());
			state = await service.GetAsync(inquiry.Id);
			state.NextReminderAt.ShouldBe(selected.ToUniversalTime());
			state.NextReminderAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
			var json = JsonSerializer.Serialize(state);
			using var document = JsonDocument.Parse(json);
			var serialized = document.RootElement.GetProperty(nameof(AdmissionInquiryManagementDto.NextReminderAt)).GetDateTimeOffset();
			serialized.Offset.ShouldBe(TimeSpan.Zero);
			serialized.ToOffset(TimeSpan.FromMinutes(330)).Date.ShouldBe(selected.Date);
			inquiry = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(inquiry.Id);
			state = await service.RecordFollowUpNoteAsync(inquiry.Id, NoteInput(inquiry, state, AdmissionInquiryStatus.NotInterested, null));
			state.NextReminderAt.ShouldBeNull();
			(await service.GetAsync(inquiry.Id)).NextReminderAt.ShouldBeNull();
		});

	[Fact]
	public Task Follow_up_note_should_atomically_persist_interest_reminder_author_and_first_email_intent() =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await service.GetAsync(inquiry.Id);
			var reminder = DateTimeOffset.UtcNow.AddDays(2);
			var recorded = await service.RecordFollowUpNoteAsync(inquiry.Id, NoteInput(inquiry, state, AdmissionInquiryStatus.Interested, reminder));
			recorded.Notes.Count.ShouldBe(1);
			var note = recorded.Notes.Single();
			note.Note.ShouldBe("Guardian confirmed a school visit.");
			note.Status.ShouldBe(AdmissionInquiryStatus.Interested);
			note.ReminderAt.ShouldBe(reminder);
			note.AuthorName.ShouldNotBeNullOrWhiteSpace();
			recorded.FollowUps.Single().AssignedCounsellorId.ShouldBeNull();
			recorded.FollowUps.Single().ScheduledAt.ShouldBe(reminder.UtcDateTime);
			var noteActivity = (await GetRequiredService<IRepository<AdmissionActivity, Guid>>().GetListAsync(item => item.InquiryId == inquiry.Id && item.ActivityType == "InquiryFollowUpNoteRecorded")).Single();
			using var metadata = JsonDocument.Parse(noteActivity.MetadataJson!);
			metadata.RootElement.GetProperty("followUpId").GetGuid().ShouldBe(recorded.FollowUps.Single().Id);
			var saved = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(inquiry.Id);
			saved.Status.ShouldBe(AdmissionInquiryStatus.Interested);
			saved.GetProperty<DateTime?>("QualificationEmailRequestedAt").ShouldNotBeNull();
			var firstIntent = saved.GetProperty<DateTime?>("QualificationEmailRequestedAt");
			inquiry = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(inquiry.Id);
			state = await service.RecordFollowUpNoteAsync(inquiry.Id, NoteInput(inquiry, recorded, AdmissionInquiryStatus.Interested, reminder.AddDays(1)));
			state.Notes.Count.ShouldBe(2);
			state.FollowUps.Count(item => item.Status == AdmissionInquiryFollowUpStatus.Scheduled).ShouldBe(1);
			state.FollowUps.Count(item => item.Status == AdmissionInquiryFollowUpStatus.Cancelled).ShouldBe(1);
			(await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(inquiry.Id)).GetProperty<DateTime?>("QualificationEmailRequestedAt").ShouldBe(firstIntent);
			(await service.GetAsync(inquiry.Id)).Notes.Count.ShouldBe(2);
		});

	[Fact]
	public Task Follow_up_note_not_interested_should_record_closure_without_reminder() =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var before = await service.GetAsync(inquiry.Id);
			before = await service.ScheduleFollowUpAsync(inquiry.Id, new() { ConcurrencyStamp = before.ConcurrencyStamp, ScheduledAt = DateTimeOffset.UtcNow.AddDays(1), Purpose = "Earlier reminder" });
			inquiry = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(inquiry.Id);
			var state = await service.RecordFollowUpNoteAsync(inquiry.Id,
				NoteInput(inquiry, before, AdmissionInquiryStatus.NotInterested, DateTimeOffset.UtcNow.AddDays(1)));
			state.FollowUps.Single().Status.ShouldBe(AdmissionInquiryFollowUpStatus.Cancelled);
			state.FollowUps.Single().CompletionNote.ShouldNotBeNull().ShouldContain("Not interested");
			state.Notes.Single().Status.ShouldBe(AdmissionInquiryStatus.NotInterested);
			state.Notes.Single().ReminderAt.ShouldBeNull();
			(await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(inquiry.Id)).Status.ShouldBe(AdmissionInquiryStatus.NotInterested);
		});

	[Theory]
	[InlineData(-1)]
	[InlineData(366)]
	public Task Follow_up_note_invalid_reminder_should_not_change_status_or_persist_any_note(int reminderDays) =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await service.GetAsync(inquiry.Id);
			(await Should.ThrowAsync<BusinessException>(() => service.RecordFollowUpNoteAsync(inquiry.Id,
				NoteInput(inquiry, state, AdmissionInquiryStatus.Interested, DateTimeOffset.UtcNow.AddDays(reminderDays)))))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
			var saved = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(inquiry.Id);
			saved.Status.ShouldBe(AdmissionInquiryStatus.New);
			saved.GetProperty<DateTime?>("QualificationEmailRequestedAt").ShouldBeNull();
			var reread = await service.GetAsync(inquiry.Id);
			reread.Notes.ShouldBeEmpty();
			reread.FollowUps.ShouldBeEmpty();
			reread.ConcurrencyStamp.ShouldBe(state.ConcurrencyStamp);
		});

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, false)]
	[InlineData(true, true)]
	[InlineData(false, true)]
	public Task Follow_up_note_should_reject_either_stale_concurrency_stamp(bool staleInquiry, bool basicInquiry) =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync(basicInquiry);
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var input = NoteInput(inquiry, await service.GetAsync(inquiry.Id), AdmissionInquiryStatus.Interested, DateTimeOffset.UtcNow.AddDays(1));
			if (staleInquiry) input.InquiryConcurrencyStamp = "stale"; else input.ConcurrencyStamp = "stale";
			(await Should.ThrowAsync<BusinessException>(() => service.RecordFollowUpNoteAsync(inquiry.Id, input)))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
			(await service.GetAsync(inquiry.Id)).Notes.ShouldBeEmpty();
			(await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(inquiry.Id)).Status.ShouldBe(AdmissionInquiryStatus.New);
		});

	[Fact]
	public Task Follow_up_note_should_reject_transferred_and_duplicate_inquiries() =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
			await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, inquiry.AdmissionOfferingId!.Value, inquiry.Id);
			(await Should.ThrowAsync<BusinessException>(async () => await service.RecordFollowUpNoteAsync(inquiry.Id,
				NoteInput(inquiry, await service.GetAsync(inquiry.Id), AdmissionInquiryStatus.New, DateTimeOffset.UtcNow.AddDays(1)))))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
			var duplicate = await CreateFollowUpNoteInquiryAsync();
			var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var entity = await repository.GetAsync(duplicate.Id);
			entity.SetStatus(AdmissionInquiryStatus.Duplicate, inquiry.Id);
			await repository.UpdateAsync(entity, true);
			duplicate = await inquiries.GetAsync(duplicate.Id);
			(await Should.ThrowAsync<BusinessException>(async () => await service.RecordFollowUpNoteAsync(duplicate.Id,
				NoteInput(duplicate, await service.GetAsync(duplicate.Id), AdmissionInquiryStatus.New, DateTimeOffset.UtcNow.AddDays(1)))))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
		});

	[Fact]
	public Task Follow_up_note_should_replace_existing_reminders_at_the_limit_without_changing_general_schedule_limit() =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await service.GetAsync(inquiry.Id);
			for (var index = 0; index < state.Policy!.MaxOpenFollowUps; index++)
				state = await service.ScheduleFollowUpAsync(inquiry.Id, new()
				{
					ConcurrencyStamp = state.ConcurrencyStamp, ScheduledAt = DateTimeOffset.UtcNow.AddDays(index + 1), Purpose = "Existing reminder"
				});
			(await Should.ThrowAsync<BusinessException>(() => service.ScheduleFollowUpAsync(inquiry.Id,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, ScheduledAt = DateTimeOffset.UtcNow.AddDays(5), Purpose = "Excess general reminder" })))
				.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.FollowUpLimitReached);
			var priorCount = state.FollowUps.Count;
			inquiry = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(inquiry.Id);
			state = await service.RecordFollowUpNoteAsync(inquiry.Id, NoteInput(inquiry, state, AdmissionInquiryStatus.Interested, DateTimeOffset.UtcNow.AddDays(5)));
			state.FollowUps.Count(item => item.Status == AdmissionInquiryFollowUpStatus.Scheduled).ShouldBe(1);
			state.FollowUps.Count(item => item.Status == AdmissionInquiryFollowUpStatus.Cancelled).ShouldBe(priorCount);
			state.Notes.Count.ShouldBe(1);
			inquiry = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(inquiry.Id);
			state = await service.RecordFollowUpNoteAsync(inquiry.Id, NoteInput(inquiry, state, AdmissionInquiryStatus.Interested, DateTimeOffset.UtcNow.AddDays(6)));
			state.FollowUps.Count(item => item.Status == AdmissionInquiryFollowUpStatus.Scheduled).ShouldBe(1);
			state.Notes.Count.ShouldBe(2);
		});

	[Fact]
	public Task Follow_up_note_should_ignore_retired_assigned_actor_restrictions() =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var entity = await repository.GetAsync(inquiry.Id);
			entity.AssignCounsellor(Guid.NewGuid());
			await repository.UpdateAsync(entity, true);
			var states = GetRequiredService<IRepository<AdmissionInquiryManagement, Guid>>();
			var state = await states.FindAsync(item => item.InquiryId == inquiry.Id);
			var policy = AdmissionInquiryPolicyResolver.ReadSnapshot(state!.PolicySnapshotJson) with { RestrictActionsToAssignedCounsellor = true };
			state.UpdatePolicy(state.StageDefinitionId, JsonSerializer.Serialize(policy), entity.CreationTime, policy.FirstContactDueInHours, policy.QualificationDueInDays);
			await states.UpdateAsync(state, true);
			inquiry = await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(inquiry.Id);
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			await service.RecordFollowUpNoteAsync(inquiry.Id,
				NoteInput(inquiry, await service.GetAsync(inquiry.Id), AdmissionInquiryStatus.Interested, DateTimeOffset.UtcNow.AddDays(1)));
			(await service.GetAsync(inquiry.Id)).Notes.ShouldHaveSingleItem();
			(await repository.GetAsync(inquiry.Id)).Status.ShouldBe(AdmissionInquiryStatus.Interested);
		});

	[Fact]
	public void Follow_up_note_validator_should_reject_blank_long_legacy_status_and_missing_reminder()
	{
		var validator = new RecordAdmissionInquiryFollowUpNoteDtoValidator();
		var input = new RecordAdmissionInquiryFollowUpNoteDto
		{
			ConcurrencyStamp = "management", InquiryConcurrencyStamp = "inquiry", Note = "Note", Status = AdmissionInquiryStatus.New, ReminderAt = DateTimeOffset.UtcNow.AddDays(1)
		};
		validator.Validate(input).IsValid.ShouldBeTrue();
		input.Note = "   "; validator.Validate(input).IsValid.ShouldBeFalse();
		input.Note = new string('a', 501); validator.Validate(input).IsValid.ShouldBeFalse();
		input.Note = "Note"; input.Status = AdmissionInquiryStatus.FollowUp; validator.Validate(input).IsValid.ShouldBeFalse();
		input.Status = AdmissionInquiryStatus.Interested; input.ReminderAt = null; validator.Validate(input).IsValid.ShouldBeFalse();
		input.Status = AdmissionInquiryStatus.NotInterested; validator.Validate(input).IsValid.ShouldBeTrue();
	}

	[Fact]
	public Task Follow_up_note_should_preserve_legacy_note_without_inventing_status() =>
		RunCertificationScenarioAsync(async () =>
		{
			var inquiry = await CreateFollowUpNoteInquiryAsync();
			var service = GetRequiredService<IAdmissionInquiryManagementAppService>();
			var state = await service.GetAsync(inquiry.Id);
			var result = await service.RecordContactAsync(inquiry.Id, new()
			{
				ConcurrencyStamp = state.ConcurrencyStamp, Channel = "PHONE", Direction = "OUTBOUND", Outcome = "CONNECTED", Note = "Historical contact note."
			});
			result.Notes.Single().Note.ShouldBe("Historical contact note.");
			result.Notes.Single().Status.ShouldBeNull();
			result.Notes.Single().AuthorName.ShouldBe("Staff");
		});

	private async Task<AdmissionInquiryDto> CreateFollowUpNoteInquiryAsync(bool basicInquiry = false)
	{
		var offering = await CreateOptionalIntakeOfferingAsync(legacyPolicy: !basicInquiry);
		var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
		var inquiry = await inquiries.CreateAsync(ManagedInquiryInput(offering));
		var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
		var entity = await repository.GetAsync(inquiry.Id);
		entity.AssignCounsellor(TestUserId);
		await repository.UpdateAsync(entity, true);
		return await inquiries.GetAsync(inquiry.Id);
	}

	private static RecordAdmissionInquiryFollowUpNoteDto NoteInput(AdmissionInquiryDto inquiry, AdmissionInquiryManagementDto state,
		AdmissionInquiryStatus status, DateTimeOffset? reminderAt) => new()
	{
		ConcurrencyStamp = state.ConcurrencyStamp, InquiryConcurrencyStamp = inquiry.ConcurrencyStamp,
		Note = "  Guardian confirmed a school visit.  ", Status = status, ReminderAt = reminderAt
	};
}
