using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Finance;

/// <summary>Canonical display names and comparisons for Finance configuration.</summary>
public static class FinanceName
{
	public static string Normalize(string? value) => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

	public static bool Equals(string? left, string? right) =>
		string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

	public static string NextCopy(string source, IEnumerable<string> existingNames, int maxLength = 128)
	{
		var names = existingNames.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var stem = Normalize(source);
		for (var number = 1; ; number++)
		{
			var suffix = number == 1 ? " Copy" : $" Copy {number}";
			var candidate = stem[..Math.Min(stem.Length, maxLength - suffix.Length)].TrimEnd() + suffix;
			if (!names.Contains(candidate)) return candidate;
		}
	}
}
