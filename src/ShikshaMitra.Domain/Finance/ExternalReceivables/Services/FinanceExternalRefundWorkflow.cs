using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ExternalReceivables;

/// <summary>Validates the existing refundable allocations; it does not create a separate balance.</summary>
public class FinanceExternalRefundWorkflow(IRepository<FinanceExternalReceivableSourceLink, Guid> links,
	IRepository<FinanceExternalSettlementPolicy, Guid> bindings, IRepository<FinancePolicyVersion, Guid> versions,
	IRepository<Charge, Guid> charges, FinanceExternalRefundSourceReader sources, ICurrentTenant tenant) : DomainService
{
	public async Task<List<PaidChargeRefundSource>> ReadSourcesAsync(FeeRefundRequest request)
	{
		if (!tenant.Id.HasValue || request.TenantId != tenant.Id || !request.FinanceExternalSourceLinkId.HasValue ||
			request.WorkflowVersion != 1 || request.StudentFeeStructureId.HasValue || request.FinanceFeeAccountId.HasValue)
			throw InvalidSource();
		var link = await links.FindAsync(request.FinanceExternalSourceLinkId.Value, includeDetails: false);
		var charge = link == null ? null : await charges.FindAsync(link.FinanceChargeId, includeDetails: false);
		if (link == null || charge == null || link.TenantId != tenant.Id || charge.TenantId != tenant.Id ||
			link.AcademicYearId != request.AcademicYearId || charge.AcademicYearId != request.AcademicYearId ||
			charge.ChargeRequestId != link.FinanceChargeRequestId || charge.StudentId != request.StudentId ||
			charge.EmployeeId != request.EmployeeId || request.Currency != link.Currency)
			throw InvalidSource();
		await EnsurePolicyAsync(request, link);
		var eligible = await sources.ReadAsync(link, charge, request.Id);
		if (!request.OriginalReceiptId.HasValue || request.Lines.Count == 0 ||
			!request.Lines.Any(line => line.SourceReceiptId == request.OriginalReceiptId) ||
			request.Lines.Select(line => line.SourceAllocationId).Distinct().Count() != request.Lines.Count)
			throw InvalidSource();
		foreach (var line in request.Lines)
		{
			var source = eligible.SingleOrDefault(row => row.AllocationId == line.SourceAllocationId && row.ReceiptId == line.SourceReceiptId);
			if (source == null || line.ChargeId != charge.Id || line.DueFeeId.HasValue || line.LateFeeDueFeeId.HasValue ||
				line.SettledPaidAmount > source.Paid || line.RequestedAmount > line.SettledPaidAmount)
				throw InvalidSource();
			FinanceExternalSettlementAmounts.EnsureMoney(line.RequestedAmount, positive: true);
			FinanceExternalSettlementAmounts.EnsureMoney(line.CalculatedAmount);
			if (line.CalculatedAmount > line.RequestedAmount || line.ProcessedAmount - line.ReversedAmount > source.Paid)
				throw InvalidSource();
		}
		return eligible;
	}

	public async Task ApproveAsync(FeeRefundRequest request, Guid actorId, string? notes)
	{
		var eligible = await ReadSourcesAsync(request);
		var reservations = new List<FeeRefundReservation>();
		foreach (var line in request.Lines.Where(line => line.CalculatedAmount > 0m))
		{
			var source = eligible.Single(row => row.AllocationId == line.SourceAllocationId && row.ReceiptId == line.SourceReceiptId);
			if (line.CalculatedAmount > source.Available)
				throw new BusinessException(FeeRefundErrorCodes.ReservationConflict);
			reservations.Add(new FeeRefundReservation(GuidGenerator.Create(), request.Id, line.Id, null,
				line.SourceAllocationId, line.CalculatedAmount, line.ChargeId));
		}
		request.Approve(actorId, reservations, notes);
	}

	private async Task EnsurePolicyAsync(FeeRefundRequest request, FinanceExternalReceivableSourceLink link)
	{
		var binding = await bindings.FindAsync(row => row.TenantId == tenant.Id && row.SourceLinkId == link.Id);
		var version = request.RefundPolicyVersionId.HasValue
			? await versions.FindAsync(request.RefundPolicyVersionId.Value, includeDetails: false) : null;
		if (binding == null || version == null || version.TenantId != tenant.Id || version.PolicyType != "refund" ||
			version.AcademicYearId != request.AcademicYearId || version.PolicyId != request.RefundPolicyId ||
			binding.RefundPolicyVersionId != version.Id || link.RefundPolicyVersionId.HasValue && link.RefundPolicyVersionId != version.Id ||
			!HasSamePolicy(request.PolicySnapshotJson, version.SnapshotJson))
			throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
		RefundPolicySnapshot? policy;
		try { policy = JsonSerializer.Deserialize<RefundPolicySnapshot>(version.SnapshotJson); }
		catch (JsonException) { throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid); }
		if (policy == null || !policy.IncludePaidCharges || policy.PolicyId != version.PolicyId ||
			policy.AcademicYearId != request.AcademicYearId || policy.Status != RefundPolicyStatus.Active ||
			request.EmployeeId.HasValue && policy.MaximumPerStudentYear.HasValue)
			throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
	}

	private static bool HasSamePolicy(string? recorded, string original)
	{
		if (string.IsNullOrWhiteSpace(recorded)) return false;
		try
		{
			using var recordedDocument = JsonDocument.Parse(recorded);
			using var originalDocument = JsonDocument.Parse(original);
			return JsonElement.DeepEquals(recordedDocument.RootElement, originalDocument.RootElement);
		}
		catch (JsonException) { return false; }
	}

	private static BusinessException InvalidSource() => new(FeeRefundErrorCodes.SourceReceiptInvalid);
}
