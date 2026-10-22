using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

// Revisions are immutable. Changing the format affects only future allocations.
public class AdmissionApplicationNumberingRule : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StoredTenantId { get; private set; }
	public int Version { get; private set; }
	public string Prefix { get; private set; } = "APP";
	public string Separator { get; private set; } = "-";
	public string Suffix { get; private set; } = "";
	public AdmissionNumberYearFormat YearFormat { get; private set; }
	public AdmissionNumberResetPolicy ResetPolicy { get; private set; }
	public int Digits { get; private set; }
	public long StartingNumber { get; private set; }

	private AdmissionApplicationNumberingRule() { }

	public AdmissionApplicationNumberingRule(Guid id, Guid? tenantId, int version, string prefix, string separator,
		string? suffix, AdmissionNumberYearFormat yearFormat, AdmissionNumberResetPolicy resetPolicy, int digits, long startingNumber) : base(id)
	{
		prefix = (prefix ?? "").Trim().ToUpperInvariant();
		suffix = (suffix ?? "").Trim().ToUpperInvariant();
		if (!Regex.IsMatch(prefix, "^[A-Z0-9][A-Z0-9-]{0,19}$") || !Regex.IsMatch(suffix, "^[A-Z0-9-]{0,20}$") ||
			separator is not ("/" or "-" or "") || !Enum.IsDefined(yearFormat) || !Enum.IsDefined(resetPolicy) ||
			digits is < 1 or > 12 || startingNumber is < 1 or > 999999999 ||
			(resetPolicy == AdmissionNumberResetPolicy.AcademicYear && yearFormat != AdmissionNumberYearFormat.AcademicYear) ||
			(resetPolicy == AdmissionNumberResetPolicy.CalendarYear && yearFormat != AdmissionNumberYearFormat.CalendarYear))
			throw new BusinessException(AdmissionNumberingErrorCodes.Invalid,
				"Review the numbering fields. A yearly reset requires the matching year in the number.");
		TenantId = tenantId; StoredTenantId = tenantId ?? Guid.Empty; Version = version;
		Prefix = prefix; Separator = separator; Suffix = suffix; YearFormat = yearFormat;
		ResetPolicy = resetPolicy; Digits = digits; StartingNumber = startingNumber;
	}

	public string YearText(int calendarYear, int academicStart, int academicEnd) => YearFormat switch
	{
		AdmissionNumberYearFormat.CalendarYear => calendarYear.ToString(CultureInfo.InvariantCulture),
		AdmissionNumberYearFormat.AcademicYear => $"{academicStart:D4}-{academicEnd % 100:D2}",
		_ => ""
	};

	public string ScopeKey(int calendarYear, Guid academicYearId) => ResetPolicy switch
	{
		AdmissionNumberResetPolicy.CalendarYear => $"calendar:{calendarYear}",
		AdmissionNumberResetPolicy.AcademicYear => $"academic:{academicYearId:N}",
		_ => "continuous"
	};

	public string NumberPrefix(string year) => string.Join(Separator, new[] { Prefix, year }.Where(x => x.Length > 0)) + Separator;
	public string NumberSuffix => Suffix.Length == 0 ? "" : Separator + Suffix;
	public string Format(long sequence, string year)
	{
		var value = NumberPrefix(year) + sequence.ToString($"D{Digits}", CultureInfo.InvariantCulture) + NumberSuffix;
		if (sequence < 1 || value.Length > 64)
			throw new BusinessException(AdmissionNumberingErrorCodes.Invalid, "The application number exceeds the supported length.");
		return value;
	}

	public bool TryReadSequence(string number, string year, out long sequence)
	{
		sequence = 0;
		var prefix = NumberPrefix(year); var suffix = NumberSuffix;
		if (!number.StartsWith(prefix, StringComparison.Ordinal) || !number.EndsWith(suffix, StringComparison.Ordinal) ||
			number.Length <= prefix.Length + suffix.Length) return false;
		var digits = number.Substring(prefix.Length, number.Length - prefix.Length - suffix.Length);
		return digits.All(char.IsAsciiDigit) && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
	}
}
