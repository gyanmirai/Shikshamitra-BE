using System;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Review_offering_validates_default_reviewer_exact_standard_on_create_update_and_activation() =>
		RunCertificationScenarioAsync(async () =>
		{
			var reviewer = await CreateReviewUserAsync("offering-reviewer");
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(reviewerRole: "offering-reviewer", assignedReviewerId: reviewer));
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			var original = await service.GetAsync(fixture.Offering.Id);
			var otherStandard = await WithUnitOfWorkAsync(async () =>
			{
				var standard = await _standardManager.CreateAsync(original.StageId, "Other review grade", 11, null);
				await _standardRepository.InsertAsync(standard, autoSave: true);
				return standard.Id;
			});
			await SetReviewOfferingScopeAsync(reviewer, original);
			var cycleId = await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId);
			var input = EligibilityOfferingInput<CreateAdmissionOfferingDto>(original);
			var created = await service.CreateAsync(cycleId, input);
			created.Configuration.IsValid.ShouldBeTrue();
			input.StandardId = otherStandard;
			(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(cycleId, input)))
				.Code.ShouldBe(AdmissionReviewErrorCodes.ReviewerNotAllowed);
			var update = EligibilityOfferingInput<UpdateAdmissionOfferingDto>(created);
			update.ConcurrencyStamp = created.ConcurrencyStamp; update.StandardId = otherStandard;
			(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(created.Id, update)))
				.Code.ShouldBe(AdmissionReviewErrorCodes.ReviewerNotAllowed);
			(await service.GetAsync(created.Id)).StandardId.ShouldBe(original.StandardId);
			created = await service.DeactivateAsync(created.Id, created.ConcurrencyStamp);
			await SetReviewOfferingScopeAsync(reviewer, null);
			(await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(created.Id, created.ConcurrencyStamp)))
				.Code.ShouldBe(AdmissionReviewErrorCodes.ReviewerNotAllowed);
			(await service.GetAsync(created.Id)).IsActive.ShouldBeFalse();
		});

	[Theory]
	[InlineData("inactive")]
	[InlineData("wrongRole")]
	public Task Review_offering_rechecks_current_default_reviewer_identity_after_workflow_publication(string change) =>
		RunCertificationScenarioAsync(async () =>
		{
			const string role = "review-offering-role";
			var reviewer = await CreateReviewUserAsync(role);
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(reviewerRole: role, assignedReviewerId: reviewer));
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			var original = await service.GetAsync(fixture.Offering.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
				var user = await users.GetAsync(reviewer, includeDetails: true);
				if (change == "inactive") { user.SetIsActive(false); await users.UpdateAsync(user, autoSave: true); }
				else (await GetRequiredService<IdentityUserManager>().RemoveFromRoleAsync(user, role)).Succeeded.ShouldBeTrue();
			});
			var input = EligibilityOfferingInput<CreateAdmissionOfferingDto>(original);
			var cycleId = await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId);
			(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(cycleId, input)))
				.Code.ShouldBe(AdmissionReviewErrorCodes.ReviewerNotAllowed);
			input.IsActive = false;
			(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(cycleId, input)))
				.Code.ShouldBe(AdmissionReviewErrorCodes.ReviewerNotAllowed);
		});

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public Task Review_offering_preserves_disabled_and_legacy_assignment_compatibility(bool disabled) =>
		RunCertificationScenarioAsync(async () =>
		{
			var inactive = await CreateReviewUserAsync(active: false, grantScope: false);
			var fixture = await CreateReviewFixtureAsync(disabled ? ReviewPolicyJson(enabled: false, assignedReviewerId: inactive) : "{}");
			var service = GetRequiredService<IAdmissionOfferingAppService>();
			var original = await service.GetAsync(fixture.Offering.Id);
			var created = await service.CreateAsync(await CreateEligibilityOfferingCycleAsync(original.AdmissionCycleId),
				EligibilityOfferingInput<CreateAdmissionOfferingDto>(original));
			created.Configuration.IsValid.ShouldBeTrue();
		});

	private async Task SetReviewOfferingScopeAsync(Guid reviewer, AdmissionOfferingDto? offering)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.DeleteAsync(item => item.UserId == reviewer, autoSave: true);
			if (offering == null) return;
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewer,
				academicYearId: cycle.AcademicYearId, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
				educationMediumStageId: offering.StageId, standardId: offering.StandardId), autoSave: true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(reviewer);
	}
}
