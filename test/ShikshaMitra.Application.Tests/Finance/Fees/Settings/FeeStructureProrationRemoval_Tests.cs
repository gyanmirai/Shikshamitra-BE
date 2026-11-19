using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
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
    public async Task Retired_Create_Fields_Do_Not_Attach_Or_Block_Publication()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        candidate.FeeHeads.Single().Amount = 1000m;
        var legacyInput = JsonSerializer.SerializeToNode(candidate)!.AsObject();
        legacyInput["ProrationPolicyId"] = Guid.NewGuid();
        legacyInput["ProrationPolicyVersionId"] = Guid.NewGuid();
        candidate = legacyInput.Deserialize<StandardFeeStructureRequest>()!;
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        published.PublicationReview!.IsReady.ShouldBeTrue();
        JsonSerializer.Serialize(published).ShouldNotContain("Proration");
        var stored = await GetRequiredService<IRepository<FeeStructure, Guid>>().GetAsync(draft.Id);
        stored.ProrationPolicyId.ShouldBeNull(); stored.ProrationPolicyVersionId.ShouldBeNull();
        typeof(FeeStructure).GetMethod("AttachProrationPolicy").ShouldBeNull();
        var context = await _feeStructures.GetContextAsync(metadata, standard);
        context.Permissions.ShouldNotContainKey("proration");
        context.Permissions.ShouldNotContainKey("prorationRead");
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
        JsonSerializer.Serialize(read).ShouldNotContain("Proration");
        read.PublicationReview!.Warnings.ShouldNotContain(x => x.Contains("proration", StringComparison.OrdinalIgnoreCase));
        (await repository.GetAsync(draft.Id)).GetProperty<string>("PublicationReview").ShouldBe(evidence);
        read.PublicationReview.Schedules.Single().Installments.Sum(x => x.CalculatedAmount).ShouldBe(1000m);
        var revision = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = read.ConcurrencyStamp });
        JsonSerializer.Serialize(revision).ShouldNotContain("Proration");
        (await repository.GetAsync(revision.Id)).ProrationPolicyVersionId.ShouldBeNull();
    }
}
