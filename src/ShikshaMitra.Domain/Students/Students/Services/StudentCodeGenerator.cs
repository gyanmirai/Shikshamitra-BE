using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students;

/// <summary>
/// Domain service responsible for generating human-readable, unique General Register (GR) numbers.
///
/// Format : {MediumCode}{Year2Digit}{Sequence4Char}
/// Example: ENG260001 — first English-medium student enrolled in 2026.
///
/// Uniqueness relies on:
/// 1. Tenant isolation   — the count query is scoped to the current tenant.
/// 2. Year scoping       — the 2-digit year is embedded in the GR number.
/// 3. Sequential count   — increments for each enrolment per year.
///
/// NOTE: A unique DB index on GrNo acts as the safety net against
/// concurrent-creation race conditions.
/// </summary>
public class StudentCodeGenerator(IStudentRepository studentRepository) : DomainService
{
    /// <summary>
    /// Generates a unique GR number for the given enrolment date and education medium name.
    /// </summary>
    /// <param name="dateOfEnrolment">Date the student is being enrolled.</param>
    /// <param name="mediumName">Display name of the education medium (e.g. "English", "Hindi").</param>
    public async Task<string> GenerateAsync(DateOnly dateOfEnrolment, string? mediumName)
    {
        var year     = dateOfEnrolment.Year;
        var tenantId = CurrentTenant.Id ?? Guid.Empty;

        var existingCount = await studentRepository.GetEnrolledCountForYearAsync(
            tenantId == Guid.Empty ? null : tenantId,
            year);

        string mediumCode = GetMediumCode(mediumName);
        string yearCode   = (year % 100).ToString("D2");
        string sequence   = GenerateUniqueHash(existingCount);

        return $"{mediumCode}{yearCode}{sequence}";
    }

    /// <summary>
    /// Converts <paramref name="count"/> into a deterministic 4-character base-36 suffix.
    /// base-36^4 = 1,679,616 unique codes per medium/year/tenant.
    /// </summary>
    private static string GenerateUniqueHash(int count)
    {
        string sequence = ToBase36(count + 1);
        return sequence.PadLeft(4, '0')[^4..].ToUpperInvariant();
    }

    /// <summary>Converts an integer to base-36 representation (0–9, A–Z).</summary>
    private static string ToBase36(int value)
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string result = string.Empty;
        do
        {
            result = chars[value % 36] + result;
            value /= 36;
        } while (value > 0);
        return result;
    }

    /// <summary>
    /// Resolves a 3-character medium code from the medium display name,
    /// falling back to "STU" if the name is missing.
    /// </summary>
    private static string GetMediumCode(string? mediumName)
    {
        if (string.IsNullOrWhiteSpace(mediumName))
            return "STU";

        string normalized = mediumName.Trim().ToUpperInvariant().Replace(" ", "");
        return normalized.Length >= 3
            ? normalized[..3]
            : normalized.PadRight(3, 'X');
    }
}
