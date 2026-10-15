using System;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Reports;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceReportPayerProjection_Tests
{
    [Fact]
    public void Applicant_and_student_money_are_both_included_without_fake_student()
    {
        var studentId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var receipts = new[] { Receipt(studentId, null, 100), Receipt(null, accountId, 250) };
        var payers = new[] { Payer(studentId, null), Payer(null, accountId) };
        var rows = FinanceReportPayerQueryService.FilterReceipts(receipts.AsQueryable(), payers).ToList();
        rows.Sum(r => r.TotalPaidAmount).ShouldBe(350);
        rows.Single(r => r.FinanceFeeAccountId.HasValue).StudentId.ShouldBeNull();
    }

    [Fact]
    public void Linked_student_cannot_bypass_the_original_account_scope()
    {
        var studentId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var receipts = new[] { Receipt(studentId, null, 100), Receipt(studentId, accountId, 250) };
        var onlyStudentScope = new[] { Payer(studentId, null) };
        FinanceReportPayerQueryService.FilterReceipts(receipts.AsQueryable(), onlyStudentScope)
            .Sum(r => r.TotalPaidAmount).ShouldBe(100);
        var accountScope = new[] { Payer(null, accountId) };
        FinanceReportPayerQueryService.FilterReceipts(receipts.AsQueryable(), accountScope)
            .Sum(r => r.TotalPaidAmount).ShouldBe(250);
    }

    [Fact]
    public void Reconciled_receipt_is_counted_once_and_rejected_receipts_are_excluded()
    {
        var accountId = Guid.NewGuid();
        var provisional = Receipt(null, accountId, 250);
        var final = Receipt(null, accountId, 250);
        provisional.LinkedFinalReceiptId = final.Id;
        var rejected = Receipt(null, accountId, 250);
        rejected.VerificationStatus = FeeReceiptVerificationStatus.Rejected;
        FinanceReportPayerQueryService.FilterReceipts(new[] { provisional, final, rejected }.AsQueryable(),
            new[] { Payer(null, accountId) }).Sum(r => r.TotalPaidAmount).ShouldBe(250);
    }

    [Fact]
    public void Original_account_group_key_survives_real_student_linking()
    {
        var accountId = Guid.NewGuid();
        FinanceReportPayerKey.From(null, accountId)
            .ShouldBe(FinanceReportPayerKey.From(Guid.NewGuid(), accountId));
        FinanceReportPayerKey.From(accountId, null)
            .ShouldNotBe(FinanceReportPayerKey.From(null, accountId));
    }

    [Fact]
    public void Reversed_collections_do_not_inflate_account_totals()
    {
        var studentId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var receipts = new[]
        {
            Metadata(null, accountId, FeeCollectionStatus.Paid, 250),
            Metadata(studentId, accountId, FeeCollectionStatus.Reversed, 300),
            Metadata(studentId, null, FeeCollectionStatus.Paid, 100)
        };
        FinanceReportPayerQueryService.FilterCollections(receipts.AsQueryable(),
            new[] { Payer(null, accountId), Payer(studentId, null) }).Sum(r => r.TotalPaidFee).ShouldBe(350);
        FinanceReportPayerQueryService.FilterCollections(receipts.AsQueryable(),
            new[] { Payer(studentId, null) }).Sum(r => r.TotalPaidFee).ShouldBe(100);
    }

    [Fact]
    public void Division_scope_does_not_expand_to_other_divisions_or_other_years()
    {
        var yearId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var mediumId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var divisionId = Guid.NewGuid();
        var allowed = Account(yearId, boardId, mediumId, classId, divisionId);
        var sibling = Account(yearId, boardId, mediumId, classId, Guid.NewGuid());
        var otherYear = Account(Guid.NewGuid(), boardId, mediumId, classId, divisionId);
        var scope = new UserScopeContext
        {
            AcademicYearIds = [yearId], BoardIds = [boardId], MediumIds = [mediumId],
            StandardIds = [classId], DivisionIds = [divisionId]
        };
        FinanceFeeAccountScopeQueryBuilder.ApplyScope(new[] { allowed, sibling, otherYear }.AsQueryable(), scope)
            .Select(a => a.Id).ShouldBe(new[] { allowed.Id });
    }

    private static FinanceReportPayer Payer(Guid? studentId, Guid? accountId) =>
        new(studentId, accountId, "QA payer", null, null, null, null);

    private static FeePaymentReceipt Receipt(Guid? studentId, Guid? accountId, double amount)
    {
        var receipt = new FeePaymentReceipt(Guid.NewGuid()) { StudentId = studentId, TotalPaidAmount = amount };
        if (accountId.HasValue) receipt.SetFinanceFeeAccount(accountId.Value);
        return receipt;
    }

    private static FeeRecordMetadata Metadata(Guid? studentId, Guid? accountId, FeeCollectionStatus status, double amount) =>
        new(Guid.NewGuid(), null, studentId, Guid.NewGuid(), amount, "QA", status, financeFeeAccountId: accountId);

    private static FinanceFeeAccount Account(Guid yearId, Guid boardId, Guid mediumId, Guid classId, Guid divisionId)
    {
        var account = new FinanceFeeAccount(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), yearId,
            boardId, mediumId, null, classId, "Original applicant");
        account.SetDivision(divisionId);
        return account;
    }
}
