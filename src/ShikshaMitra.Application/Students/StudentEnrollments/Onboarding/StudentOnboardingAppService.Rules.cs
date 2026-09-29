using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private static string ConfigurationHash(OnboardingRuleConfiguration configuration) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration.Root.GetRawText())));

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
		var scope = OnboardingRuleConfiguration.Text(value, "sequenceScope") == "academic-year" ||
			OnboardingRuleConfiguration.Text(value, "resetPolicy") == "academic-year" ? item.AcademicYearId.ToString("N") : "school";
		// A stable rule identity preserves its counter when labels, coverage, or academic years change.
		var key = $"{CurrentTenant.Id}:onboarding-gr:{OnboardingRuleConfiguration.Text(value, "id")}:{scope}";
		var seriesId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
		var year = await academicYearRepository.GetAsync(item.AcademicYearId);
		var yearShort = string.Join("-", Regex.Matches(year.Name, @"\d{2,4}").Select(m => m.Value[^2..]));
		var pattern = OnboardingRuleConfiguration.Text(value, "formatPattern", "{PREFIX}{SEQUENCE}{SUFFIX}")
			.Replace("{PREFIX}", OnboardingRuleConfiguration.Text(value, "prefix"))
			.Replace("{SUFFIX}", OnboardingRuleConfiguration.Text(value, "suffix"))
			.Replace("{YEAR_SHORT}", yearShort).Replace("{YEAR}", year.Name);
		var parts = pattern.Split("{SEQUENCE}");
		if (parts.Length != 2 || parts.Any(p => p.Contains('{') || p.Contains('}')))
			throw OnboardingRuleConfiguration.Invalid("The GR format must contain one {SEQUENCE}; supported tokens are PREFIX, SUFFIX, YEAR and YEAR_SHORT.");
		var padding = (int)OnboardingRuleConfiguration.Number(value, "padding", 6);
		var start = OnboardingRuleConfiguration.Number(value, "startingNumber", 1);
		var maximum = OnboardingRuleConfiguration.Number(value, "maximumNumber");
		var name = OnboardingRuleConfiguration.Text(value, "name", "GR numbering rule");
		var series = await seriesRepository.FindAsync(seriesId);
		if (maximum > 0 && Math.Max(series?.NextSequence ?? start, start) > maximum)
			throw new Volo.Abp.BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
		var isNew = series == null;
		if (!persist && series != null)
			return new StudentIdentifierSeries(seriesId, CurrentTenant.Id, item.AcademicYearId, null,
				StudentIdentifierType.GrNumber, series.Code, name, parts[0], parts[1], padding,
				Math.Max(series.NextSequence, start), maximum > 0 ? maximum : null, DateTime.UnixEpoch, null);
		series ??= new StudentIdentifierSeries(seriesId, CurrentTenant.Id, item.AcademicYearId, null,
			StudentIdentifierType.GrNumber, $"RULE-{seriesId:N}", name, parts[0], parts[1], padding, start,
			maximum > 0 ? maximum : null, DateTime.UnixEpoch, null);
		series.ApplyRule(name, parts[0], parts[1], padding, start, maximum > 0 ? maximum : null);
		series.EnsureAvailable(DateTime.UtcNow);
		if (persist)
		{
			if (isNew) await seriesRepository.InsertAsync(series, false);
			else await seriesRepository.UpdateAsync(series, false);
		}
		return series;
	}
}
