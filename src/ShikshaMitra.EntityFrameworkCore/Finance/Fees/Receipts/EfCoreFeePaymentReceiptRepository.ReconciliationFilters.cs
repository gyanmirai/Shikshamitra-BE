using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;

public partial class EfCoreFeePaymentReceiptRepository
{
    public async Task<List<ReconciliationRow>> GetReconciliationFilterCandidatesAsync(
        ReconciliationListFilter filter, int skipCount, int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var db = await GetDbContextAsync();
        var ids = await BuildReconciliationBaseQuery(db, filter)
            .OrderByDescending(r => r.CreationTime).ThenByDescending(r => r.ReceiptNumber).ThenByDescending(r => r.Id)
            .Skip(Math.Max(0, skipCount)).Take(Math.Clamp(maxResultCount, 1, 256))
            .Select(r => r.Id).ToListAsync(cancellationToken);
        return await ReadReconciliationRowsAsync(ids, false, cancellationToken);
    }

    public static IQueryable<FeePaymentReceipt> ApplyReconciliationScalarHeaders(
        IQueryable<FeePaymentReceipt> query, FinanceTableQuery table, PaymentMethodEnum? method)
    {
        var fields = new Dictionary<string, LambdaExpression>
        {
            ["receiptNumber"] = (Expression<Func<FeePaymentReceipt, string>>)(r => r.ReceiptNumber),
            ["amount"] = (Expression<Func<FeePaymentReceipt, decimal>>)(r => r.TotalPaidAmount),
            ["status"] = (Expression<Func<FeePaymentReceipt, int>>)(r =>
                r.VerificationStatus == FeeReceiptVerificationStatus.NotApplicable || r.VerificationStatus == FeeReceiptVerificationStatus.Verified
                    ? 1 : r.VerificationStatus == FeeReceiptVerificationStatus.Rejected ? 2 : 0),
            ["receiptDate"] = (Expression<Func<FeePaymentReceipt, DateOnly>>)(r => r.ReceiptDate)
        };
        return table.Apply(query, fields, ReconciliationHeaderColumns.IsReceipt);
    }

    private sealed class ContextHeaderRow
    {
        public FeePaymentReceipt Receipt { get; set; } = null!;
        public string StudentIdentity { get; set; } = "";
        public string BoardMedium { get; set; } = "";
        public string ClassDivision { get; set; } = "";
    }

    public static IQueryable<FeePaymentReceipt> ApplyReconciliationContextHeaders(
        IQueryable<FeePaymentReceipt> receipts, ShikshaMitraDbContext db, FinanceTableQuery table)
    {
        if (!table.Filters.Any(f => ReconciliationHeaderColumns.IsContext(f.Column))) return receipts;
        var rows = from receipt in receipts
                   join student in db.Set<Student>() on receipt.StudentId equals (Guid?)student.Id into sj
                   from student in sj.DefaultIfEmpty()
                   join account in db.Set<FinanceFeeAccount>() on receipt.FinanceFeeAccountId equals (Guid?)account.Id into aj
                   from account in aj.DefaultIfEmpty()
                   join board in db.Set<EducationBoard>() on (account != null ? (Guid?)account.BoardId : student.AcademicBoardId) equals (Guid?)board.Id into bj
                   from board in bj.DefaultIfEmpty()
                   join medium in db.Set<EducationMedium>() on (account != null ? account.EducationMediumId : student.AcademicMediumId) equals medium.Id into mj
                   from medium in mj.DefaultIfEmpty()
                   join standard in db.Set<Standard>() on (account != null ? (Guid?)account.StandardId : student.StandardId) equals (Guid?)standard.Id into stj
                   from standard in stj.DefaultIfEmpty()
                   join division in db.Set<Division>() on (account != null ? account.DivisionId : student.DivisionId) equals (Guid?)division.Id into dj
                   from division in dj.DefaultIfEmpty()
                   let name = account != null ? account.ApplicantName : student != null
                       ? (student.FirstName + " " + (student.MiddleName ?? "") + " " + student.LastName).Trim().Replace("  ", " ") : ""
                   // Preserve the existing enum labels as SQL constants rather than asking
                   // EF to translate Enum.ToString inside a filtering predicate.
                   let boardName = board == null ? "" : board.Type == EducationBoardType.CBSE ? "CBSE" : board.Type == EducationBoardType.GSEB ? "GSEB" : ((int)board.Type).ToString()
                   select new ContextHeaderRow
                   {
                       Receipt = receipt,
                       StudentIdentity = (name == "" ? "-" : name) + (student.GrNo.HasValue && student.GrNo != 0 ? " (" + student.GrNo.Value.ToString() + ")" : ""),
                       BoardMedium = (boardName == "" ? "-" : boardName) + (medium.Name == null || medium.Name == "" ? "" : " - (" + medium.Name + ")"),
                       ClassDivision = (standard.Name == null || standard.Name == "" ? "-" : standard.Name) +
                           (division.Name == null || division.Name == "" ? "" : " - (" + division.Name + ")")
                   };
        var fields = new Dictionary<string, LambdaExpression>
        {
            ["studentName"] = (Expression<Func<ContextHeaderRow, string>>)(x => x.StudentIdentity),
            ["board"] = (Expression<Func<ContextHeaderRow, string>>)(x => x.BoardMedium),
            ["standardName"] = (Expression<Func<ContextHeaderRow, string>>)(x => x.ClassDivision),
            ["class"] = (Expression<Func<ContextHeaderRow, string>>)(x => x.ClassDivision)
        };
        return table.Apply(rows, fields, ReconciliationHeaderColumns.IsContext).Select(x => x.Receipt);
    }
}
