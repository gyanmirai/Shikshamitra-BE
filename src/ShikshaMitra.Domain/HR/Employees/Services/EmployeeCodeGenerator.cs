using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Domain service responsible for generating human-readable, unique employee codes.
///
/// Format: {RoleCode}{Year2Digit}{Sequence4Char}
/// Example: TEA240001 — first teacher hired in 2024.
///
/// Uniqueness relies on:
/// 1. Tenant isolation — the count query is scoped to the current tenant.
/// 2. Year scoping — the 2-digit year is embedded in the code.
/// 3. Sequential count — increments for each hire per year.
///
/// NOTE: A unique DB index on EmployeeCode acts as the safety net against
/// concurrent-creation race conditions.
/// </summary>
public class EmployeeCodeGenerator(
	IEmployeeRepository employeeRepository,
	IDataFilter dataFilter
) : DomainService
{
	private static readonly ConcurrentDictionary<string, int> SequenceByScope = new();

	/// <summary>
	/// Generates a unique employee code for the given joining date and role.
	/// </summary>
	public async Task<string> GenerateAsync(DateOnly dateOfJoining, IdentityRole role)
	{
		var year = dateOfJoining.Year;
		var tenantId = CurrentTenant.Id ?? Guid.Empty;
		string roleCode = GetRoleCode(role);
		var existingMaxSequence = await GetMaxSequenceForRoleAndYearAsync(year, roleCode);
		var scopeKey = $"{tenantId:N}:{year}:{roleCode}";
		var nextSequence = SequenceByScope.AddOrUpdate(
			scopeKey,
			_ => existingMaxSequence + 1,
			(_, currentSequence) => Math.Max(currentSequence + 1, existingMaxSequence + 1));

		string yearCode = (year % 100).ToString("D2");
		string uniqueHash = GenerateUniqueHashFromSequence(nextSequence);

		return $"{roleCode}{yearCode}{uniqueHash}";
	}

	private async Task<int> GetMaxSequenceForRoleAndYearAsync(int year, string roleCode)
	{
		var yearCode = (year % 100).ToString("D2");
		var codePrefix = $"{roleCode}{yearCode}";
		using var _ = dataFilter.Disable<ISoftDelete>();
		var queryable = await employeeRepository.GetQueryableAsync();
		var existingCodes = await AsyncExecuter.ToListAsync(
			queryable
				.Where(e => e.EmployeeCode.StartsWith(codePrefix))
				.Select(e => e.EmployeeCode));

		return existingCodes
			.Select(code => TryParseSequence(code, codePrefix))
			.DefaultIfEmpty(0)
			.Max();
	}

	private static string GenerateUniqueHashFromSequence(int sequence)
	{
		var sequenceCode = ToBase36(sequence);
		return sequenceCode.PadLeft(4, '0')[^4..].ToUpperInvariant();
	}

	/// <summary>
	/// Converts an integer to base-36 representation (0–9, A–Z).
	/// </summary>
	private static string ToBase36(int value)
	{
		const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		var result = string.Empty;

		do
		{
			result = chars[value % 36] + result;
			value /= 36;
		} while (value > 0);

		return result;
	}

	private static int TryParseSequence(string? employeeCode, string codePrefix)
	{
		if (
			string.IsNullOrWhiteSpace(employeeCode) ||
			employeeCode.Length < codePrefix.Length + 4 ||
			!employeeCode.StartsWith(codePrefix, StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}

		var suffix = employeeCode[^4..].ToUpperInvariant();
		var value = 0;
		foreach (var character in suffix)
		{
			var digit = character switch
			{
				>= '0' and <= '9' => character - '0',
				>= 'A' and <= 'Z' => character - 'A' + 10,
				_ => -1
			};
			if (digit < 0)
			{
				return 0;
			}

			value = value * 36 + digit;
		}

		return value;
	}

	/// <summary>
	/// Resolves a 3-character role code from the role's <c>Code</c> extra property,
	/// falling back to the first 3 uppercase letters of the role name.
	/// Set the explicit code via: <code>role.SetProperty("Code", "TEA")</code>
	/// </summary>
	private static string GetRoleCode(IdentityRole role)
	{
		role.ExtraProperties.TryGetValue("Code", out var codeObj);
		var explicitCode = codeObj?.ToString();

		if (!string.IsNullOrWhiteSpace(explicitCode))
		{
			return explicitCode.Trim().ToUpperInvariant()[..Math.Min(3, explicitCode.Trim().Length)].PadRight(3, 'X');
		}

		if (string.IsNullOrWhiteSpace(role.Name))
		{
			return "UNK";
		}

		var normalized = role.Name.Trim().ToUpperInvariant().Replace(" ", "");
		return normalized.Length >= 3
			? normalized[..3]
			: normalized.PadRight(3, 'X');
	}
}
