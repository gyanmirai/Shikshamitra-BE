using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShikshaMitra.Library;
using Volo.Abp;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalLedgerFact(Guid Id, string Kind, string Status, decimal Amount,
	Guid? RelatedId = null, decimal ReversedAmount = 0m);

public static class FinanceExternalBalanceRevision
{
	public static string Create(FinanceExternalSettlementContext context, Guid? policyBindingId,
		IEnumerable<FinanceExternalLedgerFact> facts)
	{
		var payload = new
		{
			Schema = 1, context.Link.SourceFingerprint, context.Charge.ConcurrencyStamp,
			ChargeStatus = (int)context.Charge.Status, PolicyBindingId = policyBindingId,
			Facts = facts.OrderBy(row => row.Kind, StringComparer.Ordinal).ThenBy(row => row.Id)
				.ThenBy(row => row.RelatedId).Select(row => new
				{
					row.Id, row.Kind, row.Status, row.RelatedId,
					Amount = row.Amount.ToString("G29", CultureInfo.InvariantCulture),
					ReversedAmount = row.ReversedAmount.ToString("G29", CultureInfo.InvariantCulture)
				}).ToArray()
		};
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)))).ToLowerInvariant();
	}

	public static void EnsureCurrent(string supplied, string current)
	{
		if (string.IsNullOrWhiteSpace(supplied) || !string.Equals(supplied, current, StringComparison.Ordinal))
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidBalanceRevision,
				"The Finance balance changed. Refresh it before continuing.");
	}
}
