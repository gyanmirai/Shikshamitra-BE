using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Document_finalization_scope_allows_assigned_standard_and_denies_another_under_the_same_board() =>
		RunCertificationScenarioAsync(async () =>
		{
			var allowed = await CreateCertificationFixtureAsync("ScopedFinalization", acceptOffer: false);
			var otherOffering = await WithUnitOfWorkAsync(async () =>
			{
				var otherStandard = await _standardManager.CreateAsync(allowed.Offering.StageId,
					$"Other grade {Guid.NewGuid():N}"[..28], 11, null);
				await _standardRepository.InsertAsync(otherStandard, autoSave: true);
				var offering = new AdmissionOffering(Guid.NewGuid(), TestTenantId, allowed.Offering.AdmissionCycleId,
					null, allowed.Offering.BoardId, allowed.Offering.MediumId, allowed.Offering.StageId,
					otherStandard.Id, "OTHER_STANDARD", true, null, null);
				offering.ConfigureReferences(allowed.Offering.WorkflowDefinitionId,
					allowed.Offering.InquiryFormDefinitionId, allowed.Offering.ApplicationFormDefinitionId);
				return await _offeringRepository.InsertAsync(offering, autoSave: true);
			});
			var applicant = await CreateCertificationApplicantAsync("OutsideScope", true, true, null);
			var denied = await _applicationAppService.StartAsync(applicant.Id, otherOffering.Id);
			var year = (await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(allowed.Offering.AdmissionCycleId))).AcademicYearId;
			var staffId = Guid.NewGuid();
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: staffId,
				academicYearId: year, educationBoardId: allowed.Offering.BoardId, educationMediumId: allowed.Offering.MediumId,
				educationMediumStageId: allowed.Offering.StageId, standardId: allowed.Offering.StandardId,
				description: "Access only the assigned admission standard"), autoSave: true);
			using var staff = _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(
			[
				new Claim(AbpClaimTypes.UserId, staffId.ToString()),
				new Claim(AbpClaimTypes.UserName, "scoped-admission-staff")
			], "Test")));
			(await _applicationAppService.FindAsync(denied.Id)).ShouldBeNull();
			(await _applicationAppService.FindAsync(Guid.NewGuid())).ShouldBeNull();
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.GetAsync(denied.Id));
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.GetWorkflowAsync(denied.Id));
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.GetFinalizationPreviewAsync(denied.Id));
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.MarkInReviewAsync(denied.Id,
				Decision(denied, "Unauthorized decision")));
			await Should.ThrowAsync<EntityNotFoundException>(() => FinalizeAsync(denied, true, "denied-standard-finalization"));
			(await _applicationAppService.FindAsync(allowed.Application.Id)).ShouldNotBeNull();
			var accepted = await _applicationAppService.AcceptOfferAsync(allowed.Application.Id,
				Decision(allowed.Application, "Authorized guardian acceptance"));
			(await _applicationAppService.GetFinalizationPreviewAsync(accepted.Id)).IsReady.ShouldBeTrue();
			var finalized = await FinalizeAsync(accepted, true, "allowed-standard-finalization");
			finalized.Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
		});
}
