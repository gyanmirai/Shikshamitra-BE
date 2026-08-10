using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmPolicyRuleEvaluator
{
	public static (AdmissionCrmPolicyEvaluationStatus Status, string Explanation) Evaluate(
		AdmissionInquiryLead lead,
		AdmissionCrmPolicyRuleDto rule,
		EvaluateAdmissionCrmPolicyRuleDto input)
	{
		if (!string.IsNullOrWhiteSpace(rule.DesiredClass) &&
			!string.Equals(rule.DesiredClass, lead.DesiredClassName, StringComparison.OrdinalIgnoreCase))
			return (AdmissionCrmPolicyEvaluationStatus.NotApplicable, "Rule does not apply to the applicant's desired class.");
		if (rule.Type == AdmissionCrmPolicyRuleType.AgeEligibility && rule.AgeCutoffDate.HasValue &&
			(rule.MinimumAgeYears.HasValue || rule.MaximumAgeYears.HasValue))
		{
			if (!lead.DateOfBirth.HasValue)
				return (AdmissionCrmPolicyEvaluationStatus.EvidencePending, "Verified date of birth is required.");
			var dateOfBirth = lead.DateOfBirth.Value.Date;
			var cutoff = rule.AgeCutoffDate.Value.Date;
			var age = cutoff.Year - dateOfBirth.Year -
				(dateOfBirth > cutoff.AddYears(-(cutoff.Year - dateOfBirth.Year)) ? 1 : 0);
			var satisfied = (!rule.MinimumAgeYears.HasValue || age >= rule.MinimumAgeYears) &&
				(!rule.MaximumAgeYears.HasValue || age <= rule.MaximumAgeYears);
			return (
				satisfied ? AdmissionCrmPolicyEvaluationStatus.Satisfied : AdmissionCrmPolicyEvaluationStatus.NotSatisfied,
				$"Applicant age is {age} years on {cutoff:yyyy-MM-dd}.");
		}
		if (input.ManualStatus.HasValue)
			return (input.ManualStatus.Value, input.Explanation ?? "Staff recorded a governed manual evaluation.");
		return (
			rule.ManualVerificationRequired
				? AdmissionCrmPolicyEvaluationStatus.ManualReviewRequired
				: AdmissionCrmPolicyEvaluationStatus.EvidencePending,
			input.Explanation ?? "Evidence or manual verification is required.");
	}

	public static AdmissionCrmPolicyRuleDto ApplyOverride(
		AdmissionCrmPolicyRuleDto source,
		IReadOnlyList<AdmissionCrmPolicyOverrideDto> overrides)
	{
		var value = AdmissionCrmPolicyMapper.Clone(source);
		var item = overrides.FirstOrDefault(overrideItem =>
			overrideItem.RuleCode.Equals(source.Code, StringComparison.OrdinalIgnoreCase));
		if (item == null) return value;
		value.Severity = item.Severity ?? value.Severity;
		value.AgeCutoffDate = item.AgeCutoffDate ?? value.AgeCutoffDate;
		value.MinimumAgeYears = item.MinimumAgeYears ?? value.MinimumAgeYears;
		value.MaximumAgeYears = item.MaximumAgeYears ?? value.MaximumAgeYears;
		value.Deadline = item.Deadline ?? value.Deadline;
		return value;
	}
}
