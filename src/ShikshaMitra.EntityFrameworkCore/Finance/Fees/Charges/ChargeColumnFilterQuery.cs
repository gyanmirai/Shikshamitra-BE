using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Charges;

internal static class ChargeColumnFilterQuery
{
    private sealed class Row
    {
        public ChargeRequest Request { get; set; } = null!;
        public string CategoryName { get; set; } = "";
        public int StudentCount { get; set; }
    }

    private static readonly IReadOnlyDictionary<string, LambdaExpression> Fields = new Dictionary<string, LambdaExpression>
    {
        ["chargeName"] = (Expression<Func<Row, string>>)(r => r.Request.ChargeName),
        ["categoryName"] = (Expression<Func<Row, string>>)(r => r.CategoryName),
        ["description"] = (Expression<Func<Row, string>>)(r => r.Request.PurposeDescription),
        ["amount"] = (Expression<Func<Row, decimal>>)(r => r.Request.Amount),
        ["studentCount"] = (Expression<Func<Row, int>>)(r => r.StudentCount),
        ["dueDate"] = (Expression<Func<Row, DateTimeOffset>>)(r => r.Request.DueDate)
    };

    public static IQueryable<ChargeRequest> Apply(IQueryable<ChargeRequest> requests, IQueryable<Charge> visibleLines,
        IQueryable<ChargeCategory> categories, IReadOnlyCollection<ChargeColumnFilter> filters)
    {
        if (!filters.Any(f => f.Column != "workflowStatus")) return requests;
        var rows = requests.Select(request => new Row
        {
            Request = request,
            CategoryName = categories.Where(c => c.Id == request.ChargeCategoryId).Select(c => c.Name).FirstOrDefault() ?? "",
            StudentCount = visibleLines.Count(c => c.ChargeRequestId == request.Id)
        });
        return ChargeColumnFilter.Apply(rows, filters, Fields).Select(row => row.Request);
    }
}
