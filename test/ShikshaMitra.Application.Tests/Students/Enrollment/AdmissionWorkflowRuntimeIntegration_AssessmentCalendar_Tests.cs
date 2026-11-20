using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(true, true, false)]
	[InlineData(false, true, false)]
	[InlineData(true, false, false)]
	[InlineData(false, false, false)]
	[InlineData(true, true, true)]
	public async Task Assessment_calendar_scope_ignores_another_medium_or_year_through_confirmation_and_reschedule(bool otherMedium, bool matchingCalendar, bool legacyCalendar)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var input = await CalendarAssessmentInputAsync(fixture.Offering);
		var yearId = (await _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId)).AcademicYearId;
		var date = input.StartsAt.UtcDateTime.Date;
		// Insert the unrelated published calendar first, as in the reported cross-Medium failure.
		var unrelated = await SeedAssessmentCalendarAsync(otherMedium ? Guid.NewGuid() : fixture.Offering.MediumId,
			legacyCalendar ? null : otherMedium ? yearId : Guid.NewGuid(), date);
		await SeedAssessmentCalendarDayAsync(unrelated, date, DayType.Vacation);
		await SeedAssessmentCalendarDayAsync(unrelated, date.AddDays(1), DayType.Vacation);
		if (matchingCalendar) await SeedAssessmentCalendarAsync(fixture.Offering.MediumId, yearId, date);

		var preview = await service.PreviewSessionAsync(new() { Session = input });
		preview.ProposedBookings.Count.ShouldBe(1);
		var saved = await service.ConfirmSessionAsync(CalendarConfirmation(input, preview));
		saved.BookedCount.ShouldBe(1);
		var reservation = await GetRequiredService<IRepository<RoomReservation, Guid>>().GetAsync(saved.Session.RoomReservationId!.Value);
		reservation.Status.ShouldBe(RoomReservationStatus.Approved);

		input.StartsAt = input.StartsAt.AddDays(1);
		input.EndsAt = input.EndsAt.AddDays(1);
		input.ConcurrencyStamp = saved.Session.ConcurrencyStamp;
		var moved = await service.UpdateSessionAsync(saved.Session.Id, input);
		moved.BookedCount.ShouldBe(1);
		moved.RoomReservationId.ShouldNotBe(saved.Session.RoomReservationId);
		(await GetRequiredService<IRepository<RoomReservation, Guid>>().GetAsync(reservation.Id)).Status.ShouldBe(RoomReservationStatus.Cancelled);
	}

	[Theory]
	[InlineData(DayType.Vacation, false)]
	[InlineData(DayType.Holiday, false)]
	[InlineData(DayType.EmergencyClosure, false)]
	[InlineData(DayType.Vacation, true)]
	public async Task Assessment_scheduling_ignores_academic_closures_while_generic_room_checks_still_block(DayType dayType, bool legacyCalendar)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var input = await CalendarAssessmentInputAsync(fixture.Offering);
		var yearId = (await _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId)).AcademicYearId;
		var calendar = await SeedAssessmentCalendarAsync(fixture.Offering.MediumId, legacyCalendar ? null : yearId, input.StartsAt.UtcDateTime.Date);
		await SeedAssessmentCalendarDayAsync(calendar, input.StartsAt.UtcDateTime.Date, dayType);

		await WithUnitOfWorkAsync(async () =>
		{
			var scope = (await GetRequiredService<AdmissionAssessmentCalendarScopeService>().GetAsync([fixture.Offering])).Single();
			var window = AdmissionAssessmentSchedulingService.SchoolWindow(input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime);
			var ordinary = await GetRequiredService<RoomConflictService>().CheckScopedConflictsAsync(scope,
				input.RoomId!.Value, window.Date, window.Start, window.End);
			ordinary.HolidayCalendar.Passed.ShouldBeFalse();
		});
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var preview = await service.PreviewSessionAsync(new() { Session = input });
		var saved = await service.ConfirmSessionAsync(CalendarConfirmation(input, preview));
		saved.BookedCount.ShouldBe(1);
		(await service.GetAsync(fixture.Application.Id, fixture.StageId)).Attempts.Count.ShouldBe(1);
		await SeedAssessmentCalendarDayAsync(calendar, input.StartsAt.UtcDateTime.Date.AddDays(1), dayType);
		input.StartsAt = input.StartsAt.AddDays(1);
		input.EndsAt = input.EndsAt.AddDays(1);
		input.ConcurrencyStamp = saved.Session.ConcurrencyStamp;
		var moved = await service.UpdateSessionAsync(saved.Session.Id, input);
		moved.RoomReservationId.ShouldNotBe(saved.Session.RoomReservationId);
	}

	[Fact]
	public async Task Assessment_confirmation_is_not_blocked_by_a_closure_added_after_preview()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var input = await CalendarAssessmentInputAsync(fixture.Offering);
		var yearId = (await _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId)).AcademicYearId;
		var calendar = await SeedAssessmentCalendarAsync(fixture.Offering.MediumId, yearId, input.StartsAt.UtcDateTime.Date);
		var preview = await service.PreviewSessionAsync(new() { Session = input });
		await SeedAssessmentCalendarDayAsync(calendar, input.StartsAt.UtcDateTime.Date, DayType.Vacation);

		var saved = await service.ConfirmSessionAsync(CalendarConfirmation(input, preview));
		saved.BookedCount.ShouldBe(1);
		(await service.GetAsync(fixture.Application.Id, fixture.StageId)).Attempts.Count.ShouldBe(1);
		(await GetRequiredService<IRepository<RoomReservation, Guid>>().GetAsync(saved.Session.RoomReservationId!.Value))
			.Status.ShouldBe(RoomReservationStatus.Approved);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Shared_assessment_session_is_not_blocked_by_any_linked_class_academic_vacation(bool otherMedium)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var second = await CreateAvailableOfferingAsync(first.Offering.WorkflowDefinitionId!.Value);
		var (standardId, mediumId) = await WithUnitOfWorkAsync(async () =>
		{
			var offering = await _offeringRepository.GetAsync(second.Id);
			var targetMediumId = offering.MediumId;
			var targetStageId = offering.StageId;
			if (otherMedium)
			{
				var medium = await GetRequiredService<EducationMediumManager>().CreateAsync(offering.BoardId, "Gujarati");
				await _mediumRepository.InsertAsync(medium, true);
				var stage = await GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary");
				await _stageRepository.InsertAsync(stage, true);
				targetMediumId = medium.Id;
				targetStageId = stage.Id;
			}
			var standard = await _standardManager.CreateAsync(targetStageId, "Second assessment class", 20, null);
			await _standardRepository.InsertAsync(standard, true);
			offering.UpdateIdentity(offering.CampusId, offering.BoardId, targetMediumId, targetStageId, standard.Id, false);
			await _offeringRepository.UpdateAsync(offering, true);
			return (standard.Id, targetMediumId);
		});
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var input = await CalendarAssessmentInputAsync(first.Offering);
		var yearId = (await _cycleRepository.GetAsync(first.Offering.AdmissionCycleId)).AcademicYearId;
		var calendar = await SeedAssessmentCalendarAsync(first.Offering.MediumId, yearId, input.StartsAt.UtcDateTime.Date);
		if (otherMedium) calendar = await SeedAssessmentCalendarAsync(mediumId, yearId, input.StartsAt.UtcDateTime.Date);
		await SeedAssessmentCalendarDayAsync(calendar, input.StartsAt.UtcDateTime.Date, DayType.Vacation, standardId);
		(await service.PreviewSessionAsync(new() { Session = input })).EligibleCount.ShouldBe(1);
		var scope = (await service.GetScopesAsync()).Single(row => row.AdmissionOfferingId == second.Id);
		input.Scopes.Add(new() { AdmissionOfferingId = second.Id, WorkflowStageDefinitionId = scope.WorkflowStageDefinitionId });

		var preview = await service.PreviewSessionAsync(new() { Session = input });
		var saved = await service.ConfirmSessionAsync(CalendarConfirmation(input, preview));
		saved.BookedCount.ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Linked_assessment_room_approval_ignores_academic_vacation(bool genuineVacation)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var input = await CalendarAssessmentInputAsync(fixture.Offering);
		var saved = await GetRequiredService<IAdmissionAssessmentAppService>().CreateSessionAsync(input);
		var window = AdmissionAssessmentSchedulingService.SchoolWindow(input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime);
		var reservationId = await WithUnitOfWorkAsync(async () =>
		{
			var reservations = GetRequiredService<IRepository<RoomReservation, Guid>>();
			var original = await reservations.GetAsync(saved.RoomReservationId!.Value);
			original.Cancel(TestUserId, "Pending approval fixture");
			await reservations.UpdateAsync(original, true);
			var pending = await reservations.InsertAsync(new RoomReservation(Guid.NewGuid(), TestTenantId, input.RoomId!.Value,
				window.Date, window.Start, window.End, input.Name, RoomReservationType.Exam, standardId: fixture.Offering.StandardId), true);
			var sessions = GetRequiredService<IRepository<AdmissionAssessmentSession, Guid>>();
			var session = (await sessions.WithDetailsAsync(row => row.Scopes)).Single(row => row.Id == saved.Id);
			session.ConfigurePlanning("exam", input.RoomId.Value, pending.Id, null, null, null, session.Scopes);
			await sessions.UpdateAsync(session, true);
			return pending.Id;
		});
		var yearId = (await _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId)).AcademicYearId;
		var unrelated = await SeedAssessmentCalendarAsync(Guid.NewGuid(), yearId, window.Date);
		await SeedAssessmentCalendarDayAsync(unrelated, window.Date, DayType.Vacation);
		var correct = await SeedAssessmentCalendarAsync(fixture.Offering.MediumId, yearId, window.Date);
		if (genuineVacation) await SeedAssessmentCalendarDayAsync(correct, window.Date, DayType.Vacation);
		var approverId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(approverId, "calendar-approver");
		await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
			userId: approverId, description: "Calendar room approval test"), true);
		using var approver = _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, approverId.ToString()), new Claim(AbpClaimTypes.Role, "admin")], "Test")));
		var service = GetRequiredService<IRoomAvailabilityAppService>();
		await service.ApproveReservationAsync(reservationId);
		(await GetRequiredService<IRepository<RoomReservation, Guid>>().GetAsync(reservationId)).Status.ShouldBe(RoomReservationStatus.Approved);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Assessment_confirmation_still_rejects_physical_room_conflicts_on_vacation(bool maintenance)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var input = await CalendarAssessmentInputAsync(fixture.Offering);
		var yearId = (await _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId)).AcademicYearId;
		var calendar = await SeedAssessmentCalendarAsync(fixture.Offering.MediumId, yearId, input.StartsAt.UtcDateTime.Date);
		await SeedAssessmentCalendarDayAsync(calendar, input.StartsAt.UtcDateTime.Date, DayType.Vacation);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var preview = await service.PreviewSessionAsync(new() { Session = input });
		var window = AdmissionAssessmentSchedulingService.SchoolWindow(input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime);
		await WithUnitOfWorkAsync(async () =>
		{
			if (maintenance)
				await GetRequiredService<IRepository<RoomMaintenanceBlock, Guid>>().InsertAsync(new RoomMaintenanceBlock(
					Guid.NewGuid(), TestTenantId, input.RoomId!.Value, "Room repairs", "QA conflict", MaintenanceSeverity.Low,
					window.Date.Add(window.Start), window.Date.Add(window.End)), true);
			else
				await GetRequiredService<IRepository<RoomReservation, Guid>>().InsertAsync(new RoomReservation(
					Guid.NewGuid(), TestTenantId, input.RoomId!.Value, window.Date, window.Start, window.End,
					"Another booking", RoomReservationType.Exam, standardId: fixture.Offering.StandardId), true);
		});
		var error = await Should.ThrowAsync<BusinessException>(() => service.ConfirmSessionAsync(CalendarConfirmation(input, preview)));
		error.Data["Reason"].ShouldBeOfType<string>().ShouldContain(maintenance ? "maintenance" : "Room is reserved");
		(await service.GetAsync(fixture.Application.Id, fixture.StageId)).Attempts.ShouldBeEmpty();
	}

	private async Task<AdmissionAssessmentSessionInput> CalendarAssessmentInputAsync(AdmissionOffering offering)
	{
		var scope = (await GetRequiredService<IAdmissionAssessmentAppService>().GetScopesAsync()).Single(row => row.AdmissionOfferingId == offering.Id);
		var room = await CreateAssessmentPlanningRoomAsync();
		var date = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(30), TimeSpan.Zero);
		return new() { Name = "Calendar scoped entrance exam", RoomId = room.Id, Capacity = 2,
			StartsAt = date.AddHours(3), EndsAt = date.AddHours(4),
			Scopes = [new() { AdmissionOfferingId = offering.Id, WorkflowStageDefinitionId = scope.WorkflowStageDefinitionId }] };
	}

	private static AdmissionAssessmentSessionConfirmInput CalendarConfirmation(AdmissionAssessmentSessionInput input, AdmissionAssessmentSessionPreviewDto preview) =>
		new() { Session = input, IdempotencyKey = Guid.NewGuid(), PreviewFingerprint = preview.PreviewFingerprint,
			Bookings = preview.ProposedBookings.Select(row => new AdmissionAssessmentSelectedBookingInput {
				ApplicationId = row.ApplicationId, StageInstanceId = row.StageInstanceId, ConcurrencyStamp = row.ConcurrencyStamp }).ToList() };

	[Fact]
	public async Task Assessment_calendar_scope_does_not_infer_a_legacy_calendar_year_from_date_alone()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var fixture = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var input = await CalendarAssessmentInputAsync(fixture.Offering);
		var calendar = await SeedAssessmentCalendarAsync(fixture.Offering.MediumId, null, input.StartsAt.UtcDateTime.Date);
		await SeedAssessmentCalendarDayAsync(calendar, input.StartsAt.UtcDateTime.Date, DayType.Vacation);
		await WithUnitOfWorkAsync(async () =>
		{
			var scope = (await GetRequiredService<AdmissionAssessmentCalendarScopeService>().GetAsync([fixture.Offering])).Single();
			var anotherYear = scope with { AcademicYearId = Guid.NewGuid() };
			var window = AdmissionAssessmentSchedulingService.SchoolWindow(input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime);
			var result = await GetRequiredService<RoomConflictService>().CheckScopedConflictsAsync(anotherYear,
				input.RoomId!.Value, window.Date, window.Start, window.End);
			result.HolidayCalendar.Passed.ShouldBeTrue();
		});
	}

	private Task<AcademicCalendar> SeedAssessmentCalendarAsync(Guid mediumId, Guid? yearId, DateTime date) => WithUnitOfWorkAsync(async () =>
	{
		var calendar = new AcademicCalendar(Guid.NewGuid(), TestTenantId, mediumId, yearId, "Assessment calendar", date.AddDays(-7), date.AddDays(7));
		calendar.Publish(TestUserId);
		return await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, true);
	});

	private Task SeedAssessmentCalendarDayAsync(AcademicCalendar calendar, DateTime date, DayType type, Guid? standardId = null) => WithUnitOfWorkAsync(async () =>
	{
		await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(new AcademicCalendarDay(Guid.NewGuid(), TestTenantId,
			calendar.Id, date, type, "Diwali vacation", appliesToAll: !standardId.HasValue, standardId: standardId), true);
	});
}
