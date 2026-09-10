using System;

namespace ShikshaMitra.Seeders;

internal static class AccountSettingSeedCode
{
	// Sequential GUIDs can share their leading characters. Keep the complete ID,
	// which fits the 32-character account-setting code limit without truncation.
	internal static string FromId(Guid id) => id.ToString("N").ToUpperInvariant();
}
