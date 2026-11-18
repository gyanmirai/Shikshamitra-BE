using System;
using System.Security.Cryptography;
using System.Text;
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
	private Task<StudentIdentifierSeries?> GetConfiguredSeriesAsync(
		OnboardingRuleConfiguration configuration, StudentOnboardingCase item, bool persist)
		// Numbering coverage is the academic path, independent of legacy readiness profile IDs.
		=> GrNumbers.GetSeriesAsync(configuration, configuration.ResolveRule("grNumber", item, DateTime.UtcNow),
			item.AcademicYearId, persist);

	private StudentGrNumberAllocator GrNumbers => LazyServiceProvider.LazyGetRequiredService<StudentGrNumberAllocator>();
}
