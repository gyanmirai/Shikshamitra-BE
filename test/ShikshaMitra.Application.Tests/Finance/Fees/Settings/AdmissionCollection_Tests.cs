using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    [Fact]
    public async Task Mixed_Structure_Generates_Only_Regular_Head_Dues()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (input, regularHeadId) = await VariableAssignmentAsync(false, false, addAdmissionHead: true, fixedAmount: 1000m);
        var service = GetRequiredService<IStudentFeeStructureAppService>();
        var candidate = (await service.GetCandidatesAsync(input.StudentId, input.AcademicYearId))
            .Single(x => x.Id == input.StandardFeeStructureId);
        candidate.Total.ShouldBe(1000m);
        candidate.Heads.Single().FeeHeadId.ShouldBe(regularHeadId);
        var preview = await service.PreviewAsync(input);
        preview.Lines.ShouldAllBe(x => x.FeeHeadId == regularHeadId);
        preview.ProratedTotal.ShouldBe(1000m);
        input.PreviewToken = preview.PreviewToken;
        await service.AssignAsync(input);
        var dues = await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId);
        dues.Count.ShouldBe(2);
        dues.ShouldAllBe(x => x.FeeHeadId == regularHeadId);
        dues.Sum(x => x.OriginalAmount).ShouldBe(1000m);
    }

    [Fact]
    public async Task Admission_Collection_Patch_Retry_Publication_And_Revision_Preserve_Choice()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        draft.FeeHeads.Single().CollectAtAdmission.ShouldBeFalse();
        var patch = Patch(draft.ConcurrencyStamp, new { feeHeads = new { update = new[] {
            new { lineId = draft.FeeHeads.Single().Id, collectAtAdmission = true } } } });
        var changed = await _feeStructures.PatchAsync(draft.Id, patch);
        changed.FeeHeads.Single().CollectAtAdmission.ShouldBeTrue();
        changed.FeeHeads.Single().PaymentTermId.ShouldBeNull();
        (await _feeStructures.PatchAsync(draft.Id, patch)).ConcurrencyStamp.ShouldBe(changed.ConcurrencyStamp);
        var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = changed.ConcurrencyStamp });
        published.PublicationReview!.Schedules.ShouldBeEmpty();
        published.PublicationReview.Coverage.Single().Accounting.ShouldContain("Excluded from income");
        published.PublicationReview.Coverage.Single().Collection.ShouldContain("excluded from Fee Collection");
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.PatchAsync(published.Id,
            Patch(published.ConcurrencyStamp, new { feeHeads = new { update = new[] {
                new { lineId = published.FeeHeads.Single().Id, collectAtAdmission = false } } } })));
        var revision = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = published.ConcurrencyStamp });
        revision.FeeHeads.Single().CollectAtAdmission.ShouldBeTrue();
        var regular = await _feeStructures.PatchAsync(revision.Id, Patch(revision.ConcurrencyStamp,
            new { feeHeads = new { update = new[] { new { lineId = revision.FeeHeads.Single().Id, collectAtAdmission = false } } } }));
        regular.FeeHeads.Single().CollectAtAdmission.ShouldBeFalse();
        // Switching back requires a regular payment schedule before publication.
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.PublishAsync(regular.Id, new() { ConcurrencyStamp = regular.ConcurrencyStamp }));
        (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Single(x => x.Id == published.Id)
            .FeeHeads.Single().CollectAtAdmission.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Admission_Only_Heads_Create_No_Collectible_Dues_Even_When_Selected(bool variable, bool optional)
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (input, headId) = await VariableAssignmentAsync(variable, optional, collectAtAdmission: true, fixedAmount: 1000m);
        if (optional) input.SelectedOptionalFeeHeadIds.Add(headId);
        var service = GetRequiredService<IStudentFeeStructureAppService>();
        var transactions = GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction, Guid>>();
        var incomeCount = await transactions.GetCountAsync();
        var candidates = await service.GetCandidatesAsync(input.StudentId, input.AcademicYearId);
        var candidate = candidates.Single(x => x.Id == input.StandardFeeStructureId);
        candidate.Total.ShouldBe(0);
        candidate.Heads.ShouldBeEmpty();
        var preview = await service.PreviewAsync(input);
        preview.Heads.ShouldBeEmpty();
        preview.Lines.ShouldBeEmpty();
        preview.ProratedTotal.ShouldBe(0);
        input.PreviewToken = preview.PreviewToken;
        var result = await service.AssignAsync(input);
        result.Assignment.Lines.ShouldBeEmpty();
        (await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
        (await service.AssignAsync(input)).Outcome.ShouldBe("AlreadyAssigned");
        (await transactions.GetCountAsync()).ShouldBe(incomeCount);
    }
}
