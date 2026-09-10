using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using Volo.Abp.Domain.Repositories;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    private static FeeStructurePatchRequest Patch(string stamp, object changes) => new()
    { RequestId = Guid.NewGuid(), ConcurrencyStamp = stamp, Changes = JsonSerializer.SerializeToElement(changes) };

    [Fact]
    public async Task Patch_Amount_Preserves_Pins_And_Retry_Does_Not_Overwrite_Later_Changes()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var request = Patch(draft.ConcurrencyStamp, new { feeHeads = new { update = new[] { new { lineId = draft.FeeHeads.Single().Id, amount = 1250.25m } } } });
        var saved = await _feeStructures.PatchAsync(draft.Id, request);
        saved.FeeHeads.Single().Amount.ShouldBe(1250.25m);
        saved.CollectionPolicyVersionId.ShouldBe(draft.CollectionPolicyVersionId);
        saved.FeeHeads.Single().PaymentTermVersionId.ShouldBe(draft.FeeHeads.Single().PaymentTermVersionId);
        var renamed = await _feeStructures.PatchAsync(draft.Id, Patch(saved.ConcurrencyStamp, new { name = "Later edit" }));
        var retry = await _feeStructures.PatchAsync(draft.Id, request);
        retry.Name.ShouldBe("Later edit");
        retry.ConcurrencyStamp.ShouldBe(renamed.ConcurrencyStamp);
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.PatchAsync(draft.Id, Patch(draft.ConcurrencyStamp, new { name = "Stale edit" })));
        request.Changes = JsonSerializer.SerializeToElement(new { name = "Reused key" });
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.PatchAsync(draft.Id, request));
    }

    [Fact]
    public async Task Patch_Clear_And_NoOp_Have_Explicit_Semantics()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var noOp = await _feeStructures.PatchAsync(draft.Id, Patch(draft.ConcurrencyStamp, new { name = draft.Name }));
        noOp.ConcurrencyStamp.ShouldBe(draft.ConcurrencyStamp);
        var cleared = await _feeStructures.PatchAsync(draft.Id, Patch(draft.ConcurrencyStamp, new { policies = new { concession = (object?)null } }));
        cleared.ConcessionPolicyId.ShouldBeNull();
        cleared.ConcessionPolicyVersionId.ShouldBeNull();
        cleared.ConcessionApplicableFeeHeadIds.ShouldBeEmpty();
        cleared.CollectionPolicyVersionId.ShouldBe(draft.CollectionPolicyVersionId);
    }

    [Theory]
    [InlineData("{\"receiptBookId\":null}")]
    [InlineData("{\"feeHeads\":{\"update\":[{\"lineId\":\"00000000-0000-0000-0000-000000000001\",\"amount\":1}]}}")]
    [InlineData("{\"policies\":{\"collection\":{\"policyId\":null}}}")]
    [InlineData("{\"feeHeads\":null}")]
    public async Task Malformed_Patch_Is_Rejected_Without_Mutation(string json)
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var patch = Patch(draft.ConcurrencyStamp, new { }); patch.Changes = JsonDocument.Parse(json).RootElement.Clone();
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.PatchAsync(draft.Id, patch));
        var saved = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Single();
        saved.ConcurrencyStamp.ShouldBe(draft.ConcurrencyStamp);
        saved.FeeHeads.Single().Amount.ShouldBe(draft.FeeHeads.Single().Amount);
    }
    [Fact]
    public async Task Current_Policy_Lookup_Uses_Master_Version_And_Context_Has_No_Catalogs()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var context = await _feeStructures.GetContextAsync(metadata, standard);
        var properties = JsonSerializer.SerializeToElement(context).EnumerateObject().Select(x => x.Name).ToList();
        properties.ShouldNotContain("ReceiptBooks"); properties.ShouldNotContain("Policies"); properties.ShouldNotContain("Terms");
        var policy = await GetRequiredService<ICollectionPolicyAppService>().GetAsync(candidate.CollectionPolicyId!.Value);
        policy.IsAttachable.ShouldBeTrue(); policy.CurrentVersionId.ShouldNotBeNull();
        var list = await GetRequiredService<ICollectionPolicyAppService>().GetListAsync(new() { AcademicYearId = context.AcademicYearId, Status = "Active" });
        list.Items.Single().CurrentVersionId.ShouldBe(policy.CurrentVersionId);
        list.Items.Single().CurrentVersionNumber.ShouldBe(policy.Version);
    }

    [Fact]
    public async Task Amount_Patch_Preserves_Custom_Override_Identity_And_Legacy_Receipt_Storage()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var source = (await _feeStructures.GetSettingsAsync(metadata, standard)).Terms.Single().Installments.First();
        candidate.FeeHeads[0].ScheduleMode = ScheduleMode.Customized;
        candidate.FeeHeads[0].InstallmentOverrides = [new ScheduleOverrideDto { SourcePaymentTermInstallmentId = source.SourcePaymentTermInstallmentId, Sequence = source.Sequence, Name = source.Name, DueDate = new(2025, 4, 15) }];
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var repo = GetRequiredService<IRepository<FeeStructure, Guid>>();
        var entity = await repo.GetAsync(draft.Id);
        var overrideId = entity.FeeStructureDetails.Single().ScheduleOverrides.Single().Id;
        var legacyBook = (await GetRequiredService<IRepository<ReceiptBook, Guid>>().GetListAsync()).Single().Id;
        entity.SetReceiptBook(legacyBook); await repo.UpdateAsync(entity, autoSave: true);
        draft = (ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos.FeeStructureCreateResponse)await _feeStructures.PatchAsync(draft.Id, Patch(entity.ConcurrencyStamp, new { feeHeads = new { update = new[] { new { lineId = draft.FeeHeads.Single().Id, amount = 2500m } } } }));
        var saved = await repo.GetAsync(draft.Id);
        saved.FeeStructureDetails.Single().ScheduleOverrides.Single().Id.ShouldBe(overrideId);
        saved.ReceiptBookId.ShouldBe(legacyBook);
    }

    [Fact]
    public async Task Stale_New_Policy_Selection_Is_Rejected_While_Existing_Pin_Is_Retained()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var policy = await _collectionPolicies.GetAsync(candidate.CollectionPolicyId!.Value);
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        await _collectionPolicies.UpdateAsync(policy.Id, new CreateUpdateCollectionPolicyDto {
            AcademicYearId = policy.AcademicYearId, PolicyName = policy.PolicyName, PolicyCode = policy.PolicyCode,
            Status = "Active", ChangeReason = "Update test policy", AllowOnlineCollection = true
        });
        var current = await _collectionPolicies.GetAsync(policy.Id);
        current.CurrentVersionId.ShouldNotBe(policy.CurrentVersionId);
        var updated = await _feeStructures.PatchAsync(draft.Id, Patch(draft.ConcurrencyStamp, new { name = "Retained pin" }));
        updated.CollectionPolicyVersionId.ShouldBe(policy.CurrentVersionId);
        candidate.Name = "New stale selection"; candidate.CollectionPolicyVersionId = policy.CurrentVersionId;
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate }));
    }

}
