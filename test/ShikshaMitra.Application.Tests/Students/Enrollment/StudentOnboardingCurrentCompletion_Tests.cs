using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
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
	public Task Current_onboarding_activates_without_fee_assignment_or_payment_and_keeps_finance_pending() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, item) = await PrepareCurrentCompletionCaseAsync("CurrentFees");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var preview = await service.PreviewActivationAsync(item.Id);
		preview.Status.ShouldBe(StudentOnboardingStatus.ReadyForActivation);
		preview.LastError.ShouldBeNullOrEmpty();
		(await service.GetFeeStructureAsync(item.Id)).OnboardingBlocker.ShouldBeNull();
		var command = new ActivateStudentOnboardingDto { ConcurrencyStamp = preview.ConcurrencyStamp, IdempotencyKey = "current-fees-optional" };
		item = await service.ActivateAsync(item.Id, command);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
		item.FeeStructureConfirmed.ShouldBeFalse();
		item.FeeAssignmentConfirmed.ShouldBe(false);
		item.SelectedFeeStructureId.ShouldBeNull();
		item.FirstPaymentConfirmed.ShouldBeNull();
		item.PrimaryBlocker.ShouldBeNull();
		item.ProgressPercent.ShouldBe(100);
		var feeStep = item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices);
		feeStep.Required.ShouldBeFalse();
		feeStep.Status.ShouldBe(StudentOnboardingStepStatus.NotRequired);
		(await service.ActivateAsync(item.Id, command)).StudentEnrollmentId.ShouldBe(item.StudentEnrollmentId);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.OnboardingCaseId == item.Id))).ShouldBe(1);
		(await ReadAsync<AdmissionApplication>(fixture.Application.Id)).GetFeeSelection().ShouldBeNull();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().CountAsync(value => value.StudentId == item.StudentId))).ShouldBe(0);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync(value => value.StudentId == item.StudentId))).ShouldBe(0);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync(value => value.StudentId == item.StudentId))).ShouldBe(0);
	});

	[Fact]
	public Task Current_offering_does_not_fabricate_a_legacy_blanket_service_consent_gate() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, item) = await PrepareCurrentCompletionCaseAsync("CurrentConsent", legacyConsent: true);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		item = await ConfirmFixtureFeeStructureAsync(fixture.Offering, item);
		item = await service.ActivateAsync(item.Id, new() { ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "current-no-blanket-consent" });
		item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
		(await service.GetServiceHandoffsAsync(item.Id)).ShouldNotContain(value => value.ServiceKey == "service-consent");
		(await Should.ThrowAsync<BusinessException>(() => service.ConfirmServiceConsentAsync(item.Id,
			new() { ConcurrencyStamp = item.ConcurrencyStamp, EvidenceReference = "No service was selected" })))
			.Code.ShouldBe(StudentOnboardingErrorCodes.ServiceConsentInvalid);
		(await service.GetAsync(item.Id)).Events.ShouldNotContain(value => value.EventType == "ServiceConsentConfirmed");
	});

	private async Task<(AdmissionCertificationFixture Fixture, StudentOnboardingCaseDto Item)> PrepareCurrentCompletionCaseAsync(string name, bool legacyConsent = false)
	{
		var fixture = await CreateOnboardingFlowFixtureAsync(name, null);
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
		var service = GetRequiredService<IStudentOnboardingAppService>();
		if (legacyConsent) await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Historical consent policy", RequireServiceConsent = true });
		var finalized = await FinalizeAsync(fixture.Application, true, name + "-finalized");
		var division = await WithUnitOfWorkAsync(async () => await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
			await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, name, 40), true));
		var item = await service.GetAsync(finalized.OnboardingCaseId);
		item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		item = await service.AssignRollNumberAsync(item.Id, new() { RollNumber = 1, RollAssignmentMode = "automatic", ConcurrencyStamp = item.ConcurrencyStamp });
		var series = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single();
		item = await service.AssignGrNumberAsync(item.Id, new() { NumberSeriesId = series.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		return (fixture, item);
	}

	private async Task<StudentOnboardingCaseDto> ConfirmFixtureFeeStructureAsync(AdmissionOffering offering, StudentOnboardingCaseDto item)
	{
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var structure = await CreateFeeSelectionReferenceAsync(offering, item.AcademicYearId);
		await service.SaveFeeStructureAsync(item.Id, new() { StructureId = structure.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		return await service.GetAsync(item.Id);
	}
}
