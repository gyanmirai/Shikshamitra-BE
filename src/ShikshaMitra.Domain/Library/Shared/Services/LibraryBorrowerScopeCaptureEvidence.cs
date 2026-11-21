using System;
using System.Text.Json;

namespace ShikshaMitra.Library.Shared;

public static class LibraryBorrowerScopeCaptureEvidence
{
	public static Guid? ReadCaptureId(string snapshot)
	{
		try
		{
			using var json = JsonDocument.Parse(snapshot);
			var root = json.RootElement;
			if (root.ValueKind != JsonValueKind.Object) return null;
			if (root.TryGetProperty("Loan", out var loan) && loan.ValueKind == JsonValueKind.Object) root = loan;
			return root.TryGetProperty("BorrowerScopeCaptureId", out var value) && value.ValueKind == JsonValueKind.String &&
				value.TryGetGuid(out var id) && id != Guid.Empty ? id : null;
		}
		catch (JsonException) { return null; }
	}
}
