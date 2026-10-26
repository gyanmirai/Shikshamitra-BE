using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.Receipts.Dtos;

public class FeeReceiptPaymentInstrumentDto
{
	public PaymentMethodEnum PaymentMethod { get; set; }
	public decimal Amount { get; set; }
	public DateTimeOffset PaymentDate { get; set; }
	public Guid? BankAccountId { get; set; }
	/// <summary>Masked receiving bank snapshot from the original collection.</summary>
	public string? BankLabel { get; set; }
	public Dictionary<string, string?> InstrumentDetails { get; set; } = [];
}
