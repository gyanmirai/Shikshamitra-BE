using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;

namespace ShikshaMitra.Finance.Accounts.Activity;

public partial class AccountsActivityAppService
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> WorkspaceFields =
        new Dictionary<string, LambdaExpression>
        {
            ["reference"] = (Expression<Func<WorkspaceRecord, string>>)(x => x.Reference),
            ["studentIdentity"] = (Expression<Func<WorkspaceRecord, string>>)(x => x.StudentId == null && x.FinanceFeeAccountId == null ? "" :
                x.StudentName + (x.GrNumber == "" ? "" : " (" + x.GrNumber + ")")),
            ["classDivision"] = (Expression<Func<WorkspaceRecord, string>>)(x => x.ClassDivision),
            ["counterparty"] = (Expression<Func<WorkspaceRecord, string>>)(x => x.Counterparty),
            ["source"] = (Expression<Func<WorkspaceRecord, string>>)(x => x.Source),
            ["categoryName"] = (Expression<Func<WorkspaceRecord, string>>)(x => x.CategoryName),
            ["bankLabel"] = (Expression<Func<WorkspaceRecord, string>>)(x => x.BankLabel),
            ["amount"] = (Expression<Func<WorkspaceRecord, decimal>>)(x => x.Amount),
            ["occurredAt"] = (Expression<Func<WorkspaceRecord, DateTime>>)(x => x.Date),
            ["sourceStatus"] = (Expression<Func<WorkspaceRecord, string>>)(x => x.Status)
        };

    private static FinanceTableQuery WorkspaceFilters(IEnumerable<FinanceTableFilter> filters) =>
        FinanceTableQuery.Parse(JsonSerializer.Serialize(filters), null, null, null, AccountsRegisterTable.Columns);

    private sealed class WorkspaceStudent
    {
        public Guid Id { get; set; }
        public string GrNumber { get; set; } = "";
        public string ClassDivision { get; set; } = "";
    }

    private async Task<IQueryable<WorkspaceStudent>> WorkspaceStudentsAsync(Guid academicYearId)
    {
        var studentQuery = await studentRepository.GetQueryableAsync();
        var enrollmentQuery = await enrollments.GetQueryableAsync();
        var standardQuery = await standards.GetQueryableAsync();
        var divisionQuery = await divisions.GetQueryableAsync();
        return from student in studentQuery
               let standardId = enrollmentQuery.Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
                   .OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CreationTime).ThenByDescending(x => x.Id)
                   .Select(x => (Guid?)x.StandardId).FirstOrDefault() ??
                   (student.AcademicYearId == academicYearId ? student.StandardId : null)
               let divisionId = enrollmentQuery.Where(x => x.StudentId == student.Id && x.AcademicYearId == academicYearId)
                   .OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CreationTime).ThenByDescending(x => x.Id)
                   .Select(x => x.DivisionId).FirstOrDefault() ??
                   (student.AcademicYearId == academicYearId ? student.DivisionId : null)
               join standard in standardQuery on standardId equals (Guid?)standard.Id into standardGroup
               from standard in standardGroup.DefaultIfEmpty()
               join division in divisionQuery on divisionId equals (Guid?)division.Id into divisionGroup
               from division in divisionGroup.DefaultIfEmpty()
               select new WorkspaceStudent
               {
                   Id = student.Id,
                   GrNumber = student.GrNumber ?? (student.GrNo.HasValue ? student.GrNo.Value.ToString() : ""),
                   ClassDivision = standard == null && division == null ? "" :
                       (standard == null ? "—" : standard.Name) + " - " + (division == null ? "—" : division.Name)
               };
    }

    private static IQueryable<WorkspaceRecord> WithWorkspaceStudents(
        IQueryable<WorkspaceRecord> query, IQueryable<WorkspaceStudent> profiles) =>
        from row in query
        join profile in profiles on row.StudentId equals (Guid?)profile.Id into profileGroup
        from profile in profileGroup.DefaultIfEmpty()
        select new WorkspaceRecord
        {
            Id = row.Id, SourceId = row.SourceId, StudentId = row.StudentId, FinanceFeeAccountId = row.FinanceFeeAccountId,
            StudentName = row.StudentName, CategoryId = row.CategoryId, CategoryName = row.CategoryName,
            BankAccountId = row.BankAccountId, BankLabel = row.BankLabel, Reference = row.Reference,
            Source = row.Source, Status = row.Status, Amount = row.Amount, Date = row.Date,
            // Coalesce columns from the outer join; DTO-to-null comparisons cannot translate to SQL.
            GrNumber = profile.GrNumber ?? "",
            ClassDivision = profile.ClassDivision ?? ""
        };
}
