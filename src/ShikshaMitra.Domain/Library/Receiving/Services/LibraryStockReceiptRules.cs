using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Library.Receiving;

public static class LibraryStockReceiptRules
{
	public const int MaximumLines = 100;
	public const int MaximumCopies = 5000;
	public const decimal MaximumUnitCost = 9999999999999999.99m;

	public static void Validate(Guid id, Guid tenantId, DateOnly receivedOn, LibraryStockSource source,
		string? sourceName, string? reference, Guid actorId, DateTimeOffset at, IReadOnlyList<LibraryStockLineEvidence> lines)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || actorId == Guid.Empty || receivedOn == default ||
			at == default || !Enum.IsDefined(source) || sourceName?.Length > 256 || reference?.Length > 256 ||
			lines == null || lines.Count is < 1 or > MaximumLines)
			throw Invalid();

		var lineIds = new HashSet<Guid>();
		var copyIds = new HashSet<Guid>();
		var accessions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var copyNumbers = new HashSet<(Guid, int)>();
		foreach (var line in lines)
		{
			if (line == null || line.Id == Guid.Empty || !lineIds.Add(line.Id) || line.BookId == Guid.Empty ||
				line.SectionId == Guid.Empty || line.ShelfId == Guid.Empty || line.ExpectedCopyCount is < 1 or > MaximumCopies ||
				line.Copies == null || line.Copies.Count != line.ExpectedCopyCount)
				throw Invalid();
			ValidateCost(line.UnitCost, line.Currency);
			foreach (var copy in line.Copies)
			{
				if (copy == null || copy.CopyId == Guid.Empty || !copyIds.Add(copy.CopyId) || copyIds.Count > MaximumCopies ||
					copy.CopyNumber < 1 || !copyNumbers.Add((line.BookId, copy.CopyNumber)) ||
					string.IsNullOrWhiteSpace(copy.AccessionNumber) || copy.AccessionNumber.Length > 50 ||
					copy.AccessionNumber.Any(char.IsControl) || !accessions.Add(copy.AccessionNumber.Trim()))
					throw Invalid();
			}
		}
	}

	private static void ValidateCost(decimal? cost, string? currency)
	{
		if (!cost.HasValue)
		{
			if (currency != null) throw Invalid();
			return;
		}
		if (cost < 0 || cost > MaximumUnitCost || decimal.Round(cost.Value, 2) != cost ||
			currency == null || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
			throw Invalid();
	}

	private static BusinessException Invalid() => new(LibraryErrorCodes.ReceivingInvalid);
}
