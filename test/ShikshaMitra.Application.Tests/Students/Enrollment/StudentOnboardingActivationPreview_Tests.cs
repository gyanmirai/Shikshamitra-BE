using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Activation_preview_uses_current_fee_confirmation_without_rewriting_saved_evidence() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateAutomaticRegressionFixtureAsync("PreviewFeeSync");
		var item = await PrepareAutomaticRegressionCaseAsync(fixture, true, "preview-fee-sync");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var step = await _onboardingStepRepository.GetAsync(item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices).Id);
			step.Block("Select and confirm a published fee structure in the Fees tab.");
			await _onboardingStepRepository.UpdateAsync(step, true);
		});
		var before = await ReadAutomaticRegressionStepsAsync(item.Id);
		var preview = await service.PreviewActivationAsync(item.Id);
		preview.Status.ShouldBe(StudentOnboardingStatus.ReadyForActivation);
		preview.LastError.ShouldBeNullOrEmpty();
		preview.Steps.Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices).Status.ShouldBe(StudentOnboardingStepStatus.Completed);
		preview.ConcurrencyStamp.ShouldBe(item.ConcurrencyStamp);
		(await ReadAutomaticRegressionStepsAsync(item.Id)).ShouldBe(before);
		var command = new ActivateStudentOnboardingDto { ConcurrencyStamp = preview.ConcurrencyStamp, IdempotencyKey = "preview-fee-sync-activate" };
		var activated = await service.ActivateAsync(item.Id, command);
		activated.StudentEnrollmentId.ShouldNotBeNull();
		activated.Status.ShouldBe(StudentOnboardingStatus.Completed, activated.LastError);
		(await ReadAutomaticRegressionStepsAsync(item.Id)).Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices).Status.ShouldBe(StudentOnboardingStepStatus.Completed);
		(await service.ActivateAsync(item.Id, command)).StudentEnrollmentId.ShouldBe(activated.StudentEnrollmentId);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.OnboardingCaseId == item.Id))).ShouldBe(1);
	});

	[Fact]
	public Task Activation_preview_recognizes_settled_admission_fees_despite_a_stale_blocked_step() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCollectibleAdmissionAsync("PaidPreviewSync");
		await SaveAutomaticAccessAsync(fixture.YearId, student: false, guardian: false);
		var fees = GetRequiredService<IAdmissionFeeCollectionAppService>();
		var prepared = await fees.PrepareAsync(fixture.Application.Id, new());
		var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
		var payment = await fees.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 1000m, "paid-preview-sync"));
		await CompleteCollectibleDecisionAsync(fixture.Application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "paid-preview-finalize");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var division = await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
			await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "PaidPreviewA", 40), true));
		var item = await service.GetAsync(finalized.OnboardingCaseId);
		item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await service.AssignRollNumberAsync(item.Id, new() { RollNumber = 1, RollAssignmentMode = "automatic", ConcurrencyStamp = item.ConcurrencyStamp });
		var series = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single();
		item = await service.AssignGrNumberAsync(item.Id, new() { NumberSeriesId = series.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		await WithUnitOfWorkAsync(async () =>
		{
			var step = await _onboardingStepRepository.GetAsync(item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices).Id);
			step.Block("Select and confirm a published fee structure in the Fees tab.");
			await _onboardingStepRepository.UpdateAsync(step, true);
		});
		var before = await ReadAutomaticRegressionStepsAsync(item.Id);
		var preview = await service.PreviewActivationAsync(item.Id);
		preview.Status.ShouldBe(StudentOnboardingStatus.ReadyForActivation);
		preview.LastError.ShouldBeNullOrEmpty();
		(await ReadAutomaticRegressionStepsAsync(item.Id)).ShouldBe(before);
		var activated = await service.ActivateAsync(item.Id, new() { ConcurrencyStamp = preview.ConcurrencyStamp, IdempotencyKey = "paid-preview-activate" });
		activated.StudentEnrollmentId.ShouldNotBeNull();
		activated.Status.ShouldBe(StudentOnboardingStatus.Completed, activated.LastError);
		var summary = await fees.GetSummaryAsync(fixture.Application.Id);
		summary.IsSettled.ShouldBeTrue();
		summary.ConfirmedPaidAmount.ShouldBe(1000m);
		summary.OutstandingAmount.ShouldBe(0m);
		summary.Receipts.Single().Id.ShouldBe(payment.FeeReceiptId!.Value);
		(await AccountDuesAsync(prepared.AccountId.Value)).Single().Id.ShouldBe(due.Id);
		(await ReadAsync<AdmissionApplication>(fixture.Application.Id)).GetFeeSelection()!.OnboardingConfirmedAt.ShouldBeNull();
	});

	[Fact]
	public Task Activation_preview_rejects_unconfirmed_fees_even_when_saved_step_and_status_say_ready() => RunCertificationScenarioAsync(async () =>
	{
		var (_, item) = await PrepareCurrentCompletionCaseAsync("PreviewMissingFee");
		await WithUnitOfWorkAsync(async () =>
		{
			var step = await _onboardingStepRepository.GetAsync(item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices).Id);
			step.Complete(TestUserId, DateTime.UtcNow, "Stale fee confirmation.");
			await _onboardingStepRepository.UpdateAsync(step, true);
			var cases = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
			var stored = await cases.GetAsync(item.Id);
			stored.MarkReady(DateTime.UtcNow);
			await cases.UpdateAsync(stored, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var preview = await service.PreviewActivationAsync(item.Id);
		preview.Status.ShouldBe(StudentOnboardingStatus.InProgress);
		preview.LastError.ShouldContain("A confirmed fee structure is required.");
		preview.Steps.Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices).Status.ShouldBe(StudentOnboardingStepStatus.Blocked);
		(await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(item.Id,
			new() { ConcurrencyStamp = preview.ConcurrencyStamp, IdempotencyKey = "preview-missing-fee" }))).Code.ShouldBe(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.OnboardingCaseId == item.Id))).ShouldBe(0);
	});

	[Fact]
	public Task Activation_preview_preserves_the_live_seat_gate_and_returns_its_actual_reason() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateAutomaticRegressionFixtureAsync("PreviewExpiredSeat");
		var item = await PrepareAutomaticRegressionCaseAsync(fixture, true, "preview-expired-seat");
		await WithUnitOfWorkAsync(async () =>
		{
			var seats = GetRequiredService<IRepository<StudentSeatReservation, Guid>>();
			var seat = await seats.GetAsync(item.SeatReservationId!.Value);
			seat.Expire(seat.ExpiresAt);
			await seats.UpdateAsync(seat, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var preview = await service.PreviewActivationAsync(item.Id);
		preview.Status.ShouldNotBe(StudentOnboardingStatus.ReadyForActivation);
		preview.LastError.ShouldContain("The seat reservation has expired.");
		(await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(item.Id,
			new() { ConcurrencyStamp = preview.ConcurrencyStamp, IdempotencyKey = "preview-expired-seat-activate" }))).Code.ShouldBe(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.OnboardingCaseId == item.Id))).ShouldBe(0);
	});
}
