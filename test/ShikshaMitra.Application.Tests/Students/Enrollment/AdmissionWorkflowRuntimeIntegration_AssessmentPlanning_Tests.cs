using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Common;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Combined_workbench_includes_two_offerings_without_a_required_page_scope()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await CreateManagedAssessmentApplicationAsync(scheduling: true); var second = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var result = await service.GetApplicantsAsync(new() { MaxResultCount = 25 });
		result.Items.ShouldContain(item => item.ApplicationId == first.Application.Id && item.AdmissionOfferingId == first.Offering.Id);
		result.Items.ShouldContain(item => item.ApplicationId == second.Application.Id && item.AdmissionOfferingId == second.Offering.Id);
		result.Counts.ToBook.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task Shared_session_confirmation_is_atomic_idempotent_and_cancellation_releases_room()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await CreateManagedAssessmentApplicationAsync(scheduling: true); var second = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var scopes = await service.GetScopesAsync(); var room = await CreateAssessmentPlanningRoomAsync();
		var input = new AdmissionAssessmentSessionInput { Name = "Shared entrance exam", RoomId = room.Id, Capacity = 2,
			StartsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(3), EndsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(4),
			Scopes = [new() { AdmissionOfferingId = first.Offering.Id, WorkflowStageDefinitionId = scopes.First(item => item.AdmissionOfferingId == first.Offering.Id).WorkflowStageDefinitionId },
				new() { AdmissionOfferingId = second.Offering.Id, WorkflowStageDefinitionId = scopes.First(item => item.AdmissionOfferingId == second.Offering.Id).WorkflowStageDefinitionId }] };
		var preview = await service.PreviewSessionAsync(new() { Session = input, MaxResultCount = 1 });
		preview.EligibleCount.ShouldBe(2); preview.ProposedBookings.Count.ShouldBe(2); preview.Items.Count.ShouldBe(1);
		var secondPage = await service.PreviewSessionAsync(new() { Session = input, SkipCount = 1, MaxResultCount = 1 });
		secondPage.PreviewFingerprint.ShouldBe(preview.PreviewFingerprint);
		var confirmation = new AdmissionAssessmentSessionConfirmInput { Session = input, IdempotencyKey = Guid.NewGuid(), PreviewFingerprint = preview.PreviewFingerprint,
			Bookings = preview.ProposedBookings.Select(item => new AdmissionAssessmentSelectedBookingInput { ApplicationId = item.ApplicationId, StageInstanceId = item.StageInstanceId, ConcurrencyStamp = item.ConcurrencyStamp }).ToList() };
		var saved = await service.ConfirmSessionAsync(confirmation); saved.BookedCount.ShouldBe(2); saved.Session.ScopeDetails.Count.ShouldBe(2); saved.Session.RoomReservationId.ShouldNotBeNull();
		var retry = await service.ConfirmSessionAsync(confirmation); retry.Session.Id.ShouldBe(saved.Session.Id); retry.BookedCount.ShouldBe(2);
		var conflict = await Should.ThrowAsync<BusinessException>(() => service.PreviewSessionAsync(new() { Session = input }));
		conflict.Code.ShouldBe(AdmissionAssessmentErrorCodes.InvalidState);
		conflict.Data["Reason"].ShouldBeOfType<string>().ShouldContain("Room is reserved");
		var firstState = await service.GetAsync(first.Application.Id, first.StageId); firstState.Attempts.Count.ShouldBe(1);
		var cancelled = await service.CancelSessionAsync(saved.Session.Id, new() { ConcurrencyStamp = retry.Session.ConcurrencyStamp, CancelPendingBookings = true, Notes = "School changed the exam date." });
		cancelled.IsCancelled.ShouldBeTrue(); cancelled.BookedCount.ShouldBe(0);
		var reservation = await GetRequiredService<IRepository<RoomReservation, Guid>>().GetAsync(saved.Session.RoomReservationId!.Value);
		reservation.Status.ShouldBe(RoomReservationStatus.Cancelled);
		(await service.GetAsync(first.Application.Id, first.StageId)).Attempts.Single().Status.ShouldBe("cancelled");
	}

	[Fact]
	public async Task Shared_booking_preserves_unselected_edits_and_metadata_only_room_approval()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await CreateManagedAssessmentApplicationAsync(scheduling: true); var second = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>(); var scopes = await service.GetScopesAsync(); var room = await CreateAssessmentPlanningRoomAsync();
		var input = new AdmissionAssessmentSessionInput { Name = "Shared selection", RoomId = room.Id, Capacity = 2,
			StartsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(3), EndsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(4),
			Scopes = [new() { AdmissionOfferingId = first.Offering.Id, WorkflowStageDefinitionId = scopes.First(item => item.AdmissionOfferingId == first.Offering.Id).WorkflowStageDefinitionId },
				new() { AdmissionOfferingId = second.Offering.Id, WorkflowStageDefinitionId = scopes.First(item => item.AdmissionOfferingId == second.Offering.Id).WorkflowStageDefinitionId }] };
		var preview = await service.PreviewSessionAsync(new() { Session = input });
		await WithUnitOfWorkAsync(async () =>
		{
			var unselected = await _applicationRepository.GetAsync(second.Application.Id);
			unselected.SaveDraft();
			await _applicationRepository.UpdateAsync(unselected, true);
		});
		(await _applicationAppService.GetAsync(second.Application.Id)).ConcurrencyStamp.ShouldNotBe(second.Application.ConcurrencyStamp);
		(await service.PreviewSessionAsync(new() { Session = input })).PreviewFingerprint.ShouldBe(preview.PreviewFingerprint);
		var selected = preview.ProposedBookings.Single(item => item.ApplicationId == first.Application.Id);
		var saved = await service.ConfirmSessionAsync(new() { Session = input, IdempotencyKey = Guid.NewGuid(), PreviewFingerprint = preview.PreviewFingerprint,
			Bookings = [new() { ApplicationId = selected.ApplicationId, StageInstanceId = selected.StageInstanceId, ConcurrencyStamp = selected.ConcurrencyStamp }] });
		var reservationId = saved.Session.RoomReservationId!.Value;
		var reservation = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<RoomReservation, Guid>>().GetAsync(reservationId));
		var approval = reservation.Status;
		input.Name = "Renamed entrance exam"; input.ConcurrencyStamp = saved.Session.ConcurrencyStamp;
		var renamed = await service.UpdateSessionAsync(saved.Session.Id, input);
		renamed.RoomReservationId.ShouldBe(reservationId); renamed.BookedCount.ShouldBe(1);
		reservation = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<RoomReservation, Guid>>().GetAsync(reservationId));
		reservation.Status.ShouldBe(approval);
		(await service.GetAsync(second.Application.Id, second.StageId)).Attempts.ShouldBeEmpty();
	}

	[Fact]
	public async Task Legacy_session_adopts_an_institute_room_without_replacing_booked_evidence()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var application = await CreateManagedAssessmentApplicationAsync(scheduling: true); var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var scope = (await service.GetScopesAsync()).Single(item => item.AdmissionOfferingId == application.Offering.Id);
		var input = new AdmissionAssessmentSessionInput { Name = "Legacy entrance exam", Location = "Original location", Capacity = 2,
			AdmissionOfferingId = application.Offering.Id, WorkflowStageDefinitionId = scope.WorkflowStageDefinitionId,
			StartsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(3), EndsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(4) };
		var legacy = await service.CreateSessionAsync(input);
		var state = await service.GetAsync(application.Application.Id, application.StageId);
		var booked = await service.BookAsync(application.Application.Id, application.StageId, new() { SessionId = legacy.Id, ConcurrencyStamp = state.ConcurrencyStamp });
		var current = await service.GetSessionAsync(legacy.Id); var room = await CreateAssessmentPlanningRoomAsync();
		input.RoomId = room.Id; input.ConcurrencyStamp = current.ConcurrencyStamp;
		input.Scopes = [new() { AdmissionOfferingId = input.AdmissionOfferingId, WorkflowStageDefinitionId = input.WorkflowStageDefinitionId }];
		var adopted = await service.UpdateSessionAsync(legacy.Id, input);
		adopted.RoomId.ShouldBe(room.Id); adopted.RoomReservationId.ShouldNotBeNull(); adopted.BookedCount.ShouldBe(1);
		adopted.ScopeDetails.Single().AdmissionOfferingId.ShouldBe(application.Offering.Id);
		(await service.GetAsync(application.Application.Id, application.StageId)).Attempts.Single().Id.ShouldBe(booked.Attempts.Single().Id);
	}

	[Fact]
	public async Task Individual_interviews_use_sequential_capacity_and_reserve_explicit_slots_before_automatic_choices()
	{
		using var tenant = _currentTenant.Change(TestTenantId); using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var nextApplicant = await CreateApplicantAsync("Second interview candidate");
		var second = await StartFixtureApplicationAsync(nextApplicant.Id, first.Offering.Id);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var scope = (await service.GetScopesAsync()).Single(item => item.AdmissionOfferingId == first.Offering.Id);
		var room = await CreateAssessmentPlanningRoomAsync(2); var interviewer = await CreateAssessmentInterviewerAsync();
		var input = new AdmissionAssessmentSessionInput { Name = "Individual entrance interviews", SessionType = "individualInterview", RoomId = room.Id,
			InterviewerId = interviewer.Id, Capacity = 8, SlotDurationMinutes = 10, BreakMinutes = 5,
			StartsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(3), EndsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(5),
			Scopes = [new() { AdmissionOfferingId = first.Offering.Id, WorkflowStageDefinitionId = scope.WorkflowStageDefinitionId }] };
		var preview = await service.PreviewSessionAsync(new() { Session = input });
		preview.AvailableCount.ShouldBe(8); preview.Slots.Count.ShouldBe(8); preview.ProposedBookings.Count.ShouldBe(2);
		input.Capacity = 9;
		await Should.ThrowAsync<BusinessException>(() => service.PreviewSessionAsync(new() { Session = input }));
		input.Capacity = 8;
		var automatic = preview.ProposedBookings.Single(item => item.ApplicationId == first.Application.Id);
		var explicitCandidate = preview.ProposedBookings.Single(item => item.ApplicationId == second.Id);
		await service.ConfirmSessionAsync(new() { Session = input, IdempotencyKey = Guid.NewGuid(), PreviewFingerprint = preview.PreviewFingerprint,
			Bookings = [new() { ApplicationId = automatic.ApplicationId, StageInstanceId = automatic.StageInstanceId, ConcurrencyStamp = automatic.ConcurrencyStamp },
				new() { ApplicationId = explicitCandidate.ApplicationId, StageInstanceId = explicitCandidate.StageInstanceId, ConcurrencyStamp = explicitCandidate.ConcurrencyStamp, SlotStartsAt = preview.Slots[0].StartsAt }] });
		(await service.GetAsync(first.Application.Id, first.StageId)).Attempts.Single().SlotStartsAt.ShouldBe(preview.Slots[1].StartsAt);
		(await service.GetAsync(second.Id, explicitCandidate.StageInstanceId)).Attempts.Single().SlotStartsAt.ShouldBe(preview.Slots[0].StartsAt);
	}

	[Fact]
	public void Reservation_window_uses_school_time_and_rejects_cross_school_day()
	{
		var window = AdmissionAssessmentSchedulingService.SchoolWindow(new(2035, 1, 2, 3, 30, 0, DateTimeKind.Utc), new(2035, 1, 2, 4, 30, 0, DateTimeKind.Utc));
		window.Start.ShouldBe(TimeSpan.FromHours(9)); window.End.ShouldBe(TimeSpan.FromHours(10)); window.Date.Day.ShouldBe(2);
		Should.Throw<BusinessException>(() => AdmissionAssessmentSchedulingService.SchoolWindow(new(2035, 1, 2, 18, 0, 0, DateTimeKind.Utc), new(2035, 1, 2, 19, 0, 0, DateTimeKind.Utc)));
	}

	private async Task<Employee> CreateAssessmentInterviewerAsync()
	{
		await EnsureTestIdentityUserAsync(TestUserId, "assessment-interviewer");
		var role = new IdentityRole(Guid.NewGuid(), $"Assessment teacher {Guid.NewGuid():N}", TestTenantId);
		await GetRequiredService<IRepository<IdentityRole, Guid>>().InsertAsync(role, true);
		var employee = new Employee(Guid.NewGuid(), TestTenantId, TestUserId, $"ASM{Guid.NewGuid():N}"[..12],
			new EmployeeBasicInfo(ProfilePictureBlobName: null, LastName: "Teacher", FirstName: "Interview", MiddleName: null, ShortName: "INT",
				EmployeeStatusType: EmployeeStatusType.Active, DateOfBirth: null, FamilyCast: null, CastType: null, GenderType: null, Religion: null,
				MaritalStatus: null, BloodGroup: null, MobileNo: null, SecondaryMobileNo: null, Email: null, PanNo: "ABCDE1234F", AdharNo: "123456789012",
				Address1: null, Address2: null, Address3: null, Department: "Academics", Designation: "Teacher", DateOfJoining: new DateOnly(2025, 1, 1),
				EmployeeGroup: null, Qualification: null, RoleId: role.Id, IdentificationMark1: null, City: null, State: null, Country: null, PinCode: null, ShiftId: null),
			new BankDetails(null, null, null, null, null), null);
		return await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(employee, true);
	}

	private async Task<Room> CreateAssessmentPlanningRoomAsync(int capacity = 30)
	{
		var building = new Building { TenantId = TestTenantId, BuildingName = "Assessment building", TotalFloors = 1 };
		var floor = new Floor { FloorName = "Ground", FloorNo = 0, TotalRoom = 1 };
		var type = new RoomType { TenantId = TestTenantId, RoomTypeName = "Assessment classroom", ShortName = "ASM" };
		typeof(Building).GetProperty("Id")!.SetValue(building, Guid.NewGuid()); typeof(Floor).GetProperty("Id")!.SetValue(floor, Guid.NewGuid()); typeof(RoomType).GetProperty("Id")!.SetValue(type, Guid.NewGuid());
		floor.BuildingId = building.Id; await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(building, true);
		await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(floor, true); await GetRequiredService<IRepository<RoomType, Guid>>().InsertAsync(type, true);
		var room = new Room { TenantId = TestTenantId, RoomName = "Entrance room", BuildingId = building.Id, FloorId = floor.Id, RoomTypeId = type.Id, Capacity = capacity };
		typeof(Room).GetProperty("Id")!.SetValue(room, Guid.NewGuid()); return await GetRequiredService<IRepository<Room, Guid>>().InsertAsync(room, true);
	}
}
