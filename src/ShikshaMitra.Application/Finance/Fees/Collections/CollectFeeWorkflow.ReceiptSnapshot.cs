using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class CollectFeeWorkflow
{
	private async Task<List<ReceiptDocumentLine>> CaptureReceiptLinesAsync(CollectFeeInput input,
		DueFeeValidationContext fees, ChargeValidationContext charges, Dictionary<Guid, DueFee> lateDues)
	{
		var lines = input.DueFeePayments.Select(payment =>
		{
			var due = fees.DueFeeMap[payment.DueFeeId];
			var amount = (decimal)payment.Amount;
			return ReceiptCollectionSnapshot.FeeLine(due, fees.FeeHeadMap[payment.FeeHeadId].Name, amount);
		}).ToList();
		foreach (var payment in input.ChargePayments)
		{
			var charge = charges.ChargeMap[payment.ChargeId];
			var payable = charges.PayableChargeMap[payment.ChargeId];
			var pending = (decimal)charges.PendingChargeMap[payment.ChargeId];
			var amount = (decimal)payment.Amount;
			lines.Add(new(string.IsNullOrWhiteSpace(charge.ChargeName) ? charge.PurposeDescription : charge.ChargeName,
				"—", payable, payable - pending + amount, Math.Max(0m, pending - amount), amount, IsCharge: true));
		}
		if (input.LateFeePayments.Count > 0)
		{
			var balances = await lateFeeBalanceService.GetBalancesAsync(lateDues.Values.ToList(), input.AcademicYearId, clock.Now);
			foreach (var payment in input.LateFeePayments)
			{
				var due = lateDues[payment.DueFeeId];
				var balance = balances[payment.DueFeeId];
				var amount = (decimal)payment.Amount;
				lines.Add(new("Late fees", ClassicReceiptPdfRenderer.Period(due.CoverageStartDate, due.CoverageEndDate,
					due.GetDisplayInstallmentLabel()), balance.Total - balance.WaivedAmount, balance.PaidAmount + amount,
					Math.Max(0m, balance.PendingAmount - amount), amount, IsCharge: true));
			}
		}
		return lines;
	}
}
