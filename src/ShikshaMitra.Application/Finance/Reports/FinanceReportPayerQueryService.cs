using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports;

public readonly record struct FinanceReportPayerKey(Guid? StudentId, Guid? FinanceFeeAccountId)
{
    public static FinanceReportPayerKey From(Guid? studentId, Guid? accountId) =>
        accountId.HasValue ? new(null, accountId) : new(studentId, null);
}

public sealed record FinanceReportPayer(
    Guid? StudentId, Guid? FinanceFeeAccountId, string DisplayName,
    int? GrNo, int? RollNo, Guid? StandardId, Guid? DivisionId)
{
    public FinanceReportPayerKey Key => FinanceReportPayerKey.From(StudentId, FinanceFeeAccountId);
}

public class FinanceReportPayerQueryService(
    IRepository<Student, Guid> studentRepository,
    IRepository<FinanceFeeAccount, Guid> accountRepository,
    IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
    public static IQueryable<FeePaymentReceipt> FilterReceipts(IQueryable<FeePaymentReceipt> query,
        IReadOnlyCollection<FinanceReportPayer> payers)
    {
        var students = payers.Where(p => p.StudentId.HasValue && !p.FinanceFeeAccountId.HasValue)
            .Select(p => p.StudentId!.Value).ToList();
        var accounts = payers.Where(p => p.FinanceFeeAccountId.HasValue)
            .Select(p => p.FinanceFeeAccountId!.Value).ToList();
        return query.Where(r => r.VerificationStatus != FeeReceiptVerificationStatus.Rejected && r.LinkedFinalReceiptId == null)
            .Where(r => (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && students.Contains(r.StudentId.Value)) ||
                (r.FinanceFeeAccountId.HasValue && accounts.Contains(r.FinanceFeeAccountId.Value)));
    }

    public static IQueryable<FeeRecordMetadata> FilterCollections(IQueryable<FeeRecordMetadata> query,
        IReadOnlyCollection<FinanceReportPayer> payers)
    {
        var students = payers.Where(p => p.StudentId.HasValue && !p.FinanceFeeAccountId.HasValue)
            .Select(p => p.StudentId!.Value).ToList();
        var accounts = payers.Where(p => p.FinanceFeeAccountId.HasValue)
            .Select(p => p.FinanceFeeAccountId!.Value).ToList();
        return query.Where(r => r.Status != FeeCollectionStatus.Reversed)
            .Where(r => (!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && students.Contains(r.StudentId.Value)) ||
                (r.FinanceFeeAccountId.HasValue && accounts.Contains(r.FinanceFeeAccountId.Value)));
    }

    public async Task<List<FinanceReportPayer>> GetPayersAsync(FinanceReportFilterDto filters,
        FinanceReportExecutionScope scope)
    {
        var years = filters.AcademicYearIds ?? [];
        var organizations = filters.OrganizationIds ?? [];
        var classes = filters.ClassIds ?? [];
        var divisions = filters.DivisionIds ?? [];
        var studentQuery = StudentScopeAccessService.ApplyScope(
                (await studentRepository.GetQueryableAsync()).Where(s => s.TenantId == scope.TenantId), scope.UserScope)
            .WhereIf(years.Count > 0, s => s.AcademicYearId.HasValue && years.Contains(s.AcademicYearId.Value))
            .WhereIf(filters.BatchId.HasValue, s => s.AcademicYearId == filters.BatchId)
            .WhereIf(filters.BoardId.HasValue, s => s.AcademicBoardId == filters.BoardId)
            .WhereIf(filters.MediumId.HasValue, s => s.AcademicMediumId == filters.MediumId)
            .WhereIf(organizations.Count > 0, s => s.AcademicMediumStageId.HasValue && organizations.Contains(s.AcademicMediumStageId.Value))
            .WhereIf(classes.Count > 0, s => s.StandardId.HasValue && classes.Contains(s.StandardId.Value))
            .WhereIf(divisions.Count > 0, s => s.DivisionId.HasValue && divisions.Contains(s.DivisionId.Value));
        var students = await asyncExecuter.ToListAsync(studentQuery.Select(s => new
        {
            s.Id, s.FirstName, s.MiddleName, s.LastName,
            s.GrNo, s.RollNo, s.StandardId, s.DivisionId
        }));
        var accountsQuery = FinanceFeeAccountScopeQueryBuilder.ApplyScope(
                (await accountRepository.GetQueryableAsync()).Where(a => a.TenantId == scope.TenantId), scope.UserScope)
            .WhereIf(years.Count > 0, a => years.Contains(a.AcademicYearId))
            .WhereIf(filters.BatchId.HasValue, a => a.AcademicYearId == filters.BatchId)
            .WhereIf(filters.BoardId.HasValue, a => a.BoardId == filters.BoardId)
            .WhereIf(filters.MediumId.HasValue, a => a.EducationMediumId == filters.MediumId)
            .WhereIf(organizations.Count > 0, a => a.MediumStageId.HasValue && organizations.Contains(a.MediumStageId.Value))
            .WhereIf(classes.Count > 0, a => classes.Contains(a.StandardId))
            .WhereIf(divisions.Count > 0, a => a.DivisionId.HasValue && divisions.Contains(a.DivisionId.Value));
        var accounts = await asyncExecuter.ToListAsync(accountsQuery.Select(a => new
        {
            a.Id, a.StudentId, a.ApplicantName, a.StandardId, a.DivisionId
        }));
        var studentMap = students.ToDictionary(s => s.Id);
        var result = students.Select(s => new FinanceReportPayer(s.Id, null,
            string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }.Where(p => !string.IsNullOrWhiteSpace(p))),
            s.GrNo, s.RollNo, s.StandardId, s.DivisionId)).ToList();
        result.AddRange(accounts.Select(a =>
        {
            var student = a.StudentId.HasValue ? studentMap.GetValueOrDefault(a.StudentId.Value) : null;
            return new FinanceReportPayer(a.StudentId, a.Id, a.ApplicantName,
                student?.GrNo, student?.RollNo, a.StandardId, a.DivisionId);
        }));
        return result;
    }
}
