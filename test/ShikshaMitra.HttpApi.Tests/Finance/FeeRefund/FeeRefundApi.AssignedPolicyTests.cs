using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeRefunds;

public partial class FeeRefundApi_Tests
{
    [Fact]
    public async Task Student_eligibility_filters_inaccessible_linked_account_sources_and_preserves_legacy_receipts()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, includePaidCharges: true);
        var charge = await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, source.StudentId, combined: false);
        var accountId = await FeeRefundTestHelper.AdoptLegacyAssignmentAsync(Factory.Services, source.StudentId, source.DueFeeId);
        var readerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, readerId);
        Guid legacyReceiptId;
        using (var scope = Factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            using var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var scopeRepository = provider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
            foreach (var saved in await scopeRepository.GetListAsync(x => x.UserId == readerId))
            {
                saved.Deactivate();
                await scopeRepository.UpdateAsync(saved);
            }
            await scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), provider.GetRequiredService<ICurrentTenant>().Id,
                readerId, academicYearId: FeeRefundTestHelper.DefaultAcademicYearId, educationBoardId: FeeRefundTestHelper.DefaultBoardId,
                description: "Refund fixture restricted board scope"));
            var accounts = provider.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>();
            var account = await accounts.GetAsync(accountId);
            var db = provider.GetRequiredService<ShikshaMitraDbContext>();
            db.Entry(account).Property(x => x.BoardId).CurrentValue = Guid.NewGuid();
            await accounts.UpdateAsync(account);
            var receipts = provider.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
            legacyReceiptId = (await receipts.GetListAsync(x => x.StudentId == source.StudentId && x.Id != charge.ReceiptId)).Single().Id;
            var receipt = await receipts.GetAsync(charge.ReceiptId);
            receipt.SetFinanceFeeAccount(accountId);
            await receipts.UpdateAsync(receipt);
            var collections = provider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
            var collection = await collections.GetAsync(receipt.FeeCollectionTransactionId);
            db.Entry(collection).Property(x => x.FinanceFeeAccountId).CurrentValue = accountId;
            await collections.UpdateAsync(collection);
            await uow.CompleteAsync();
            await provider.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(readerId);
        }
        using var reader = CreateClientAs(readerId);
        var url = $"{BaseUrl}/eligibility?studentId={source.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}";
        var allowed = (await reader.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!;
        allowed.TotalRefundable.ShouldBe(1000m);
        allowed.FeeHeads.SelectMany(x => x.Months).ShouldNotContain(x => x.ChargeId.HasValue);
        allowed.Receipts.ShouldNotContain(x => x.ReceiptId == charge.ReceiptId);
        (await reader.GetAsync(url + $"&financeFeeAccountId={accountId}")).StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
        var legacy = (await reader.GetFromJsonAsync<RefundEligibilityDto>(url + $"&originalReceiptId={legacyReceiptId}", JsonOptions))!;
        legacy.TotalRefundable.ShouldBe(1000m);
        legacy.FeeHeads.SelectMany(x => x.Months).ShouldNotContain(x => x.ChargeId.HasValue);
    }

    [Fact]
    public async Task Student_eligibility_excludes_an_account_that_only_claims_the_assignment()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            includePaidCharges: true, includePaidLateFee: true);
        var charge = await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, source.StudentId, combined: false);
        var late = await FeeRefundTestHelper.SeedPaidLateFeeAsync(Factory.Services, source.StudentId, source.DueFeeId);
        var currentAccountId = await FeeRefundTestHelper.AdoptLegacyAssignmentAsync(Factory.Services, source.StudentId, source.DueFeeId);
        using (var scope = Factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            using var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var accounts = provider.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>();
            var current = await accounts.GetAsync(currentAccountId);
            var stale = new FinanceFeeAccount(Guid.NewGuid(), current.TenantId, Guid.NewGuid(), Guid.NewGuid(),
                current.AcademicYearId, current.BoardId, current.EducationMediumId, current.MediumStageId,
                current.StandardId, "Stale refund fixture", studentId: source.StudentId);
            stale.CaptureAssignment(current.StructureId!.Value, current.StructureRevision!.Value, current.AssignmentSnapshotJson!, "stale-refund-fixture");
            stale.BindStudentAssignment(current.StudentFeeStructureId!.Value);
            await accounts.InsertAsync(stale);
            var receipts = provider.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
            var collections = provider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
            foreach (var id in new[] { charge.ReceiptId, late.ReceiptId })
            {
                var receipt = await receipts.GetAsync(id);
                receipt.SetFinanceFeeAccount(stale.Id);
                var collection = await collections.GetAsync(receipt.FeeCollectionTransactionId);
                provider.GetRequiredService<ShikshaMitraDbContext>().Entry(collection)
                    .Property(x => x.FinanceFeeAccountId).CurrentValue = stale.Id;
                await receipts.UpdateAsync(receipt);
                await collections.UpdateAsync(collection);
            }
            await uow.CompleteAsync();
        }
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={source.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        eligibility.TotalRefundable.ShouldBe(1000m);
        eligibility.FeeHeads.SelectMany(x => x.Months).ShouldNotContain(x => x.ChargeId.HasValue || x.LateFeeDueFeeId.HasValue);
    }

    [Fact]
    public async Task Mixed_legacy_and_account_sources_expose_original_payers_and_require_separate_refunds()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            includePaidCharges: true, includePaidLateFee: true);
        var charge = await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, source.StudentId, amount: 250, combined: false);
        var late = await FeeRefundTestHelper.SeedPaidLateFeeAsync(Factory.Services, source.StudentId, source.DueFeeId, amount: 125);
        var accountId = await FeeRefundTestHelper.AdoptLegacyAssignmentAsync(Factory.Services, source.StudentId, source.DueFeeId);
        using (var scope = Factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            using var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var receipts = provider.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
            var collections = provider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
            foreach (var id in new[] { charge.ReceiptId, late.ReceiptId })
            {
                var receipt = await receipts.GetAsync(id);
                receipt.SetFinanceFeeAccount(accountId);
                var collection = await collections.GetAsync(receipt.FeeCollectionTransactionId);
                provider.GetRequiredService<ShikshaMitraDbContext>().Entry(collection)
                    .Property(x => x.FinanceFeeAccountId).CurrentValue = accountId;
                await receipts.UpdateAsync(receipt);
                await collections.UpdateAsync(collection);
            }
            await uow.CompleteAsync();
        }
        var response = await Client.GetAsync($"{BaseUrl}/eligibility?studentId={source.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var eligibility = JsonSerializer.Deserialize<RefundEligibilityDto>(json, JsonOptions)!;
        eligibility.TotalRefundable.ShouldBe(1375m);
        using var document = JsonDocument.Parse(json);
        var payers = document.RootElement.GetProperty("receipts").EnumerateArray().ToDictionary(
            x => x.GetProperty("receiptId").GetGuid(),
            x => x.TryGetProperty("financeFeeAccountId", out var payer) && payer.ValueKind == JsonValueKind.String ? payer.GetGuid() : (Guid?)null);
        payers[charge.ReceiptId].ShouldBe(accountId);
        payers[late.ReceiptId].ShouldBe(accountId);
        var rows = eligibility.FeeHeads.SelectMany(x => x.Months).ToList();
        payers[rows.Single(x => x.DueFeeId.HasValue).SourceReceiptId].ShouldBeNull();
        CreateFeeRefundRequestDto Input(System.Collections.Generic.List<RefundEligibilityMonthDto> selected) => new()
        {
            StudentId = source.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = selected[0].SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = selected.Select(x => new CreateFeeRefundLineDto
            {
                DueFeeId = x.DueFeeId, ChargeId = x.ChargeId, LateFeeDueFeeId = x.LateFeeDueFeeId,
                SourceAllocationId = x.SourceAllocationId, SourceReceiptId = x.SourceReceiptId, Amount = x.Refundable
            }).ToList()
        };
        var mixed = await Client.PostAsJsonAsync($"{BaseUrl}/preview", Input(rows), JsonOptions);
        mixed.IsSuccessStatusCode.ShouldBeFalse();
        (await mixed.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.SourceReceiptInvalid);
        foreach (var group in rows.GroupBy(x => payers[x.SourceReceiptId]))
        {
            var selected = group.ToList();
            var created = await Client.PostAsJsonAsync(BaseUrl, Input(selected), JsonOptions);
            created.IsSuccessStatusCode.ShouldBeTrue(await created.Content.ReadAsStringAsync());
            var refund = (await created.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
            refund.CalculatedAmount.ShouldBe(selected.Sum(x => x.Refundable));
            refund.Lines.Count.ShouldBe(selected.Count);
        }
    }

    [Fact]
    public async Task Student_eligibility_preserves_legacy_paid_extras_after_assignment_account_adoption()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            includePaidCharges: true, includePaidLateFee: true, lateFeeAmount: 125);
        await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, source.StudentId, amount: 250);
        await FeeRefundTestHelper.AdoptLegacyAssignmentAsync(Factory.Services, source.StudentId, source.DueFeeId);
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={source.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        eligibility.TotalRefundable.ShouldBe(1375m);
        eligibility.FeeHeads.SelectMany(x => x.Months).Count().ShouldBe(3);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Linked_account_refunds_use_explicitly_updated_assignment_policy_without_rewriting_original_snapshot(bool accountRoute, bool includeExtras)
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            lateFeeAmount: 125, attachCollectionPolicy: true, includePaidCharges: !includeExtras, includePaidLateFee: !includeExtras);
        await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, source.StudentId, amount: 250);
        var accountId = await FeeRefundTestHelper.AdoptLegacyAssignmentAsync(Factory.Services, source.StudentId, source.DueFeeId);
        Guid updatedVersionId;
        string originalSnapshot;
        using (var scope = Factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            using var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var account = await provider.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetAsync(accountId);
            originalSnapshot = account.AssignmentSnapshotJson!;
            var assignments = provider.GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
            var assignment = await assignments.GetAsync(account.StudentFeeStructureId!.Value);
            var versions = provider.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>();
            var original = await versions.GetAsync(assignment.RefundPolicyVersionId!.Value);
            var snapshot = JsonSerializer.Deserialize<RefundPolicySnapshot>(original.SnapshotJson)!;
            updatedVersionId = Guid.NewGuid();
            await versions.InsertAsync(new FinancePolicyVersion(updatedVersionId, provider.GetRequiredService<ICurrentTenant>().Id, "refund", original.PolicyId,
                original.AcademicYearId, original.VersionNumber + 1, original.PolicyName, "Active",
                JsonSerializer.Serialize(snapshot with { IncludePaidCharges = includeExtras, IncludePaidLateFee = includeExtras }), "edited", "Update settled extras eligibility"));
            assignment.ChangeAssignedPolicies(assignment.ConcessionPolicyId, assignment.ConcessionPolicyVersionId,
                assignment.ConcessionFeeHeadScope, [], assignment.CollectionPolicyId!.Value, assignment.CollectionPolicyVersionId!.Value,
                original.PolicyId, updatedVersionId, assignment.ProrationPolicyId, assignment.ProrationPolicyVersionId,
                "Apply updated refund eligibility", Guid.NewGuid(), DateTime.UtcNow);
            await assignments.UpdateAsync(assignment);
            // Model account-native settled payment evidence after enrollment.
            var receipts = provider.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
            var collections = provider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
            foreach (var receipt in await receipts.GetListAsync(x => x.StudentId == source.StudentId))
            {
                receipt.SetFinanceFeeAccount(accountId);
                var collection = await collections.GetAsync(receipt.FeeCollectionTransactionId);
                provider.GetRequiredService<ShikshaMitraDbContext>().Entry(collection)
                    .Property(x => x.FinanceFeeAccountId).CurrentValue = accountId;
                await receipts.UpdateAsync(receipt);
                await collections.UpdateAsync(collection);
            }
            await uow.CompleteAsync();
        }
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={source.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}" +
                (accountRoute ? $"&financeFeeAccountId={accountId}" : ""), JsonOptions))!;
        eligibility.RefundPolicyVersionId.ShouldBe(updatedVersionId);
        eligibility.FeeHeads.Any(x => x.FeeHeadName == "Paid charges" && x.TotalRefundable == 250m).ShouldBe(includeExtras);
        eligibility.FeeHeads.Any(x => x.FeeHeadName == "Paid late fees" && x.TotalRefundable == 125m).ShouldBe(includeExtras);
        var expected = includeExtras ? 1375m : 1000m;
        eligibility.TotalRefundable.ShouldBe(expected);
        if (accountRoute)
        {
            var otherStudentId = await FeeRefundTestHelper.SeedStudentOnlyAsync(Factory.Services);
            var wrongStudent = await Client.GetAsync($"{BaseUrl}/eligibility?studentId={otherStudentId}&financeFeeAccountId={accountId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");
            wrongStudent.IsSuccessStatusCode.ShouldBeFalse();
            var wrongYear = await Client.GetAsync($"{BaseUrl}/eligibility?studentId={source.StudentId}&financeFeeAccountId={accountId}&academicYearId={Guid.NewGuid()}");
            wrongYear.IsSuccessStatusCode.ShouldBeFalse();
        }
        var rows = eligibility.FeeHeads.SelectMany(x => x.Months).ToList();
        var create = await Client.PostAsJsonAsync(BaseUrl, new CreateFeeRefundRequestDto
        {
            StudentId = source.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = rows[0].SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = rows.Select(x => new CreateFeeRefundLineDto
            {
                DueFeeId = x.DueFeeId, ChargeId = x.ChargeId, LateFeeDueFeeId = x.LateFeeDueFeeId,
                SourceAllocationId = x.SourceAllocationId, SourceReceiptId = x.SourceReceiptId, Amount = x.Refundable
            }).ToList()
        }, JsonOptions);
        create.IsSuccessStatusCode.ShouldBeTrue(await create.Content.ReadAsStringAsync());
        var request = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        request.RefundPolicyVersionId.ShouldBe(updatedVersionId);
        request.Lines.Count.ShouldBe(includeExtras ? 3 : 1);
        request.CalculatedAmount.ShouldBe(expected);
        foreach (var row in rows)
            request.Lines.ShouldContain(line => line.SourceAllocationId == row.SourceAllocationId &&
                line.SourceReceiptId == row.SourceReceiptId && line.DueFeeId == row.DueFeeId &&
                line.ChargeId == row.ChargeId && line.LateFeeDueFeeId == row.LateFeeDueFeeId);
        using var verification = Factory.Services.CreateScope();
        using var verificationTenant = verification.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        using var verificationUow = verification.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        (await verification.ServiceProvider.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().GetAsync(accountId))
            .AssignmentSnapshotJson.ShouldBe(originalSnapshot);
    }
}
