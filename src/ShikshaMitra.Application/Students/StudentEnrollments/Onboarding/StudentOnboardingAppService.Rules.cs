using System;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private static string ConfigurationHash(OnboardingRuleConfiguration configuration, StudentOnboardingCase item) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration.Readiness(item).GetRawText())));

	private async Task InvalidateApprovalAsync(Guid caseId, string detail)
	{
		var approval = await stepRepository.FindAsync(s => s.OnboardingCaseId == caseId && s.Key == StudentOnboardingStepKeys.ApprovalReview);
		if (approval == null) return;
		approval.Reset(detail);
		await stepRepository.UpdateAsync(approval, false);
	}
	private async Task<StudentIdentifierSeries?> GetConfiguredSeriesAsync(
		OnboardingRuleConfiguration configuration, StudentOnboardingCase item, bool persist)
	{
		var rule = configuration.ResolveRule("grNumber", item, DateTime.UtcNow);
		if (!rule.HasValue) return null;
		var value = rule.Value;
		// Numbering coverage is the academic path. Readiness profile IDs are legacy UI
		// metadata and must not exclude newly added rules for the same admission route.
		var seriesId = ConfiguredGrSeriesId(configuration, value, item.AcademicYearId);
		var year = await academicYearRepository.GetAsync(item.AcademicYearId);
		var (prefix, suffix) = ConfiguredGrFormat(value, year.Name);
		var padding = (int)OnboardingRuleConfiguration.Number(value, "padding", 6);
		var start = OnboardingRuleConfiguration.Number(value, "startingNumber", 1);
		var maximum = OnboardingRuleConfiguration.Number(value, "maximumNumber");
		var name = OnboardingRuleConfiguration.Text(value, "name", "GR numbering rule");
		var series = await seriesRepository.FindAsync(seriesId);
		var next = Math.Max(series?.NextSequence ?? start, start);
		if (maximum > 0 && next > maximum)
			throw new Volo.Abp.BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable)
				.WithData("ReasonCode", "SeriesLimitReached")
				.WithData("MaximumNumber", maximum).WithData("NextSequence", next);
		var isNew = series == null;
		if (!persist && series != null)
			return new StudentIdentifierSeries(seriesId, CurrentTenant.Id, item.AcademicYearId, null,
				StudentIdentifierType.GrNumber, series.Code, name, prefix, suffix, padding,
				next, maximum > 0 ? maximum : null, DateTime.UnixEpoch, null);
		series ??= new StudentIdentifierSeries(seriesId, CurrentTenant.Id, item.AcademicYearId, null,
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

	private Guid ConfiguredGrSeriesId(OnboardingRuleConfiguration configuration, JsonElement value, Guid academicYearId)
	{
		var scope = OnboardingRuleConfiguration.Text(value, "sequenceScope") == "academic-year" ||
			OnboardingRuleConfiguration.Text(value, "resetPolicy") == "academic-year" ? academicYearId.ToString("N") : "school";
		// A stable rule identity preserves its counter when labels, coverage, or academic years change.
		var ruleKey = OnboardingRuleConfiguration.Text(value, "id");
		var predecessor = OnboardingRuleConfiguration.Text(value, "previousVersionId");
		var visited = new System.Collections.Generic.HashSet<string> { ruleKey };
		while (!string.IsNullOrWhiteSpace(predecessor) && visited.Add(predecessor))
		{
			ruleKey = predecessor;
			var previous = configuration.Rules("grNumber").FirstOrDefault(r => OnboardingRuleConfiguration.Text(r, "id") == predecessor);
			predecessor = OnboardingRuleConfiguration.Text(previous, "previousVersionId");
		}
		var key = $"{CurrentTenant.Id}:onboarding-gr:{ruleKey}:{scope}";
		return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
	}

	private static (string Prefix, string Suffix) ConfiguredGrFormat(JsonElement value, string yearName)
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
}
