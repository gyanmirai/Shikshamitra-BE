using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    [Fact]
    public async Task Assignment_preview_batches_a_shared_payment_term_for_ten_heads()
    {
        using var reviewer = Reviewer();
        using var tenant = _currentTenant.Change(TestTenantId);
        var (metadataId, standardId, candidate) = await ReadyCandidateAsync();
        candidate.ConcessionPolicyId = null;
        candidate.ConcessionPolicyVersionId = null;
        var first = candidate.FeeHeads.Single();
        first.Amount = 1000m;
        var head = await _feeHeadRepo.GetAsync(first.FeeHeadId);
        var others = Enumerable.Range(1, 9).Select(index => new FeeHead(Guid.NewGuid(), TestTenantId,
            head.AcademicYearId, head.FeeCategoryId, $"Assignment batch head {index}", head.Module, true, null, head.IncomeCategoryId)).ToList();
        await _feeHeadRepo.InsertManyAsync(others, true);
        candidate.FeeHeads.AddRange(others.Select(other => new FeeHeadAssignmentRequest {
            FeeHeadId = other.Id, Amount = 1000m, PaymentTermId = first.PaymentTermId, PaymentTermVersionId = first.PaymentTermVersionId
        }));
        var draft = await _feeStructures.CreateAsync(metadataId, new() { StandardId = standardId, StandardFeeStructures = candidate });
        var structure = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        var metadata = await _metadataRepo.GetAsync(metadataId);
        var standard = await _standardRepo.GetAsync(standardId);
        var student = new Student(Guid.NewGuid(), TestTenantId, "Shared Term", null, "Student", metadata.AcademicYearId!.Value,
            metadata.AcademicBoardId!.Value, metadata.AcademicMediumId!.Value, standard.EducationMediumStageId,
            standardId, status: StudentLifeCycleStatusEnum.Enrolled);
        await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);

        await WithUnitOfWorkAsync(async () =>
        {
            var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
            var preview = await GetRequiredService<IStudentFeeStructureAppService>().PreviewAsync(new AssignStudentFeeStructureDto {
                StudentId = student.Id, AcademicYearId = metadata.AcademicYearId.Value, StandardFeeStructureId = structure.Id
            });
            preview.Heads.Count.ShouldBe(10);
            preview.Lines.Count.ShouldBe(20);
            preview.ProratedTotal.ShouldBe(10000m);
            preview.Heads.ShouldAllBe(item => item.PaymentTermVersionId == first.PaymentTermVersionId);
            var termReads = reads.Commands.Count(sql => sql.Contains("FROM \"Finance_PaymentTerms\"", StringComparison.Ordinal));
            termReads.ShouldBeGreaterThan(0, "the real query capture must observe the calculation's term read");
            termReads.ShouldBeLessThanOrEqualTo(2, "ten heads sharing one published term must not reload term history per head or during due calculation");
        });
    }
}
