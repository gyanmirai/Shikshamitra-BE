using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<ReservationPageRow, string>>> ReservationColumns =
        new Dictionary<string, Expression<Func<ReservationPageRow, string>>>(StringComparer.Ordinal)
        {
            ["roomName"] = row => row.RoomName + (row.BuildingName == "" ? "" : " " + row.BuildingName),
            ["purpose"] = row => row.Reservation.Purpose,
            ["requestedBy"] = row => row.RequestedBy == "" ? "—" : row.RequestedBy,
            ["startTime"] = row => row.Reservation.StartTime.ToString().Substring(0, 5) + " – " + row.Reservation.EndTime.ToString().Substring(0, 5),
            ["date"] = row => row.Reservation.Date.ToString().Substring(8, 2) + "-" + row.Reservation.Date.ToString().Substring(5, 2) + "-" + row.Reservation.Date.ToString().Substring(0, 4) +
                (row.Reservation.IsRecurring && row.UntilDate.HasValue ? " to " + row.UntilDate.Value.ToString().Substring(8, 2) + "-" +
                    row.UntilDate.Value.ToString().Substring(5, 2) + "-" + row.UntilDate.Value.ToString().Substring(0, 4) : "") +
                (row.Reservation.IsRecurring ? " (Recurring)" : ""),
            ["conflictStatus"] = row => "None",
            ["createdAt"] = row => row.Reservation.CreationTime.ToString().Substring(8, 2) + "-" +
                row.Reservation.CreationTime.ToString().Substring(5, 2) + "-" + row.Reservation.CreationTime.ToString().Substring(0, 4) + ", " +
                ((row.Reservation.CreationTime.Hour % 12 == 0 ? 12 : row.Reservation.CreationTime.Hour % 12) + 100).ToString().Substring(1, 2) + ":" +
                row.Reservation.CreationTime.ToString().Substring(14, 2) + (row.Reservation.CreationTime.Hour < 12 ? " AM" : " PM"),
            ["reservationType"] = row => row.Reservation.ReservationType == RoomReservationType.Class ? "class" :
                row.Reservation.ReservationType == RoomReservationType.Practical ? "practical" :
                row.Reservation.ReservationType == RoomReservationType.Exam ? "exam" :
                row.Reservation.ReservationType == RoomReservationType.Meeting ? "meeting" :
                row.Reservation.ReservationType == RoomReservationType.Event ? "event" :
                row.Reservation.ReservationType == RoomReservationType.Maintenance ? "maintenance" : "other",
            ["approvalStatus"] = row => row.Reservation.Status == RoomReservationStatus.Approved || row.Reservation.Status == RoomReservationStatus.Active ? "approved" :
                row.Reservation.Status == RoomReservationStatus.Rejected ? "rejected" :
                row.Reservation.Status == RoomReservationStatus.PendingApproval ? "pending" :
                row.Reservation.Status == RoomReservationStatus.Cancelled ? "cancelled" : "none"
        };

    private static IQueryable<ReservationPageRow> ApplyReservationTableFilters(IQueryable<ReservationPageRow> query, GetRoomReservationsPageInputDto input)
    {
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var columns = input.SearchColumns == null ? ReservationColumns.Keys : input.SearchColumns.SelectMany(value => value.Split(','));
            var searchPredicates = columns.Distinct().Where(ReservationColumns.ContainsKey)
                .SelectMany(column => ReservationSearchSelectors(column, input.IsApprovalView))
                .Select(selector => StringFilter(selector, "contains", input.SearchTerm)).ToArray();
            query = query.Where(Combine(searchPredicates, false));
        }
        if (string.IsNullOrWhiteSpace(input.Filters)) return query;
        List<ReservationColumnFilter> filters;
        try
        {
            filters = JsonSerializer.Deserialize<List<ReservationColumnFilter>>(input.Filters,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (JsonException)
        {
            throw new BusinessException("RoomReservations:InvalidFilters");
        }
        foreach (var group in filters.Where(filter => ReservationColumns.ContainsKey(filter.Column)).GroupBy(filter => filter.Column))
        {
            var alternatives = new List<Expression<Func<ReservationPageRow, bool>>>();
            foreach (var filter in group)
            {
                var value = filter.Value.ValueKind == JsonValueKind.String ? filter.Value.GetString() ?? "" : filter.Value.ToString();
                var operation = filter.Type.ToLowerInvariant();
                if (filter.ValueType == "enum")
                {
                    if (filter.Column == "conflictStatus") alternatives.Add(row => false);
                    else
                    {
                        var selector = ReservationColumns[filter.Column];
                        alternatives.Add(Expression.Lambda<Func<ReservationPageRow, bool>>(
                            Expression.Equal(selector.Body, Expression.Constant(value)), selector.Parameters));
                    }
                }
                else if ((filter.Column == "date" || filter.Column == "createdAt") &&
                    (filter.ValueType == "date" || filter.ValueType == "dateTime") &&
                    DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    var minute = filter.ValueType == "dateTime";
                    date = minute ? new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0, date.Kind) : date.Date;
                    var next = minute ? date.AddMinutes(1) : date.AddDays(1);
                    Expression<Func<ReservationPageRow, DateTime>> selector = filter.Column == "createdAt"
                        ? row => row.Reservation.CreationTime : row => row.Reservation.Date;
                    Expression comparison = operation switch
                    {
                        "after" => Expression.GreaterThanOrEqual(selector.Body, Expression.Constant(date)),
                        "before" => Expression.LessThan(selector.Body, Expression.Constant(next)),
                        "equals" => Expression.AndAlso(Expression.GreaterThanOrEqual(selector.Body, Expression.Constant(date)), Expression.LessThan(selector.Body, Expression.Constant(next))),
                        _ => Expression.Constant(false)
                    };
                    alternatives.Add(Expression.Lambda<Func<ReservationPageRow, bool>>(comparison, selector.Parameters));
                }
                else
                {
                    var selectors = filter.Column == "createdAt" ? ReservationSearchSelectors(filter.Column, input.IsApprovalView).Skip(2)
                        .Append(ReservationDisplaySelector(filter.Column, input.IsApprovalView)) : new[] { ReservationDisplaySelector(filter.Column, input.IsApprovalView) };
                    alternatives.Add(Combine(selectors.Select(selector => StringFilter(selector, operation, value)), false));
                }
            }
            query = query.Where(Combine(alternatives, false));
        }
        return query;
    }

    private static Expression<Func<ReservationPageRow, string>> ReservationDisplaySelector(string column, bool approvalView)
    {
        if (column != "date" || !approvalView) return ReservationColumns[column];
        Expression<Func<ReservationPageRow, string>> time = row => row.Reservation.StartTime.ToString().Substring(0, 5) + " – " +
            row.Reservation.EndTime.ToString().Substring(0, 5) + (row.Reservation.IsRecurring ? " (Recurring)" : "");
        // Approval cells put the recurring marker after the time, unlike reservation cells.
        Expression<Func<ReservationPageRow, string>> plainDate = row => row.Reservation.Date.ToString().Substring(8, 2) + "-" +
            row.Reservation.Date.ToString().Substring(5, 2) + "-" + row.Reservation.Date.ToString().Substring(0, 4) +
            (row.Reservation.IsRecurring && row.UntilDate.HasValue ? " to " + row.UntilDate.Value.ToString().Substring(8, 2) + "-" +
                row.UntilDate.Value.ToString().Substring(5, 2) + "-" + row.UntilDate.Value.ToString().Substring(0, 4) : "");
        var timeBody = new ReservationParameterVisitor(time.Parameters[0], plainDate.Parameters[0]).Visit(time.Body)!;
        return Expression.Lambda<Func<ReservationPageRow, string>>(Expression.Call(typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string), typeof(string) })!,
            plainDate.Body, Expression.Constant(" "), timeBody), plainDate.Parameters);
    }

    private static IEnumerable<Expression<Func<ReservationPageRow, string>>> ReservationSearchSelectors(string column, bool approvalView)
    {
        yield return ReservationDisplaySelector(column, approvalView);
        if (column == "date") yield return row => row.Reservation.Date.ToString().Substring(0, 10);
        if (column == "roomName") yield return row => row.RoomName;
        if (column == "startTime") yield return row => row.Reservation.StartTime.ToString().Substring(0, 5);
        if (column == "requestedBy") yield return row => row.RequestedBy;
        if (column == "createdAt")
        {
            yield return row => row.Reservation.CreationTime.ToString().Replace(" ", "T");
            yield return row => row.Reservation.CreationTime.ToString().Substring(8, 2) + "-" +
                row.Reservation.CreationTime.ToString().Substring(5, 2) + "-" + row.Reservation.CreationTime.ToString().Substring(0, 4);
            yield return row => ((row.Reservation.CreationTime.Hour % 12 == 0 ? 12 : row.Reservation.CreationTime.Hour % 12) + 100).ToString().Substring(1, 2) + ":" +
                row.Reservation.CreationTime.ToString().Substring(14, 2) + (row.Reservation.CreationTime.Hour < 12 ? " AM" : " PM");
            yield return row => (row.Reservation.CreationTime.Hour % 12 == 0 ? 12 : row.Reservation.CreationTime.Hour % 12).ToString() + ":" +
                row.Reservation.CreationTime.ToString().Substring(14, 2) + (row.Reservation.CreationTime.Hour < 12 ? " AM" : " PM");
        }
    }

    private static Expression<Func<ReservationPageRow, bool>> StringFilter(Expression<Func<ReservationPageRow, string>> selector, string operation, string value)
    {
        var text = Expression.Call(selector.Body, nameof(string.ToLower), Type.EmptyTypes);
        var constant = Expression.Constant(value.ToLowerInvariant());
        Expression comparison = operation switch
        {
            "equals" or "is" => Expression.Equal(text, constant),
            "notequals" or "isnot" => Expression.NotEqual(text, constant),
            "startswith" => Expression.Call(text, nameof(string.StartsWith), Type.EmptyTypes, constant),
            "endswith" => Expression.Call(text, nameof(string.EndsWith), Type.EmptyTypes, constant),
            "notcontains" => Expression.Not(Expression.Call(text, nameof(string.Contains), Type.EmptyTypes, constant)),
            _ => Expression.Call(text, nameof(string.Contains), Type.EmptyTypes, constant)
        };
        return Expression.Lambda<Func<ReservationPageRow, bool>>(comparison, selector.Parameters);
    }

    private static Expression<Func<ReservationPageRow, bool>> Combine(IEnumerable<Expression<Func<ReservationPageRow, bool>>> predicates, bool and)
    {
        var parameter = Expression.Parameter(typeof(ReservationPageRow), "row");
        Expression body = Expression.Constant(and);
        foreach (var predicate in predicates)
        {
            var next = new ReservationParameterVisitor(predicate.Parameters[0], parameter).Visit(predicate.Body)!;
            body = and ? Expression.AndAlso(body, next) : Expression.OrElse(body, next);
        }
        return Expression.Lambda<Func<ReservationPageRow, bool>>(body, parameter);
    }

    private static IOrderedQueryable<ReservationPageRow> OrderReservations(IQueryable<ReservationPageRow> query, string? sorting)
    {
        IOrderedQueryable<ReservationPageRow>? ordered = null;
        foreach (var item in (sorting ?? "creationTime asc").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
            LambdaExpression? selector = parts[0] switch
            {
                "id" => (Expression<Func<ReservationPageRow, Guid>>)(row => row.Reservation.Id),
                "creationTime" or "createdAt" => (Expression<Func<ReservationPageRow, DateTime>>)(row => row.Reservation.CreationTime),
                "date" => (Expression<Func<ReservationPageRow, DateTime>>)(row => row.Reservation.Date),
                "startTime" => (Expression<Func<ReservationPageRow, string>>)(row => row.Reservation.StartTime.ToString()),
                "roomName" => (Expression<Func<ReservationPageRow, string>>)(row => row.RoomName),
                "conflictStatus" => null,
                _ => ReservationColumns.GetValueOrDefault(parts[0])
            };
            if (selector == null) continue;
            var method = ordered == null ? (descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy)) :
                (descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));
            ordered = (IOrderedQueryable<ReservationPageRow>)query.Provider.CreateQuery<ReservationPageRow>(
                Expression.Call(typeof(Queryable), method, new[] { typeof(ReservationPageRow), selector.ReturnType },
                    (ordered ?? query).Expression, Expression.Quote(selector)));
        }
        return (ordered ?? query.OrderBy(row => row.Reservation.CreationTime)).ThenBy(row => row.Reservation.Id);
    }

    private sealed class ReservationParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : base.VisitParameter(node);
    }

    private sealed class ReservationColumnFilter
    {
        public string Column { get; set; } = "";
        public string Type { get; set; } = "contains";
        public JsonElement Value { get; set; }
        public string? ValueType { get; set; }
    }
}
