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
	public Task Current_onboarding_requires_a_confirmed_structure_without_a_payment_completion_gate() => RunCertificationScenarioAsync(async () =>
	{
		var (fixture, item) = await PrepareCurrentCompletionCaseAsync("CurrentFees");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		(await service.PreviewActivationAsync(item.Id)).LastError.ShouldContain("fee structure");
		(await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(item.Id,
			new() { ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "current-fees-missing" })))
			.Code.ShouldBe(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
		item = await ConfirmFixtureFeeStructureAsync(fixture.Offering, await service.GetAsync(item.Id));
		item = await service.ActivateAsync(item.Id, new() { ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "current-fees-confirmed" });
		item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
		item.FeeStructureConfirmed.ShouldBeTrue();
		item.FirstPaymentConfirmed.ShouldBeNull();
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
