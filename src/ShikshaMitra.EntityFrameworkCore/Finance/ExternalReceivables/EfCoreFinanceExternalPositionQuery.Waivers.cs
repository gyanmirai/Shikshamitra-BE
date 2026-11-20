using System;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class EfCoreFinanceExternalPositionQuery
{
	private static IQueryable<WaiverTotal> WaiverTotals(ShikshaMitraDbContext db, Guid tenantId, IQueryable<Guid> relevant)
	{
		var waivers = db.Set<ChargeWaiverApplication>().Where(row => row.TenantId == tenantId && relevant.Contains(row.ChargeId));
		var waiverIds = waivers.Select(row => row.Id);
		var reversals = db.Set<FinanceChargeWaiverReversal>().Where(row => row.TenantId == tenantId && waiverIds.Contains(row.WaiverApplicationId))
			.GroupBy(row => row.WaiverApplicationId).Select(group => new ReversedTotal { Id = group.Key, Amount = group.Sum(row => row.Amount) });
		var rows = from waiver in waivers
			join charge in db.Set<Charge>().Where(row => row.TenantId == tenantId) on waiver.ChargeId equals charge.Id
			join reversal in reversals on waiver.Id equals reversal.Id into reversalJoin
			from reversal in reversalJoin.DefaultIfEmpty()
			select new WaiverRow
			{
				ChargeId = charge.Id, Input = new()
				{
					ChargeYear = charge.AcademicYearId, ChargeStudent = charge.StudentId, ChargeEmployee = charge.EmployeeId,
					Year = waiver.AcademicYearId, Student = waiver.StudentId, Employee = waiver.EmployeeId,
					Effective = waiver.EffectiveAmount, Reversed = (decimal?)reversal.Amount ?? 0m
				}
			};
		Expression<Func<WaiverRow, WaiverTotal>> values = value => new()
		{
			ChargeId = value.ChargeId, Applied = value.Input.Effective, Reversed = value.Input.Reversed,
			Invalid = FinanceExternalWaiverExpressions.Matches(value.Input) ? 0 : 1
		};
		return rows.Select(FinanceExternalPositionExpressionExpansion.Expand(values)).GroupBy(row => row.ChargeId)
			.Select(group => new WaiverTotal { ChargeId = group.Key, Applied = group.Sum(row => row.Applied),
				Reversed = group.Sum(row => row.Reversed), Invalid = group.Sum(row => row.Invalid) });
	}

	private sealed class ReversedTotal { public Guid Id { get; init; } public decimal Amount { get; init; } }
	private sealed class WaiverRow { public Guid ChargeId { get; init; } public FinanceExternalWaiverBalanceInput Input { get; init; } = null!; }
}
