using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.Dashboard;

public static class FinanceDashboardBalanceProjection
{
    public record PayerBalance(Guid? ClassId, Guid? StudentId, decimal Outstanding, decimal Overdue);
    public record ClassCharges(Guid? ClassId, decimal Outstanding);
    public record ClassContext(Guid Id, string Name, Guid BoardId, Guid MediumId, string BoardMedium);

    public static FinanceDashboardBalancesDto Build(IReadOnlyList<PayerBalance> dues,
        IReadOnlyList<ClassCharges>? charges, IReadOnlyList<ClassContext> classes)
    {
        var contexts = classes.ToDictionary(row => row.Id);
        Guid? VisibleClass(Guid? id) => id.HasValue && contexts.ContainsKey(id.Value) ? id : null;
        var duesByClass = dues.ToLookup(row => VisibleClass(row.ClassId));
        var chargesByClass = charges?.ToLookup(row => VisibleClass(row.ClassId));
        var classIds = duesByClass.Select(group => group.Key)
            .Union(chargesByClass?.Select(group => group.Key) ?? []);
        var rows = classIds.Select(id =>
        {
            var context = id.HasValue ? contexts.GetValueOrDefault(id.Value) : null;
            var balances = duesByClass[id];
            return new FinanceDashboardClassDto(id, context?.Name ?? "Unassigned class", context?.BoardId,
                context?.MediumId, context?.BoardMedium, balances.Sum(row => row.Outstanding),
                balances.Sum(row => row.Overdue), CountOverdueStudents(balances),
                chargesByClass == null ? null : chargesByClass[id].Sum(row => row.Outstanding));
        }).OrderByDescending(row => row.Overdue).ThenBy(row => row.ClassName).ThenBy(row => row.ClassId).ToList();

        return new(dues.Sum(row => row.Outstanding), dues.Sum(row => row.Overdue), CountOverdueStudents(dues), rows);
    }

    private static int CountOverdueStudents(IEnumerable<PayerBalance> rows) =>
        rows.Where(row => row.Overdue > 0 && row.StudentId.HasValue)
            .Select(row => row.StudentId).Distinct().Count();
}
