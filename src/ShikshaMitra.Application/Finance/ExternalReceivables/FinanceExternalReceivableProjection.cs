using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Library;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

public class FinanceExternalReceivableProjection(
	IRepository<Charge, Guid> charges,
	FinanceExternalChargeBalanceReader balances,
	IRepository<FeeRefundRequest, Guid> refunds,
	ICurrentTenant tenant,
	IAsyncQueryableExecuter queries,
	IReadOnlyRepository<AcademicYear, Guid> academicYears) : ITransientDependency
{
	public async Task<FinanceExternalReceivableDto> ReadAsync(FinanceExternalReceivableSourceLink link,
		FinanceExternalReceivableSourceFacts source)
	{
		var charge = await charges.GetAsync(link.FinanceChargeId);
		if (link.TenantId != tenant.Id || charge.TenantId != tenant.Id || charge.AcademicYearId != link.AcademicYearId ||
			charge.ChargeRequestId != link.FinanceChargeRequestId || charge.StudentId != source.Payer.StudentId ||
			charge.EmployeeId != source.Payer.EmployeeId || !charge.SchoolAssessmentDate.HasValue || !charge.SchoolDueDate.HasValue)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		var balance = (await balances.ReadAsync(charge)).Amounts;
		var refundQuery = await refunds.GetQueryableAsync();
		var refunded = await queries.SumAsync(refundQuery.Where(row => row.TenantId == tenant.Id)
			.SelectMany(row => row.Lines).Where(row => row.ChargeId == charge.Id)
			.Select(row => row.ProcessedAmount - row.ReversedAmount));
		var academicYearName = await queries.FirstOrDefaultAsync((await academicYears.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id && row.Id == link.AcademicYearId).Select(row => row.Name));
		return new()
		{
			Source = new() { Kind = link.SourceKind, SourceId = link.SourceId, SourceVersion = link.SourceVersion },
			FinanceChargeRequestId = link.FinanceChargeRequestId, FinanceChargeId = charge.Id,
			AcademicYearId = link.AcademicYearId, AcademicYearName = academicYearName, Payer = source.Payer, Currency = link.Currency,
			AssessmentDate = charge.SchoolAssessmentDate.Value, DueDate = charge.SchoolDueDate.Value,
			OriginalAmount = charge.Amount, AcceptedPaymentAmount = balance.Accepted, SettledPaymentAmount = balance.Settled,
			PendingVerificationAmount = balance.PendingVerification, WaivedAmount = balance.NetWaived, RefundedAmount = refunded,
			OutstandingAmount = balance.Collectible, FinancialOutstandingAmount = balance.Unsettled,
			ApprovalStatus = charge.Status, SettlementAvailable = false,
			SettlementBlockedReason = "Finance collection is unavailable for this receivable."
		};
	}
}
