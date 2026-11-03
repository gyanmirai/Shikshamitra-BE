using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;

internal static class ConcessionApplicationsTableQuery
{
    private sealed class Row
    {
        public ConcessionRequest Request { get; init; } = null!;
        public string StudentIdentity { get; init; } = "";
        public string RollNo { get; init; } = "";
        public string ClassDivision { get; init; } = "";
        public string TypeName { get; init; } = "";
        public decimal Amount { get; init; }
    }

    private static readonly IReadOnlyDictionary<string, LambdaExpression> Fields = new Dictionary<string, LambdaExpression>
    {
        ["studentIdentity"] = (Expression<Func<Row, string>>)(r => r.StudentIdentity),
        ["rollNo"] = (Expression<Func<Row, string>>)(r => r.RollNo),
        ["classDivision"] = (Expression<Func<Row, string>>)(r => r.ClassDivision),
        ["concessionTypeName"] = (Expression<Func<Row, string>>)(r => r.TypeName),
        ["concessionAmount"] = (Expression<Func<Row, decimal>>)(r => r.Amount),
        ["createdAt"] = (Expression<Func<Row, DateTime>>)(r => r.Request.CreationTime),
        ["status"] = (Expression<Func<Row, int>>)(r => (int)r.Request.Status)
    };

    private static IQueryable<Row> Rows(IQueryable<ConcessionRequest> query, ShikshaMitraDbContext db) =>
        from request in query
        join studentValue in db.Set<Student>() on request.StudentId equals studentValue.Id into students
        from student in students.DefaultIfEmpty()
        join typeValue in db.Set<ConcessionType>() on request.ConcessionTypeId equals typeValue.Id into types
        from type in types.DefaultIfEmpty()
        join standardValue in db.Set<Standard>() on request.StandardId equals standardValue.Id into standards
        from standard in standards.DefaultIfEmpty()
        join divisionValue in db.Set<Division>() on request.DivisionId equals divisionValue.Id into divisions
        from division in divisions.DefaultIfEmpty()
        let first = student == null ? "" : (student.FirstName ?? "")
        let middle = student == null ? "" : (student.MiddleName ?? "")
        let last = student == null ? "" : (student.LastName ?? "")
        let fullName = (first.Trim() == "" ? "" : first) +
            (middle.Trim() == "" ? "" : (first.Trim() == "" ? "" : " ") + middle) +
            (last.Trim() == "" ? "" : (first.Trim() == "" && middle.Trim() == "" ? "" : " ") + last)
        select new Row
        {
            Request = request,
            StudentIdentity = (fullName == "" ? "-" : fullName) +
                (student != null && student.GrNo.HasValue ? " (" + student.GrNo!.Value.ToString() + ")" : ""),
            RollNo = student != null && student.RollNo.HasValue ? student.RollNo!.Value.ToString() : "",
            ClassDivision = (standard == null ? "-" : standard.Name) + " (" + (division == null ? "-" : division.Name) + ")",
            TypeName = type == null ? "" : type.Name,
            Amount = request.ConcessionFeeHeadMappings.Sum(m => m.FeeHeadConcessionAmount)
        };

    public static IQueryable<ConcessionRequest> Filter(IQueryable<ConcessionRequest> query, ShikshaMitraDbContext db, FinanceTableQuery options)
    {
        var rows = Rows(query, db);
        if (!string.IsNullOrEmpty(options.Search))
        {
            var search = options.Search.ToLowerInvariant();
            rows = rows.Where(r => r.StudentIdentity.ToLower().Contains(search) || r.RollNo.ToLower().Contains(search) ||
                r.ClassDivision.ToLower().Contains(search) || r.TypeName.ToLower().Contains(search));
        }
        return options.Apply(rows, Fields).Select(r => r.Request);
    }

    public static IQueryable<ConcessionRequest> Order(IQueryable<ConcessionRequest> query, ShikshaMitraDbContext db, FinanceTableQuery options) =>
        options.Order(Rows(query, db), Fields, "createdAt", true, r => r.Request.Id).Select(r => r.Request);
}
