using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Managed_Seat_Should_protect_capacity_and_support_waitlist_promotion()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Seat runtime integration test"), true);
			var basic = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(basic.Id);
			var managed = await CreateManagedSeatWorkflowAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureReferences(managed.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(stored, true);
			});
			var service = GetRequiredService<IAdmissionSeatAppService>();
			var first = await EnterSeatAsync(offering.Id, "Reserved Applicant", "enter-seat-first");
			first.Stage.SeatStageEnabled.ShouldBeTrue();
			var initial = await service.GetAsync(first.Application.Id, first.Stage.Id);
			initial.Capacity.ShouldBe(1);
			var reserveInput = new ReserveAdmissionSeatDto { JoinWaitlistWhenFull = true,
				IdempotencyKey = "seat-reserve-first", ConcurrencyStamp = initial.ConcurrencyStamp };
			var reserved = await service.ReserveAsync(first.Application.Id, first.Stage.Id, reserveInput);
			reserved.Status.ShouldBe("reserved");
			reserved.RemainingCapacity.ShouldBe(0);
			(await service.ReserveAsync(first.Application.Id, first.Stage.Id, reserveInput)).Events.Count.ShouldBe(1);
			var conflict = await Should.ThrowAsync<BusinessException>(() => service.ReserveAsync(first.Application.Id,
				first.Stage.Id, new ReserveAdmissionSeatDto { JoinWaitlistWhenFull = false,
					IdempotencyKey = reserveInput.IdempotencyKey, ConcurrencyStamp = reserved.ConcurrencyStamp }));
			conflict.Code.ShouldBe(AdmissionSeatStageErrorCodes.IdempotencyConflict);
			var blocked = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(
				first.Application.Id, new() { ExpectedCurrentStageInstanceId = first.Stage.Id,
					IdempotencyKey = "seat-blocked", ConcurrencyStamp = reserved.ConcurrencyStamp }));
			blocked.Code.ShouldBe(AdmissionSeatStageErrorCodes.CompletionRequired);
			var confirmed = await service.ConfirmAsync(first.Application.Id, first.Stage.Id, new()
			{
				Reason = "Family confirmed admission.", IdempotencyKey = "seat-confirm-first",
				ConcurrencyStamp = reserved.ConcurrencyStamp,
				ReservationConcurrencyStamp = reserved.ReservationConcurrencyStamp
			});
			confirmed.Status.ShouldBe("confirmed");
			confirmed.CanComplete.ShouldBeTrue();
			var completed = await _applicationAppService.AdvanceWorkflowAsync(first.Application.Id, new()
			{
				ExpectedCurrentStageInstanceId = first.Stage.Id, IdempotencyKey = "seat-complete-first",
				ConcurrencyStamp = confirmed.ConcurrencyStamp
			});
			completed.Stages.Single(item => item.Code == "DONE").Status.ShouldBe(AdmissionRuntimeStageStatus.Active);

			var second = await EnterSeatAsync(offering.Id, "Waitlisted Applicant", "enter-seat-second");
			var secondInitial = await service.GetAsync(second.Application.Id, second.Stage.Id);
			var waitlisted = await service.ReserveAsync(second.Application.Id, second.Stage.Id, new()
			{
				JoinWaitlistWhenFull = true, IdempotencyKey = "seat-waitlist-second",
				ConcurrencyStamp = secondInitial.ConcurrencyStamp
			});
			waitlisted.Status.ShouldBe("waitlisted");
			waitlisted.WaitlistPosition.ShouldBe(1);
			var expanded = await service.UpdateCapacityAsync(second.Application.Id, second.Stage.Id, new()
			{
				Capacity = 2, Reason = "Approved second intake seat.", IdempotencyKey = "seat-capacity-two",
				ConcurrencyStamp = waitlisted.ConcurrencyStamp, PoolConcurrencyStamp = waitlisted.PoolConcurrencyStamp
			});
			expanded.RemainingCapacity.ShouldBe(1);
			var promoted = await service.ReserveAsync(second.Application.Id, second.Stage.Id, new()
			{
				JoinWaitlistWhenFull = true, IdempotencyKey = "seat-promote-second",
				ConcurrencyStamp = expanded.ConcurrencyStamp,
				ReservationConcurrencyStamp = expanded.ReservationConcurrencyStamp
			});
			promoted.Status.ShouldBe("reserved");
			promoted.WaitlistPosition.ShouldBeNull();
		}
	}

	[Fact]
	public async Task Managed_Seat_Should_reserve_on_entry_and_allow_confirmed_release_after_advancement()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Automatic seat runtime integration test"), true);
			var basic = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(basic.Id);
			var managed = await CreateManagedSeatWorkflowAsync("onEntry");
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await _offeringRepository.GetAsync(offering.Id);
				stored.ConfigureReferences(managed.Id, stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
				await _offeringRepository.UpdateAsync(stored, true);
			});
			var service = GetRequiredService<IAdmissionSeatAppService>();
			var entered = await EnterSeatAsync(offering.Id, "Automatic Seat Applicant", "enter-auto-seat");
			var reserved = await service.GetAsync(entered.Application.Id, entered.Stage.Id);
			reserved.Status.ShouldBe("reserved");
			reserved.Events.Single().Action.ShouldBe("seat-reserve");
			var confirmed = await service.ConfirmAsync(entered.Application.Id, entered.Stage.Id, new()
			{
				IdempotencyKey = "confirm-auto-seat", ConcurrencyStamp = reserved.ConcurrencyStamp,
				ReservationConcurrencyStamp = reserved.ReservationConcurrencyStamp
			});
			await _applicationAppService.AdvanceWorkflowAsync(entered.Application.Id, new()
			{
				ExpectedCurrentStageInstanceId = entered.Stage.Id, IdempotencyKey = "complete-auto-seat",
				ConcurrencyStamp = confirmed.ConcurrencyStamp
			});
			var afterAdvance = await service.GetAsync(entered.Application.Id, entered.Stage.Id);
			afterAdvance.IsActive.ShouldBeFalse();
			afterAdvance.CanRelease.ShouldBeTrue();
			var released = await service.ReleaseAsync(entered.Application.Id, entered.Stage.Id, new()
			{
				Reason = "Applicant withdrew before finalization.", IdempotencyKey = "release-auto-seat",
				ConcurrencyStamp = afterAdvance.ConcurrencyStamp,
				ReservationConcurrencyStamp = afterAdvance.ReservationConcurrencyStamp
			});
			released.Status.ShouldBe("released");
			released.RemainingCapacity.ShouldBe(1);
			var preview = await _applicationAppService.GetFinalizationPreviewAsync(entered.Application.Id);
			preview.Items.Single(item => item.Key == "admission_seat").Passed.ShouldBeFalse();
		}
	}

	private async Task<(AdmissionApplicationDto Application, AdmissionApplicationStageDto Stage)> EnterSeatAsync(
		Guid offeringId, string name, string key)
	{
		var application = await StartFixtureApplicationAsync((await CreateApplicantAsync(name)).Id, offeringId);
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		runtime = await _applicationAppService.AdvanceWorkflowAsync(application.Id, new()
		{
			ExpectedCurrentStageInstanceId = runtime.CurrentStageInstanceId!.Value,
			IdempotencyKey = key, ConcurrencyStamp = application.ConcurrencyStamp
		});
		return (await _applicationAppService.GetAsync(application.Id),
			runtime.Stages.Single(item => item.StageType == AdmissionWorkflowStageType.Seat));
	}

	private async Task<AdmissionWorkflowDto> CreateManagedSeatWorkflowAsync(string reservationMode = "manual")
	{
		var startId = Guid.NewGuid(); var seatId = Guid.NewGuid(); var doneId = Guid.NewGuid();
		var policy = new AdmissionSeatStagePolicy { Version = 1, Enabled = true, DefaultCapacity = 1,
			ReservationMode = reservationMode, ReservationDurationHours = 72, AllowDurationOverride = false,
			MinimumDurationHours = 72, MaximumDurationHours = 72, AllowRenewal = true,
			MaximumRenewals = 1, AllowWaitlist = true, AllowCapacityOverride = true,
			AllowedCompletionOutcomes = ["confirmed", "waitlisted"] };
		var workflow = await SeedPublishedHistoricalWorkflowAsync(new()
		{
			Name = "Managed seat", Code = $"SEAT_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = startId, Code = "START", Name = "Ready for seat", StageType = AdmissionWorkflowStageType.Custom,
					DisplayOrder = 0, IsEntryStage = true, IsRequired = true },
				new() { Id = seatId, Code = "SEAT", Name = "Seat allocation", StageType = AdmissionWorkflowStageType.Seat,
					DisplayOrder = 1, IsRequired = true, ConfigurationJson = JsonSerializer.Serialize(new { seat = policy }) },
				new() { Id = doneId, Code = "DONE", Name = "Seat complete", StageType = AdmissionWorkflowStageType.Custom,
					DisplayOrder = 2, IsTerminalStage = true, IsRequired = true }
			],
			Transitions =
			[
				new() { FromStageId = startId, ToStageId = seatId, Priority = 1, IsDefault = true },
				new() { FromStageId = seatId, ToStageId = doneId, Priority = 1, IsDefault = true }
			]
		});
		return workflow;
	}
}
