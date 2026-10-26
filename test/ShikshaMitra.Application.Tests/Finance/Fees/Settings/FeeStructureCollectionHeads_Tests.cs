using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    [Fact]
    public async Task Removed_Admission_Collection_Patch_Is_Rejected()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.PatchAsync(draft.Id,
            Patch(draft.ConcurrencyStamp, new { feeHeads = new { update = new[] {
                new { lineId = draft.FeeHeads.Single().Id, collectAtAdmission = true } } } })));
        typeof(FeeHeadAssignmentRequest).GetProperty("CollectAtAdmission").ShouldBeNull();
        typeof(FeeHeadAssignmentResponse).GetProperty("CollectAtAdmission").ShouldBeNull();
        typeof(FeeStructureDetail).GetProperty("CollectAtAdmission").ShouldBeNull();
    }

    [Fact]
    public async Task Retired_Create_Field_Cannot_Exclude_A_Head_Or_Remove_Its_Schedule()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var legacy = JsonSerializer.SerializeToNode(candidate)!;
        legacy["FeeHeads"]![0]!["CollectAtAdmission"] = true;
        candidate = legacy.Deserialize<StandardFeeStructureRequest>()!;
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        published.FeeHeads.Single().PaymentTermVersionId.ShouldBe(candidate.FeeHeads.Single().PaymentTermVersionId);
        published.PublicationReview!.Schedules.Count.ShouldBe(1);
        JsonSerializer.Serialize(published).ShouldNotContain("CollectAtAdmission");
    }

    [Fact]
    public async Task Obsolete_Exclusion_Review_Is_Hidden_Without_Rewriting_Publication_Evidence()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        var repository = GetRequiredService<IRepository<FeeStructure, Guid>>();
        string evidence;
        using (var hydration = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var stored = await repository.GetAsync(draft.Id);
            var frozen = JsonNode.Parse(stored.GetProperty<string>("PublicationReview")!)!;
            frozen["Coverage"]![0]!["Collection"] = "Admission only; excluded from Fee Collection";
            evidence = frozen.ToJsonString();
            stored.SetProperty("PublicationReview", evidence);
            await repository.UpdateAsync(stored, autoSave: true);
            await hydration.CompleteAsync();
        }
        var read = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Single();
        read.PublicationReview.ShouldBeNull();
        (await repository.GetAsync(draft.Id)).GetProperty<string>("PublicationReview").ShouldBe(evidence);
    }

    [Fact]
    public async Task Repair_Does_Not_Invent_Dues_For_A_Published_Head_Whose_Schedule_Was_Cleared()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (input, _) = await VariableAssignmentAsync(false, false, fixedAmount: 1000m);
        var students = GetRequiredService<IRepository<Student, Guid>>();
        var student = await students.GetAsync(input.StudentId);
        student.AssignFeeStructure(input.StandardFeeStructureId);
        await students.UpdateAsync(student, autoSave: true);
        var details = GetRequiredService<IRepository<FeeStructureDetail, Guid>>();
        var detail = (await details.GetListAsync(x => x.FeeStructureId == input.StandardFeeStructureId)).Single();
        detail.DetachPaymentTerm();
        await details.UpdateAsync(detail, autoSave: true);
        await WithUnitOfWorkAsync(() => GetRequiredService<FeeAssignmentRepairSeederContributor>()
            .SeedAsync(new DataSeedContext(TestTenantId)));
        (await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
        (await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_Configured_Head_Appears_In_Candidates_Assignment_Dues_And_Fee_Collection()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (input, firstHeadId) = await VariableAssignmentAsync(false, false, addAdditionalHead: true, fixedAmount: 1000m);
        var service = GetRequiredService<IStudentFeeStructureAppService>();
        var candidate = (await service.GetCandidatesAsync(input.StudentId, input.AcademicYearId))
            .Single(x => x.Id == input.StandardFeeStructureId);
        candidate.Total.ShouldBe(1750m);
        candidate.Heads.Count.ShouldBe(2);
        candidate.Heads.ShouldContain(x => x.FeeHeadId == firstHeadId);
        var preview = await service.PreviewAsync(input);
        preview.Heads.Count.ShouldBe(2);
        preview.ProratedTotal.ShouldBe(1750m);
        input.PreviewToken = preview.PreviewToken;
        await service.AssignAsync(input);
        var dues = await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId);
        dues.Count.ShouldBe(4);
        dues.Select(x => x.FeeHeadId).Distinct().Order().ShouldBe(candidate.Heads.Select(x => x.FeeHeadId).Order());
        dues.Sum(x => x.OriginalAmount).ShouldBe(1750m);
        var form = await GetRequiredService<IFeeCollectionAppService>().GetCollectFormAsync(input.StudentId, input.AcademicYearId);
        form.Ledger.SelectMany(x => x.Items).Select(x => x.FeeHeadId).Distinct().Order()
            .ShouldBe(candidate.Heads.Select(x => x.FeeHeadId).Order());
        (await service.AssignAsync(input)).Outcome.ShouldBe("AlreadyAssigned");
        (await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).Count.ShouldBe(4);
    }
}
