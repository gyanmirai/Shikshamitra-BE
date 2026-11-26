using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    [Fact]
    public async Task Missing_Proration_Blocks_Readiness_And_Publication_But_Draft_Saves_And_Clears_Succeed()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var policyId = candidate.ProrationPolicyId;
        var versionId = candidate.ProrationPolicyVersionId;
        (await GetRequiredService<IProrationPolicyAppService>().GetAsync(policyId!.Value)).IsDefault.ShouldBeTrue();
        candidate.ProrationPolicyId = null; candidate.ProrationPolicyVersionId = null;
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        draft.ProrationPolicyId.ShouldBeNull();
        var edit = AsRequest(draft); edit.Name = "Incomplete proration draft";
        draft = await _feeStructures.UpdateAsync(draft.Id, edit);
        var review = await _feeStructures.PreviewAsync(new()
        {
            FeeStructureMetadataId = metadata, StandardId = standard, StructureId = draft.Id,
            StandardFeeStructures = AsRequest(draft)
        });
        review.Errors.ShouldBe(new[] { "Select an active proration policy." });
        review.IsReady.ShouldBeFalse();
        var error = await Should.ThrowAsync<BusinessException>(() => _feeStructures.PublishAsync(draft.Id,
            new() { ConcurrencyStamp = draft.ConcurrencyStamp }));
        error.Code.ShouldBe(FeeStructureErrorCodes.InvalidConfiguration);
        ((System.Collections.Generic.List<string>)error.Data["Errors"]!).ShouldContain("Select an active proration policy.");
        (await _feeStructures.GetDetailAsync(draft.Id)).Status.ShouldBe(FeeStructureStatus.Unlocked);
        edit = AsRequest(draft); edit.SaveAsDraft = false;
        (await Should.ThrowAsync<BusinessException>(() => _feeStructures.UpdateAsync(draft.Id, edit)))
            .Code.ShouldBe(FeeStructureErrorCodes.InvalidConfiguration);

        edit = AsRequest(draft); edit.ProrationPolicyId = policyId; edit.ProrationPolicyVersionId = versionId;
        draft = await _feeStructures.UpdateAsync(draft.Id, edit);
        draft = await _feeStructures.PatchAsync(draft.Id, new()
        {
            RequestId = Guid.NewGuid(), ConcurrencyStamp = draft.ConcurrencyStamp,
            Changes = JsonSerializer.SerializeToElement(new { policies = new { proration = (object?)null } })
        });
        draft.ProrationPolicyId.ShouldBeNull(); draft.ProrationPolicyVersionId.ShouldBeNull();
    }

    [Fact]
    public async Task Explicit_Proration_Pin_Passes_Readiness_And_Is_Frozen_On_Publication()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        candidate.SaveAsDraft = false;
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var policies = GetRequiredService<IProrationPolicyAppService>();
        var policy = await policies.GetAsync(candidate.ProrationPolicyId!.Value);
        var newer = await policies.UpdateAsync(policy.Id, new()
        {
            AcademicYearId = policy.AcademicYearId, PolicyName = "Updated withdrawal proration",
            PolicyCode = policy.PolicyCode, Status = policy.Status, IsDefault = policy.IsDefault,
            FeeHeadRules = policy.FeeHeadRules, ConcurrencyStamp = policy.ConcurrencyStamp,
            ChangeReason = "Revise policy without changing the structure pin"
        });
        newer.CurrentVersionId.ShouldNotBe(candidate.ProrationPolicyVersionId);
        var review = await _feeStructures.PreviewAsync(new()
        {
            FeeStructureMetadataId = metadata, StandardId = standard, StructureId = draft.Id,
            StandardFeeStructures = AsRequest(draft)
        });
        review.Errors.ShouldBeEmpty(); review.IsReady.ShouldBeTrue();
        var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        published.ProrationPolicyId.ShouldBe(candidate.ProrationPolicyId);
        published.ProrationPolicyVersionId.ShouldBe(candidate.ProrationPolicyVersionId);
        published.PublicationReview!.Candidate.ProrationPolicyVersionId.ShouldBe(candidate.ProrationPolicyVersionId);
        published.PublicationReview.PolicyLabels.ShouldContainKey("proration");
        var read = await _feeStructures.GetDetailAsync(published.Id);
        read.ProrationPolicyVersionId.ShouldBe(candidate.ProrationPolicyVersionId);
        var retry = await _feeStructures.PublishAsync(published.Id, new() { ConcurrencyStamp = published.ConcurrencyStamp });
        retry.ConcurrencyStamp.ShouldBe(published.ConcurrencyStamp);
        retry.ProrationPolicyVersionId.ShouldBe(candidate.ProrationPolicyVersionId);
    }

    [Fact]
    public async Task Foreign_Proration_Version_Is_Rejected_Without_Substituting_The_Valid_Pin()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        candidate.ProrationPolicyVersionId = Guid.NewGuid();
        var review = await _feeStructures.PreviewAsync(new()
        {
            FeeStructureMetadataId = metadata, StandardId = standard, StandardFeeStructures = candidate
        });
        review.Errors.ShouldContain(message => message.Contains("Proration Policy") && message.Contains("unavailable"));
        review.IsReady.ShouldBeFalse();
    }
}
