using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructure
{
	/// <summary>Selected heads, including zero amounts. Null means legacy or unreadable evidence; readers conservatively use structure heads.</summary>
	private Guid[]? _includedFeeHeadIds;
	public Guid[]? IncludedFeeHeadIds { get => _includedFeeHeadIds?.ToArray(); private set => _includedFeeHeadIds = value; }

	private static Guid[]? ReadIncludedFeeHeadIds(string snapshot)
	{
		try
		{
			using var document = JsonDocument.Parse(snapshot);
			if (document.RootElement.ValueKind != JsonValueKind.Object ||
				!document.RootElement.TryGetProperty("Heads", out var heads) || heads.ValueKind != JsonValueKind.Array) return null;
			var selected = new HashSet<Guid>();
			foreach (var head in heads.EnumerateArray())
			{
				if (head.ValueKind != JsonValueKind.Object) return null;
				// Match the snapshot contract defaults for omitted IDs and Included flags.
				var included = false;
				if (head.TryGetProperty("Included", out var flag))
				{
					if (flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
					included = flag.GetBoolean();
				}
				var id = Guid.Empty;
				if (head.TryGetProperty("FeeHeadId", out var value) && !value.TryGetGuid(out id)) return null;
				if (included) selected.Add(id);
			}
			return selected.Order().ToArray();
		}
		catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException) { return null; }
	}
}
