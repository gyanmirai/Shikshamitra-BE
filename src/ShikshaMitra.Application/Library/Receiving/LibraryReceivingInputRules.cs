using System;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.Library.Receiving;

internal static class LibraryReceivingInputRules
{
	public static bool ValidHeader(LibraryReceivingInput input) => input.ReceivedOn != default && input.Source.HasValue &&
		Enum.IsDefined(input.Source.Value) && input.SourceName?.Length is not > 256 && input.Reference?.Length is not > 256;

	public static bool TryCost(string? text, string? currency, out decimal? cost)
	{
		cost = null;
		if (text == null) return currency == null;
		if (string.IsNullOrEmpty(text) || text.Any(character => character != '.' && character is not (>= '0' and <= '9')) ||
			text.Count(character => character == '.') > 1 || text.StartsWith('.') || text.EndsWith('.') ||
			text.Contains('.') && text.Length - text.IndexOf('.') - 1 > 2 ||
			!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) ||
			parsed > LibraryStockReceiptRules.MaximumUnitCost || currency == null || currency.Length != 3 ||
			currency.Any(character => character is < 'A' or > 'Z')) return false;
		cost = parsed;
		return true;
	}

	public static string? CostText(decimal? cost) => cost?.ToString("0.00", CultureInfo.InvariantCulture);

	public static LibraryReceivingInput Normalize(LibraryReceivingInput input) => new()
	{
		ReceivedOn = input.ReceivedOn, Source = input.Source,
		SourceName = string.IsNullOrWhiteSpace(input.SourceName) ? null : input.SourceName.Trim(),
		Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim(),
		Lines = input.Lines.Select(line => new LibraryReceivingLineInput
		{
			BookId = line.BookId, SectionId = line.SectionId, ShelfId = line.ShelfId, Quantity = line.Quantity,
			UnitCost = TryCost(line.UnitCost, line.Currency, out var amount) ? CostText(amount) : line.UnitCost,
			Currency = line.Currency, Accessions = line.Accessions?.Select(value => value?.Trim() ?? string.Empty).ToList()
		}).ToList()
	};
}
