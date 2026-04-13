using System;
using System.Runtime.CompilerServices;

namespace ShikshaMitra.Infrastructure.Data;

/// <summary>
/// Generates unique, deterministic test data identifiers to prevent
/// collisions between parallel tests while keeping names human-readable.
/// </summary>
public static class TestDataHelper
{
	/// <summary>
	/// Generates a unique name with a prefix and short GUID suffix.
	/// Example: "Test_EarningComponent_3a7f" — readable in logs, unique per run.
	/// </summary>
	public static string UniqueName(string prefix) =>
		$"Test_{prefix}_{Guid.NewGuid().ToString("N")[..8]}";

	/// <summary>
	/// Generates a unique name using the calling test method name as prefix.
	/// </summary>
	public static string UniqueNameFromCaller([CallerMemberName] string caller = "") =>
		$"Test_{caller}_{Guid.NewGuid().ToString("N")[..8]}";

	/// <summary>
	/// Returns a unique email address for test data.
	/// </summary>
	public static string UniqueEmail(string prefix = "testuser") =>
		$"{prefix}_{Guid.NewGuid().ToString("N")[..6]}@test.shikshamitra.local";

	/// <summary>
	/// Returns a unique phone number string for test data.
	/// </summary>
	public static string UniquePhone() =>
		$"90{Random.Shared.Next(10000000, 99999999)}";
}
