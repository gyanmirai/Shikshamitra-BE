using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalRefundAppService
{
	private sealed record PreparedSourceRefund(FinancePolicyVersion Version, RefundPolicySnapshot Policy,
		List<RefundLineSpec> Lines, RefundPolicyCalculationResult Calculation, string CalculationJson, string CalculationHash);

	private async Task<PreparedSourceRefund> PrepareAsync(FinanceExternalSettlementContext context, FinanceExternalRefundSelection input)
	{
		var state = await ReadFormAsync(context);
		if (!state.Form.CanRequest || state.Version == null || state.Policy == null)
			throw new BusinessException(FeeRefundErrorCodes.PolicyNotAssigned, string.Join(" ", state.Form.SetupIssues));
		FinanceExternalBalanceRevision.EnsureCurrent(input.Revision, state.Form.Revision);
		if (input.OriginalReceiptId == Guid.Empty || input.Lines.Count == 0 || input.Lines.Count > 100 ||
			input.Lines.Select(line => line.SourceAllocationId).Distinct().Count() != input.Lines.Count ||
			!input.Lines.Any(line => line.SourceReceiptId == input.OriginalReceiptId))
			throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
		if (!FeeRefundAppService.TryMapPayoutMethod(input.Method, out var method) || !state.Policy.AllowedPayoutMethods.Contains(method))
			throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed);
		var specs = new List<RefundLineSpec>();
		foreach (var line in input.Lines)
		{
			FinanceExternalSettlementAmounts.EnsureMoney(line.Amount, positive: true);
			var source = state.Sources.SingleOrDefault(row => row.AllocationId == line.SourceAllocationId && row.ReceiptId == line.SourceReceiptId);
			if (source == null) throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
			if (line.Amount > source.Available) throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
			specs.Add(new(null, source.AllocationId, source.ReceiptId, null, "Paid charges", source.ChargeName,
				source.Available, line.Amount, 0m, source.ChargeId));
		}
		var requested = specs.Sum(line => line.Amount);
		var calculation = LazyServiceProvider.LazyGetRequiredService<RefundPolicyCalculationService>().Calculate(state.Policy,
			new(specs.Sum(line => line.SettledPaidAmount), 0m, 0m, state.PayerYearCommitted, requested));
		if (calculation.FinalRefund <= 0m || calculation.FinalRefund > requested)
			throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount);
		specs = FeeRefundAppService.AllocateCalculatedAmounts(specs, calculation.FinalRefund);
		var json = JsonSerializer.Serialize(new { SourceLinkId = context.Link.Id, input.OriginalReceiptId,
			PolicyVersionId = state.Version.Id, RequestedAmount = requested, Result = calculation, Lines = specs.OrderBy(line => line.SourceAllocationId) });
		return new(state.Version, state.Policy, specs, calculation, json, Hash(json));
	}

	private static FeeRefundPreviewDto Preview(PreparedSourceRefund prepared) => new()
	{
		RefundPolicyId = prepared.Version.PolicyId, RefundPolicyVersionId = prepared.Version.Id,
		RefundPolicyName = prepared.Policy.PolicyName, SettledPaidAmount = prepared.Calculation.SettledPaidAmount,
		PriorProcessedRefund = prepared.Calculation.PriorProcessedRefund, ActiveReservation = prepared.Calculation.ActiveReservation,
		RequestedAmount = prepared.Lines.Sum(line => line.Amount), DeductionAmount = prepared.Calculation.DeductionAmount,
		CapAdjustment = prepared.Calculation.CapAdjustment, MaximumAllowedAmount = prepared.Calculation.FinalRefund, IsAllowed = true
	};

	private static string RequestFingerprint(FinanceExternalSettlementContext context, FinanceExternalRefundRequestInput input) => Hash(JsonSerializer.Serialize(new
	{
		context.Link.Id, context.Link.SourceFingerprint, input.Revision, input.OriginalReceiptId,
		Method = CanonicalPayoutMethod(input.Method), Reason = input.Reason.Trim(), PaymentDetails = CanonicalDetails(input.PaymentDetails),
		Lines = input.Lines.OrderBy(line => line.SourceAllocationId).Select(line => new
		{
			line.SourceAllocationId, line.SourceReceiptId, Amount = line.Amount.ToString("G29", CultureInfo.InvariantCulture)
		})
	}));
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
