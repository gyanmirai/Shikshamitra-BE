using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    [Fact]
    public async Task Variable_Patch_Publication_And_Revision_Preserve_Unpriced_Terms_And_Optionality()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var line = draft.FeeHeads.Single();
        var patch = Patch(draft.ConcurrencyStamp, new { feeHeads = new { update = new[] { new { lineId = line.Id, isVariable = true, isOptional = true, amount = (decimal?)null } } } });
        var variable = await _feeStructures.PatchAsync(draft.Id, patch);
        variable.FeeHeads.Single().Amount.ShouldBeNull();
        variable.FeeHeads.Single().IsVariable.ShouldBeTrue();
        variable.FeeHeads.Single().IsOptional.ShouldBeTrue();
        variable.FeeHeads.Single().PaymentTermVersionId.ShouldBe(line.PaymentTermVersionId);
        (await _feeStructures.PatchAsync(draft.Id, patch)).ConcurrencyStamp.ShouldBe(variable.ConcurrencyStamp);
        var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = variable.ConcurrencyStamp });
        published.PublicationReview!.Schedules.Single().AnnualAmount.ShouldBeNull();
        published.PublicationReview.Schedules.Single().Installments.ShouldAllBe(x => x.CalculatedAmount == null);
        var revision = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = published.ConcurrencyStamp });
        revision.FeeHeads.Single().IsVariable.ShouldBeTrue();
        revision.FeeHeads.Single().Amount.ShouldBeNull();
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.PatchAsync(revision.Id,
            Patch(revision.ConcurrencyStamp, new { feeHeads = new { update = new[] { new { lineId = revision.FeeHeads.Single().Id, isVariable = false } } } })));
        var fixedRevision = await _feeStructures.PatchAsync(revision.Id, Patch(revision.ConcurrencyStamp,
            new { feeHeads = new { update = new[] { new { lineId = revision.FeeHeads.Single().Id, isVariable = false, amount = 2500m } } } }));
        fixedRevision.FeeHeads.Single().Amount.ShouldBe(2500m);
        fixedRevision.FeeHeads.Single().IsVariable.ShouldBeFalse();
        var original = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Single(x => x.Id == published.Id);
        original.FeeHeads.Single().Amount.ShouldBeNull();
        original.FeeHeads.Single().IsVariable.ShouldBeTrue();
    }

    private async Task<(AssignStudentFeeStructureDto Input, Guid HeadId)> VariableAssignmentAsync(bool variable, bool optional, bool addAdditionalHead = false, decimal fixedAmount = 1000.01m)
    {
        var (metadataId, standardId, candidate) = await ReadyCandidateAsync();
        candidate.ConcessionPolicyId = null;
        candidate.ConcessionPolicyVersionId = null;
        var head = candidate.FeeHeads.Single();
        head.IsVariable = variable;
        head.IsOptional = optional;
        head.Amount = variable ? null : fixedAmount;
        if (addAdditionalHead)
        {
            var original = await _feeHeadRepo.GetAsync(head.FeeHeadId);
            var additionalHead = new FeeHead(Guid.NewGuid(), TestTenantId, original.AcademicYearId,
                original.FeeCategoryId, "Additional fee", original.Module, false, null, original.IncomeCategoryId);
            await _feeHeadRepo.InsertAsync(additionalHead, autoSave: true);
            candidate.FeeHeads.Add(new() { FeeHeadId = additionalHead.Id, Amount = 750m, PaymentTermId = head.PaymentTermId, PaymentTermVersionId = head.PaymentTermVersionId });
        }
        var draft = await _feeStructures.CreateAsync(metadataId, new() { StandardId = standardId, StandardFeeStructures = candidate });
        draft.FeeHeads.Single(x => x.FeeHeadId == head.FeeHeadId).IsVariable.ShouldBe(variable);
        draft.FeeHeads.Single(x => x.FeeHeadId == head.FeeHeadId).Amount.ShouldBe(variable ? null : Math.Ceiling(fixedAmount));
        var structure = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        var metadata = await _metadataRepo.GetAsync(metadataId);
        var standard = await _standardRepo.GetAsync(standardId);
        var student = new Student(Guid.NewGuid(), TestTenantId, "Variable", null, "Student", metadata.AcademicYearId!.Value,
            metadata.AcademicBoardId!.Value, metadata.AcademicMediumId!.Value, standard.EducationMediumStageId, standardId, status: StudentLifeCycleStatusEnum.Enrolled);
        await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);
        return (new() { StudentId = student.Id, AcademicYearId = metadata.AcademicYearId.Value, StandardFeeStructureId = structure.Id }, head.FeeHeadId);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Assignment_Four_Pricing_Conditions_Create_Only_Selected_Dues(bool variable, bool optional, bool selected)
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (input, headId) = await VariableAssignmentAsync(variable, optional);
        var included = !optional || selected;
        if (selected) input.SelectedOptionalFeeHeadIds.Add(headId);
        if (variable && included) input.VariableFeeHeads.Add(new() { FeeHeadId = headId, Amount = 1234.01m });
        var service = GetRequiredService<IStudentFeeStructureAppService>();
        if (!included)
        {
            (await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
            await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input));
            (await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
            (await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
            return;
        }
        var preview = await service.PreviewAsync(input);
        preview.Heads.Single().Included.ShouldBe(included);
        preview.Heads.Single().ConfiguredAmount.ShouldBe(variable ? null : 1001m);
        preview.ProratedTotal.ShouldBe(included ? variable ? 1235m : 1001m : 0m);
        input.PreviewToken = preview.PreviewToken;
        var result = await service.AssignAsync(input);
        result.AssignmentId.ShouldNotBe(Guid.Empty);
        var dues = await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId);
        dues.Count.ShouldBe(included ? 2 : 0);
        dues.Sum(x => x.OriginalAmount).ShouldBe(preview.ProratedTotal);
        (await service.AssignAsync(input)).Outcome.ShouldBe("AlreadyAssigned");
        (await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).Count.ShouldBe(dues.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Included_Variable_Head_Without_Amount_Cannot_Preview_Or_Write(bool optional)
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (input, headId) = await VariableAssignmentAsync(true, optional);
        if (optional) input.SelectedOptionalFeeHeadIds.Add(headId);
        var service = GetRequiredService<IStudentFeeStructureAppService>();
        (await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.VariableAmountRequired);
        (await GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
        (await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetListAsync(x => x.StudentId == input.StudentId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Variable_Amount_Rejects_Stale_Preview_And_Records_Explicit_Zero_Reason()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (input, headId) = await VariableAssignmentAsync(true, false);
        var service = GetRequiredService<IStudentFeeStructureAppService>();
        input.VariableFeeHeads.Add(new() { FeeHeadId = headId, Amount = 100m });
        input.PreviewToken = (await service.PreviewAsync(input)).PreviewToken;
        input.VariableFeeHeads[0].Amount = 200m;
        (await Should.ThrowAsync<BusinessException>(() => service.AssignAsync(input))).Code.ShouldBe(AssignmentErrorCodes.StalePreview);
        input.VariableFeeHeads[0].Amount = 0;
        (await Should.ThrowAsync<BusinessException>(() => service.PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
        input.VariableFeeHeads[0].ZeroAmountReason = "Approved scholarship";
        var preview = await service.PreviewAsync(input);
        preview.ProratedTotal.ShouldBe(0m);
        preview.Heads.Single().ZeroAmountReason.ShouldBe("Approved scholarship");
        input.PreviewToken = preview.PreviewToken;
        (await service.AssignAsync(input)).Assignment.Heads.Single().ZeroAmountReason.ShouldBe("Approved scholarship");
    }

    [Fact]
    public async Task Variable_Amounts_For_Skipped_Or_Fixed_Heads_Are_Rejected()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (input, headId) = await VariableAssignmentAsync(true, true);
        input.VariableFeeHeads.Add(new() { FeeHeadId = headId, Amount = 100m });
        (await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
        input.VariableFeeHeads[0].FeeHeadId = Guid.NewGuid();
        (await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(input))).Code.ShouldBe(AssignmentErrorCodes.InvalidInput);
    }
}
