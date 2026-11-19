using System.Collections.Generic;
using System;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.Data;

namespace ShikshaMitra.Application.Finance.Fees.Receipts;

public static class ReceiptCollectionSnapshot
{
	private const string Property = "ReceiptDocumentLinesV1";
	private const string OrganizationProperty = "ReceiptOrganizationId";

	public static void CaptureOrganization(FeeRecordMetadata metadata, Guid? organizationId)
	{
		if (!metadata.ExtraProperties.ContainsKey(OrganizationProperty))
			metadata.SetProperty(OrganizationProperty, organizationId);
	}

	public static Guid? ReadOrganization(FeeRecordMetadata? metadata) => metadata?.GetProperty<Guid?>(OrganizationProperty);

	public static ReceiptDocumentLine FeeLine(DueFee due, string feeHead, decimal amount) => new(feeHead,
		ClassicReceiptPdfRenderer.Period(due.CoverageStartDate, due.CoverageEndDate, due.GetDisplayInstallmentLabel()),
		due.NetPayableAmount, due.PaidAmount + amount, System.Math.Max(0m, due.OutstandingAmount - amount), amount);

	public static void Capture(FeeRecordMetadata metadata, IReadOnlyList<ReceiptDocumentLine> lines)
	{
		// The collection transaction is the shared source for provisional and verified documents.
		if (!metadata.ExtraProperties.ContainsKey(Property))
			metadata.SetProperty(Property, JsonSerializer.Serialize(lines));
	}

	public static IReadOnlyList<ReceiptDocumentLine> Read(FeeRecordMetadata? metadata)
	{
		var json = metadata?.GetProperty<string>(Property);
		return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<ReceiptDocumentLine>>(json) ?? [];
	}
}
