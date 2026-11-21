using System;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class EfCoreFinanceExternalPositionQuery
{
	private static IQueryable<PaymentTotal> PaymentTotals(ShikshaMitraDbContext db, Guid tenantId, IQueryable<Guid> relevant)
	{
		var rows = from allocation in db.Set<ChargeCollectionRecord>().Where(row => row.TenantId == tenantId && relevant.Contains(row.ChargeId))
			join charge in db.Set<Charge>().Where(row => row.TenantId == tenantId) on allocation.ChargeId equals charge.Id
			join header in db.Set<FeeRecordMetadata>().Where(row => row.TenantId == tenantId)
				on allocation.FeeRecordMetadataId equals header.Id into headerJoin
			from header in headerJoin.DefaultIfEmpty()
			select new AllocationRow
			{
				ChargeId = charge.Id,
				Input = new()
				{
					ChargeTenant = charge.TenantId, ChargeYear = charge.AcademicYearId, ChargeStudent = charge.StudentId, ChargeEmployee = charge.EmployeeId,
					RowTenant = allocation.TenantId, RowStudent = allocation.StudentId, RowEmployee = allocation.EmployeeId, Amount = allocation.Amount,
					HeaderId = (Guid?)header.Id, HeaderTenant = header.TenantId, HeaderYear = (Guid?)header.AcademicYearId,
					HeaderStudent = header.StudentId, HeaderEmployee = header.EmployeeId, HeaderAccount = header.FinanceFeeAccountId,
					HeaderCurrency = header.Currency, Status = header.Status
				}
			};
		Expression<Func<AllocationRow, PaymentTotal>> values = value => new()
		{
			ChargeId = value.ChargeId,
			Accepted = FinanceExternalAllocationExpressions.AcceptedAmount(value.Input),
			Settled = FinanceExternalAllocationExpressions.SettledAmount(value.Input),
			Pending = FinanceExternalAllocationExpressions.PendingAmount(value.Input),
			// Persisted allocation amounts have the strict decimal18,2 mapping.
			Invalid = FinanceExternalAllocationExpressions.MatchesIdentity(value.Input) &&
				FinanceExternalAllocationExpressions.HasKnownStatus(value.Input) && value.Input.Amount > 0m &&
				value.Input.Amount <= FinanceChargeAmountRules.MaximumAmount ? 0 : 1
		};
		return rows.Select(FinanceExternalPositionExpressionExpansion.Expand(values)).GroupBy(row => row.ChargeId)
			.Select(group => new PaymentTotal { ChargeId = group.Key, Accepted = group.Sum(row => row.Accepted),
				Settled = group.Sum(row => row.Settled), Pending = group.Sum(row => row.Pending), Invalid = group.Sum(row => row.Invalid) });
	}

	private sealed class AllocationRow { public Guid ChargeId { get; init; } public FinanceExternalAllocationInput Input { get; init; } = null!; }
}
