using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundManager
{
	public async Task<FeeRefundRequest> CreateExternalPendingAsync(FinanceExternalReceivableSourceLink link,
		Charge charge, FinanceExternalPayerIdentity payer, FinancePolicyVersion policyVersion,
		FeePaymentReceipt receipt, IReadOnlyCollection<RefundLineSpec> lines, string method,
		RefundPaymentDetails? details, string reason, string calculationSnapshot, string calculationHash,
		Guid actorId, string idempotencyKey)
	{
		if (link.TenantId != currentTenant.Id || charge.StudentId != payer.StudentId || charge.EmployeeId != payer.EmployeeId ||
			policyVersion.TenantId != currentTenant.Id || policyVersion.PolicyType != "refund" || policyVersion.AcademicYearId != link.AcademicYearId ||
			receipt.TenantId != currentTenant.Id || receipt.StudentId != payer.StudentId || receipt.EmployeeId != payer.EmployeeId ||
			receipt.FinanceFeeAccountId.HasValue || receipt.AcademicYearId != link.AcademicYearId ||
			!RefundReceiptEvidencePolicy.IsSettled(receipt) || !RefundReceiptEvidencePolicy.CoversCharges(receipt) || lines.Count == 0 ||
			lines.Select(line => line.SourceAllocationId).Distinct().Count() != lines.Count)
			throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
		var sources = await LazyServiceProvider.LazyGetRequiredService<FinanceExternalRefundSourceReader>().ReadAsync(link, charge);
		foreach (var line in lines)
		{
			var source = sources.SingleOrDefault(row => row.AllocationId == line.SourceAllocationId && row.ReceiptId == line.SourceReceiptId);
			if (source == null || line.ChargeId != charge.Id || line.DueFeeId.HasValue || line.LateFeeDueFeeId.HasValue)
				throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
			FinanceExternalSettlementAmounts.EnsureMoney(line.Amount, positive: true);
			FinanceExternalSettlementAmounts.EnsureMoney(line.CalculatedAmount);
			if (line.Amount > source.Available || line.CalculatedAmount > line.Amount || line.SettledPaidAmount != source.Available)
				throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
		}
		var id = GuidGenerator.Create();
		var entities = lines.Select(line => ToEntity(id, line)).ToList();
		var request = FeeRefundRequest.CreateExternal(id, currentTenant.Id!.Value, payer, link.AcademicYearId,
			link.Id, GenerateVoucherNumber(), policyVersion.PolicyId, policyVersion.Id, receipt.Id, receipt.ReceiptNumber,
			entities, method, details, reason, policyVersion.SnapshotJson, calculationSnapshot, calculationHash, actorId, idempotencyKey);
		return await repository.InsertAsync(request, autoSave: false);
	}
}
