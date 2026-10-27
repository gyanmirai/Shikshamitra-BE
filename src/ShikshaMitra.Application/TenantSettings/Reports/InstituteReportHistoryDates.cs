using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Volo.Abp.Linq;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.Reports;

internal static class InstituteReportHistoryDates
{
	private static readonly string[] DateWords = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec", "am", "pm", "t", "z"];
	public static async Task<Guid[]> MatchingIdsAsync(IQueryable<InstituteReportJob> query, InstituteReportHistoryInput input, IAsyncQueryableExecuter executor)
	{
		var zone = ResolveZone(input.TimeZone);
		var term = input.Filter?.Trim().ToLowerInvariant();
		if (string.IsNullOrEmpty(term) || !CouldMatchDate(term)) return [];
		// Only explicit date-like searches scan timestamps; normal history pages never load the saved requests in bulk.
		var dates = await executor.ToListAsync(query.Select(x => new { x.Id, x.CreationTime, x.CompletedAt, x.ExpiresAt }));
		return dates.Where(x => Matches(x.CreationTime, zone, term) || Matches(x.ExpiresAt, zone, term)
			|| (x.CompletedAt.HasValue && Matches(x.CompletedAt.Value, zone, term))).Select(x => x.Id).ToArray();
	}
	private static TimeZoneInfo ResolveZone(string? id)
	{
		if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
		try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
		catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
		{ throw new AbpValidationException("Use a supported report history time zone."); }
	}
	private static bool CouldMatchDate(string term)
	{
		if (term.Length > 200 || term.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not (' ' or ':' or '-' or '.' or '+'))) return false;
		return Regex.Matches(term, "[a-z]+").All(word => DateWords.Any(part => part.Contains(word.Value, StringComparison.Ordinal)));
	}
	private static bool Matches(DateTime value, TimeZoneInfo zone, string term)
	{
		var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
		var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
		return Has(local.ToString("dd MMM yyyy h:mm tt", CultureInfo.InvariantCulture), term)
			|| Has(local.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), term)
			|| Has(local.ToString("hh:mm tt", CultureInfo.InvariantCulture), term)
			|| Has(local.ToString("h:mm tt", CultureInfo.InvariantCulture), term)
			|| Has(JsonSerializer.Serialize(value).Trim('"'), term) || Has(JsonSerializer.Serialize(utc).Trim('"'), term);
	}
	private static bool Has(string text, string term) => text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
