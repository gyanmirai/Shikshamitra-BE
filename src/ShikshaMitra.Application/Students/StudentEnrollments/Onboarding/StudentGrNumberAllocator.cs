using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[RemoteService(false)]
public class StudentGrNumberAllocator(IRepository<StudentIdentifierSeries, Guid> seriesRepository,
	IRepository<StudentIdentifierAllocation, Guid> allocations, IStudentRepository students,
	IRepository<AcademicYear, Guid> years, IDataFilter<ISoftDelete> softDelete) : ApplicationService, ITransientDependency
{
	internal async Task<StudentIdentifierSeries?> GetSeriesAsync(OnboardingRuleConfiguration configuration,
		JsonElement? rule, Guid academicYearId, bool persist)
	{
		if (!rule.HasValue) return null;
		var value = rule.Value;
		var seriesId = GetSeriesId(configuration, value, academicYearId);
		var year = await years.GetAsync(academicYearId);
		var (prefix, suffix) = Format(value, year.Name);
		if (prefix.Trim().Length > 40 || suffix.Trim().Length > 40)
			throw OnboardingRuleConfiguration.Invalid("The rendered GR prefix and suffix must each fit within 40 characters.");
		var padding = (int)OnboardingRuleConfiguration.Number(value, "padding", 6);
		var start = OnboardingRuleConfiguration.Number(value, "startingNumber", 1);
		var maximum = OnboardingRuleConfiguration.Number(value, "maximumNumber");
		var name = OnboardingRuleConfiguration.Text(value, "name", "GR numbering rule");
		var series = await seriesRepository.FindAsync(seriesId);
		var next = Math.Max(series?.NextSequence ?? start, start);
		if (maximum > 0 && next > maximum)
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable)
				.WithData("ReasonCode", "SeriesLimitReached")
				.WithData("MaximumNumber", maximum).WithData("NextSequence", next);
		var isNew = series == null;
		if (!persist && series != null)
			return new StudentIdentifierSeries(seriesId, CurrentTenant.Id, academicYearId, null,
				StudentIdentifierType.GrNumber, series.Code, name, prefix, suffix, padding,
				next, maximum > 0 ? maximum : null, DateTime.UnixEpoch, null);
		series ??= new StudentIdentifierSeries(seriesId, CurrentTenant.Id, academicYearId, null,
			StudentIdentifierType.GrNumber, $"RULE-{seriesId:N}", name, prefix, suffix, padding, start,
			maximum > 0 ? maximum : null, DateTime.UnixEpoch, null);
		series.ApplyRule(name, prefix, suffix, padding, start, maximum > 0 ? maximum : null);
		series.EnsureAvailable(DateTime.UtcNow);
		if (persist)
		{
			if (isNew) await seriesRepository.InsertAsync(series, false);
			else await seriesRepository.UpdateAsync(series, false);
		}
		return series;
	}

	internal Guid GetSeriesId(OnboardingRuleConfiguration configuration, JsonElement value, Guid academicYearId)
	{
		var scope = OnboardingRuleConfiguration.Text(value, "sequenceScope") == "academic-year" ||
			OnboardingRuleConfiguration.Text(value, "resetPolicy") == "academic-year" ? academicYearId.ToString("N") : "school";
		// A stable rule identity preserves the counter across revisions and covered academic years.
		var ruleKey = OnboardingRuleConfiguration.Text(value, "id");
		var predecessor = OnboardingRuleConfiguration.Text(value, "previousVersionId");
		var visited = new HashSet<string> { ruleKey };
		while (!string.IsNullOrWhiteSpace(predecessor) && visited.Add(predecessor))
		{
			ruleKey = predecessor;
			var previous = configuration.Rules("grNumber").FirstOrDefault(r => OnboardingRuleConfiguration.Text(r, "id") == predecessor);
			predecessor = OnboardingRuleConfiguration.Text(previous, "previousVersionId");
		}
		var key = $"{CurrentTenant.Id}:onboarding-gr:{ruleKey}:{scope}";
		return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
	}

	internal static (string Prefix, string Suffix) Format(JsonElement value, string yearName)
	{
		var yearShort = string.Join("-", Regex.Matches(yearName, @"\d{2,4}").Select(m => m.Value[^2..]));
		var pattern = OnboardingRuleConfiguration.Text(value, "formatPattern", "{PREFIX}{SEQUENCE}{SUFFIX}")
			.Replace("{PREFIX}", OnboardingRuleConfiguration.Text(value, "prefix"))
			.Replace("{SUFFIX}", OnboardingRuleConfiguration.Text(value, "suffix"))
			.Replace("{YEAR_SHORT}", yearShort).Replace("{YEAR}", yearName);
		var parts = pattern.Split("{SEQUENCE}");
		if (parts.Length != 2 || parts.Any(p => p.Contains('{') || p.Contains('}')))
			throw OnboardingRuleConfiguration.Invalid("The GR format must contain one {SEQUENCE}; supported tokens are PREFIX, SUFFIX, YEAR and YEAR_SHORT.");
		return (parts[0], parts[1]);
	}

	internal async Task<bool> IsInUseAsync(string value, Guid studentId, Guid? allocationId, GrCollisionReadContext read)
	{
		// The unique indexes retain soft-deleted GR values as well as live rows.
		using var includeHistorical = softDelete.Disable();
		var numeric = int.TryParse(value, out var number) ? (int?)number : null;
		var reserved = (await allocations.GetQueryableAsync()).Where(a => a.TenantId == CurrentTenant.Id &&
			a.Id != allocationId && (a.IdentifierType == StudentIdentifierType.GrNumber ||
				a.IdentifierType == StudentIdentifierType.AdmissionNumber && a.Status != StudentIdentifierAllocationStatus.Released));
		var assigned = (await students.GetQueryableWithoutAutoIncludesAsync()).Where(s => s.TenantId == CurrentTenant.Id && s.Id != studentId);
		if (await AsyncExecuter.AnyAsync(reserved.Where(a => a.Value == value)) ||
			await AsyncExecuter.AnyAsync(assigned.Where(s => s.GrNumber == value || numeric.HasValue && s.GrNo == numeric))) return true;
		if (!numeric.HasValue) return false;
		// Keep indexed exact checks. Parse numeric-equivalence metadata only once per allocation operation.
		read.ReservedNumbers ??= NumericValues(await AsyncExecuter.ToListAsync(reserved.Select(a => a.Value)));
		if (read.ReservedNumbers.Contains(numeric.Value)) return true;
		read.AssignedNumbers ??= NumericValues(await AsyncExecuter.ToListAsync(assigned.Where(s => s.GrNumber != null).Select(s => s.GrNumber)));
		return read.AssignedNumbers.Contains(numeric.Value);
	}

	private static HashSet<int> NumericValues(IEnumerable<string?> values)
	{
		var numbers = new HashSet<int>();
		foreach (var value in values)
			if (int.TryParse(value, out var number)) numbers.Add(number);
		return numbers;
	}
}

internal sealed class GrCollisionReadContext
{
	internal HashSet<int>? ReservedNumbers { get; set; }
	internal HashSet<int>? AssignedNumbers { get; set; }
}
