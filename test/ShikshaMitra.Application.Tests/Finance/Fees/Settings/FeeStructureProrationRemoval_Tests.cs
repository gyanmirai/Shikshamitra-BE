using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Policies;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    private async Task<Guid> CreateRefundExpenseCategoryAsync(Guid year)
    {
        var category = new ShikshaMitra.Domain.Finance.Accounts.Settings.TransactionCategory(
            Guid.NewGuid(), TestTenantId, year, ShikshaMitra.Finance.Accounts.Transactions.TransactionType.Expense,
            "Refund expenses", null);
        await GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Accounts.Settings.TransactionCategory, Guid>>()
            .InsertAsync(category, autoSave: true);
        return category.Id;
    }

    [Fact]
    public async Task Structure_Override_Can_Be_Set_And_Cleared_And_Wins_Over_The_Year_Default()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var context = await _feeStructures.GetContextAsync(metadata, standard);
        var policies = GetRequiredService<IProrationPolicyAppService>();
        async Task<ProrationPolicyDto> CreatePolicy(string name, bool isDefault)
        {
            var created = await policies.CreateAsync(new()
            {
                AcademicYearId = context.AcademicYearId, PolicyName = name, Status = "Active", IsDefault = isDefault,
                FeeHeadRules = [new() { FeeHeadId = candidate.FeeHeads.Single().FeeHeadId, RuleType = "Daily", ApplyOnAdmission = true }]
            });
            return await policies.GetAsync(created.Id);
        }

        var yearDefault = await CreatePolicy("Academic-year daily rule", true);
        var selected = await CreatePolicy("Structure daily rule", false);
        candidate.ProrationPolicyId = null;
        candidate.ProrationPolicyVersionId = null;
        (await _feeStructures.GetCatalogueAsync(metadata, standard)).Policies
            .ShouldContain(x => x.Family == "proration" && x.VersionId == selected.CurrentVersionId);
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        async Task<FeeStructureCreateResponse> Patch(FeeStructureCreateResponse structure, object? selection) =>
            await _feeStructures.PatchAsync(structure.Id, new()
            {
                RequestId = Guid.NewGuid(), ConcurrencyStamp = structure.ConcurrencyStamp,
                Changes = JsonSerializer.SerializeToElement(new { policies = new { proration = selection } })
            });

        var attached = await Patch(draft, new { policyId = selected.Id, policyVersionId = selected.CurrentVersionId });
        attached.ProrationPolicyVersionId.ShouldBe(selected.CurrentVersionId);
        var resolver = GetRequiredService<AdmissionProrationPolicyResolver>();
        var resolved = await WithUnitOfWorkAsync(() => resolver.ResolveAsync(context.AcademicYearId, draft.Id));
        resolved!.Id.ShouldBe(selected.CurrentVersionId!.Value);

        var cleared = await Patch(attached, null);
        cleared.ProrationPolicyVersionId.ShouldBeNull();
        (await WithUnitOfWorkAsync(() => resolver.ResolveAsync(context.AcademicYearId, draft.Id)))!.Id
            .ShouldBe(yearDefault.CurrentVersionId!.Value);
    }

    [Fact]
    public async Task Legacy_Publication_Projection_Hides_Proration_Without_Rewriting_Evidence()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        candidate.FeeHeads.Single().Amount = 1000m;
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        var repository = GetRequiredService<IRepository<FeeStructure, Guid>>();
        string evidence;
        using (var hydration = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var stored = await repository.GetAsync(draft.Id);
            // Reconstruct a publication made before explicit proration pins were supported.
            stored.AttachProrationPolicy(null, null);
            var frozen = JsonNode.Parse(stored.GetProperty<string>("PublicationReview")!)!;
            frozen["PolicyLabels"]!["proration"] = "Historical policy · v1";
            frozen["Candidate"]!["ProrationPolicyId"] = Guid.NewGuid();
            frozen["Coverage"]![0]!["Proration"] = "Daily";
            frozen["Warnings"]!.AsArray().Add("Heads without a proration rule use full charge. Review this treatment before publishing.");
            evidence = frozen.ToJsonString();
            stored.SetProperty("PublicationReview", evidence);
            await repository.UpdateAsync(stored, autoSave: true);
            await hydration.CompleteAsync();
        }
        var read = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Single();
        read.ProrationPolicyVersionId.ShouldBeNull();
        read.PublicationReview!.PolicyLabels.ShouldNotContainKey("proration");
        read.PublicationReview.Candidate.ProrationPolicyId.ShouldBeNull();
        read.PublicationReview.Coverage.Single().Proration.ShouldBeNull();
        read.PublicationReview!.Warnings.ShouldNotContain(x => x.Contains("proration", StringComparison.OrdinalIgnoreCase));
        (await repository.GetAsync(draft.Id)).GetProperty<string>("PublicationReview").ShouldBe(evidence);
        read.PublicationReview.Schedules.Single().Installments.Sum(x => x.CalculatedAmount).ShouldBe(1000m);
        var retry = await _feeStructures.PublishAsync(read.Id, new() { ConcurrencyStamp = read.ConcurrencyStamp });
        retry.ConcurrencyStamp.ShouldBe(read.ConcurrencyStamp);
        retry.ProrationPolicyVersionId.ShouldBeNull();
        (await repository.GetAsync(draft.Id)).GetProperty<string>("PublicationReview").ShouldBe(evidence);
        var revision = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = read.ConcurrencyStamp });
        revision.ProrationPolicyVersionId.ShouldBeNull();
        (await repository.GetAsync(revision.Id)).ProrationPolicyVersionId.ShouldBeNull();
    }
}
