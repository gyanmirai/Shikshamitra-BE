using System;
using System.Collections.Generic;

namespace ShikshaMitra.Application.Finance.Fees.Receipts;

public sealed record ReceiptSchoolIdentity(string Name, string? Address = null, byte[]? Logo = null);

/// <summary>Amounts are canonical collection snapshots, not today's outstanding balances.</summary>
public sealed record ReceiptDocumentLine(string FeeHead, string Installment,
	decimal? TotalFees, decimal? PaidFees, decimal? PendingFees, decimal ReceivedAmount,
	bool IsCharge = false);

public sealed record ReceiptDocumentField(string Label, string? Value);

public sealed class ReceiptDocumentModel
{
	public required ReceiptSchoolIdentity School { get; init; }
	public required string Title { get; init; }
	public required string Number { get; init; }
	public required DateOnly Date { get; init; }
	public string? AcademicYear { get; init; }
	public IReadOnlyList<ReceiptDocumentField> StudentFields { get; init; } = [];
	public IReadOnlyList<ReceiptDocumentField> PaymentFields { get; init; } = [];
	public IReadOnlyList<ReceiptDocumentLine> Lines { get; init; } = [];
	public required decimal Amount { get; init; }
	public bool IsRefund { get; init; }
	public string? RelatedReceipt { get; init; }
	public string? Status { get; init; }
}
