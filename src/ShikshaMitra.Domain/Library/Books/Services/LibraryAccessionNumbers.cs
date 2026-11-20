using System;
using System.Collections.Generic;
using System.Globalization;
using Volo.Abp;

namespace ShikshaMitra.Library.Books;

public static class LibraryAccessionNumbers
{
	public static IReadOnlyList<string> Allocate(IEnumerable<string> reservedValues, int quantity)
	{
		if (quantity < 0) throw new BusinessException(LibraryErrorCodes.InvalidCopyIdentity);
		if (quantity == 0) return [];
		var reserved = new HashSet<string>(reservedValues, StringComparer.OrdinalIgnoreCase);
		long next = 0;
		foreach (var value in reserved)
			if (value.StartsWith("ACC-", StringComparison.OrdinalIgnoreCase) &&
				long.TryParse(value.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
				next = Math.Max(next, number);
		var result = new List<string>(quantity);
		while (result.Count < quantity)
		{
			if (next == long.MaxValue) throw new BusinessException(LibraryErrorCodes.InvalidCopyIdentity,
				"The automatic accession sequence is exhausted. Choose unique manual accessions before receiving these copies.");
			next++;
			var accession = $"ACC-{next.ToString("D6", CultureInfo.InvariantCulture)}";
			if (reserved.Add(accession)) result.Add(accession);
		}
		return result.AsReadOnly();
	}
}
