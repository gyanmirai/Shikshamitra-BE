using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.Students;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

internal static class FeeRefundColumnFilterQuery
{
    private sealed class Row
    {
        public FeeRefundRequest Request { get; set; } = null!;
        public string Name { get; set; } = "";
        public string ClassDivision { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime RequestedAt { get; set; }
    }

    public static IQueryable<FeeRefundRequest> Apply(IQueryable<FeeRefundRequest> requests,
        IQueryable<Student> students, IReadOnlyCollection<FeeRefundColumnFilter> filters)
    {
        if (filters.Count == 0) return requests;
        var rows = from request in requests
                   join student in students on request.StudentId equals student.Id into matches
                   from student in matches.DefaultIfEmpty()
                   select new Row
                   {
                       Request = request,
                       Name = student == null ? request.StudentId.ToString() :
                           (student.FirstName + " " +
                           (student.MiddleName == null || student.MiddleName == "" ? "" : student.MiddleName + " ") +
                           student.LastName).Trim() + (student.GrNo == null ? "" : " (" + student.GrNo.ToString() + ")"),
                       ClassDivision = (student == null || student.Standard == null ? "—" : student.Standard.Name) +
                           " / " + (student == null || student.Division == null ? "—" : student.Division.Name),
                       Amount = !request.RefundPolicyVersionId.HasValue && !request.ConcessionRequestId.HasValue
                           ? request.TotalAmount : request.ApprovedAmount != 0 ? request.ApprovedAmount : request.CalculatedAmount,
                       RequestedAt = request.SubmittedAt.HasValue ? request.SubmittedAt.Value.UtcDateTime : request.CreationTime
                   };
        foreach (var group in filters.GroupBy(f => f.Column))
        {
            var row = Expression.Parameter(typeof(Row), "row");
            Expression? alternatives = null;
            foreach (var filter in group)
            {
                var predicate = Build(filter);
                var body = new ReplaceParameter(predicate.Parameters[0], row).Visit(predicate.Body)!;
                alternatives = alternatives == null ? body : Expression.OrElse(alternatives, body);
            }
            rows = rows.Where(Expression.Lambda<Func<Row, bool>>(alternatives!, row));
        }
        return rows.Select(row => row.Request);
    }

    private static Expression<Func<Row, bool>> Build(FeeRefundColumnFilter filter)
    {
        if (filter.Column == "status")
        {
            var status = (FeeRefundStatus)int.Parse(filter.Value.ToString(), CultureInfo.InvariantCulture);
            return row => row.Request.Status == status;
        }
        if (filter.Column == "refundAmount")
        {
            var amount = decimal.Parse(filter.Value.ToString(), CultureInfo.InvariantCulture);
            return filter.Type switch
            {
                "greaterThan" => row => row.Amount > amount,
                "lessThan" => row => row.Amount < amount,
                _ => row => row.Amount == amount
            };
        }
        if (filter.Column == "requestDate")
        {
            var start = DateTimeOffset.Parse(filter.Value.ToString(), CultureInfo.InvariantCulture);
            start = new DateTimeOffset(start.Date, start.Offset);
            var from = start.UtcDateTime;
            var end = start.AddDays(1).UtcDateTime;
            return filter.Type switch
            {
                "after" => row => row.RequestedAt >= from,
                "before" => row => row.RequestedAt < end,
                _ => row => row.RequestedAt >= from && row.RequestedAt < end
            };
        }
        Expression<Func<Row, string>> field = filter.Column switch
        {
            "_voucherNumber" => row => row.Request.VoucherNumber,
            "name" => row => row.Name,
            "classDivision" => row => row.ClassDivision,
            "_receiptNumber" => row => row.Request.OriginalReceiptNumber ?? "",
            "refundMethod" => row => row.Request.RefundMethod ?? "",
            _ => throw new ArgumentException("Unsupported refund filter column.")
        };
        var value = filter.Value.GetString()!.ToLowerInvariant();
        var lowered = Expression.Call(field.Body, nameof(string.ToLower), Type.EmptyTypes);
        var body = filter.Type == "equals"
            ? (Expression)Expression.Equal(lowered, Expression.Constant(value))
            : Expression.Call(lowered, filter.Type switch
            {
                "startsWith" => nameof(string.StartsWith),
                "endsWith" => nameof(string.EndsWith),
                _ => nameof(string.Contains)
            }, Type.EmptyTypes, Expression.Constant(value));
        return Expression.Lambda<Func<Row, bool>>(body, field.Parameters);
    }

    private sealed class ReplaceParameter(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : node;
    }
}
