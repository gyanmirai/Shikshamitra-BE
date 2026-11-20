using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalChargeBalance(FinanceExternalBalanceAmounts Amounts,
	IReadOnlyList<ChargeCollectionRecord> Allocations, IReadOnlyList<FeeRecordMetadata> Collections,
	IReadOnlyList<ChargeWaiverApplication> Waivers, IReadOnlyList<FinanceChargeWaiverReversal> Reversals);

public class FinanceExternalChargeBalanceReader(
	IRepository<ChargeCollectionRecord, Guid> allocations,
	IRepository<FeeRecordMetadata, Guid> collections,
	IRepository<ChargeWaiverApplication, Guid> waivers,
	IRepository<FinanceChargeWaiverReversal, Guid> reversals,
	ICurrentTenant tenant) : DomainService
{
	public async Task<FinanceExternalChargeBalance> ReadAsync(Charge charge)
	{
		if (!tenant.Id.HasValue || charge.TenantId != tenant.Id)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		var rows = await allocations.GetListAsync(row => row.TenantId == tenant.Id && row.ChargeId == charge.Id);
		var ids = rows.Select(row => row.FeeRecordMetadataId).Distinct().ToArray();
		var headers = await collections.GetListAsync(row => row.TenantId == tenant.Id && Enumerable.Contains(ids, row.Id));
		var applied = await waivers.GetListAsync(row => row.TenantId == tenant.Id && row.ChargeId == charge.Id);
		var waiverIds = applied.Select(row => row.Id).ToArray();
		var reversed = await reversals.GetListAsync(row => row.TenantId == tenant.Id && Enumerable.Contains(waiverIds, row.WaiverApplicationId));
		return Calculate(charge, rows, headers, applied, reversed);
	}

	internal static FinanceExternalChargeBalance Calculate(Charge charge, IReadOnlyList<ChargeCollectionRecord> rows,
		IReadOnlyList<FeeRecordMetadata> headers, IReadOnlyList<ChargeWaiverApplication> applied,
		IReadOnlyList<FinanceChargeWaiverReversal> reversed)
	{
		var byId = headers.ToDictionary(row => row.Id);
		decimal accepted = 0m, settled = 0m, verification = 0m;
		foreach (var row in rows)
		{
			byId.TryGetValue(row.FeeRecordMetadataId, out var header);
			var input = new FinanceExternalAllocationInput
			{
				ChargeTenant = charge.TenantId, ChargeYear = charge.AcademicYearId, ChargeStudent = charge.StudentId, ChargeEmployee = charge.EmployeeId,
				RowTenant = row.TenantId, RowStudent = row.StudentId, RowEmployee = row.EmployeeId, Amount = row.Amount,
				HeaderId = header?.Id, HeaderTenant = header?.TenantId, HeaderYear = header?.AcademicYearId,
				HeaderStudent = header?.StudentId, HeaderEmployee = header?.EmployeeId, HeaderAccount = header?.FinanceFeeAccountId,
				HeaderCurrency = header?.Currency, Status = header?.Status
			};
			if (!FinanceExternalAllocationExpressions.MatchesIdentity(input)) throw new BusinessException(LibraryFinanceErrorCodes.InvalidBalance);
			FinanceExternalSettlementAmounts.EnsureMoney(row.Amount, positive: true);
			if (!FinanceExternalAllocationExpressions.HasKnownStatus(input)) throw new BusinessException(LibraryFinanceErrorCodes.InvalidBalance);
			accepted += FinanceExternalAllocationExpressions.AcceptedAmount(input);
			settled += FinanceExternalAllocationExpressions.SettledAmount(input);
			verification += FinanceExternalAllocationExpressions.PendingAmount(input);
		}
		foreach (var waiver in applied)
		{
			if (!FinanceExternalWaiverExpressions.Matches(new()
			{
				ChargeYear = charge.AcademicYearId, ChargeStudent = charge.StudentId, ChargeEmployee = charge.EmployeeId,
				Year = waiver.AcademicYearId, Student = waiver.StudentId, Employee = waiver.EmployeeId,
				Effective = waiver.EffectiveAmount, Reversed = reversed.Where(row => row.WaiverApplicationId == waiver.Id).Sum(row => row.Amount)
			}))
				throw new BusinessException(LibraryFinanceErrorCodes.InvalidBalance);
		}
		var amounts = FinanceExternalSettlementAmounts.Balance(charge.Amount, accepted, settled, verification,
			applied.Sum(row => row.EffectiveAmount), reversed.Sum(row => row.Amount), charge.Status);
		return new(amounts, rows, headers, applied, reversed);
	}
}
