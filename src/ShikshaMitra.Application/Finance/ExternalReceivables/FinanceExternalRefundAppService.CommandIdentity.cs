using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Volo.Abp;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalRefundAppService
{
	private static string CanonicalPayoutMethod(string value)
	{
		if (!FeeRefundAppService.TryMapPayoutMethod(value, out var method))
			throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed);
		return method switch
		{
			RefundPayoutMethod.Cash => "Cash",
			RefundPayoutMethod.Cheque => "Cheque",
			RefundPayoutMethod.DemandDraft => "Demand Draft (DD)",
			RefundPayoutMethod.BankTransfer => "Bank Transfer (NEFT/RTGS/IMPS)",
			RefundPayoutMethod.Upi => "UPI",
			_ => throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed)
		};
	}

	private static string? CanonicalDetails(Dictionary<string, object?>? details)
	{
		if (details == null) return null;
		using var bytes = new MemoryStream();
		using (var writer = new Utf8JsonWriter(bytes))
			WriteCanonical(writer, JsonSerializer.SerializeToElement(details));
		return Encoding.UTF8.GetString(bytes.ToArray());
	}

	private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Object)
		{
			writer.WriteStartObject();
			foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
			{
				writer.WritePropertyName(property.Name);
				WriteCanonical(writer, property.Value);
			}
			writer.WriteEndObject();
		}
		else if (value.ValueKind == JsonValueKind.Array)
		{
			writer.WriteStartArray();
			foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
			writer.WriteEndArray();
		}
		else value.WriteTo(writer);
	}
}
