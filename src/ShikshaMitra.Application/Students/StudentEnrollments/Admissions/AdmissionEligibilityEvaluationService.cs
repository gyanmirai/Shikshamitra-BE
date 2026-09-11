using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionEligibilityCheck(string Outcome, string Fingerprint, string PolicyJson, string FactsJson,
	List<AdmissionEligibilityCriterionResult> Criteria);

[RemoteService(false)]
public class AdmissionEligibilityEvaluationService(IRepository<AdmissionApplicant, Guid> applicants, IStudentRepository students,
	AdmissionEligibilityAnswerService answers, AdmissionEligibilitySourceStageService sources, AdmissionAssessmentRuntimeService assessments,
	AdmissionDocumentEvaluationService documents, IRepository<AdmissionWorkflowStageDefinition, Guid> definitions) : ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	public async Task<AdmissionEligibilityCheck> CheckAsync(AdmissionEligibilityContext context)
	{
		var policy = context.Policy ?? throw new BusinessException(AdmissionEligibilityErrorCodes.InvalidPolicy);
		var rows = new List<AdmissionEligibilityCriterionResult>();
		var snapshots = new SortedDictionary<string, string>(StringComparer.Ordinal);
		foreach (var criterion in policy.Criteria)
		{
			var (row, snapshot) = criterion.Kind switch
			{
				"age" => await AgeAsync(context, criterion),
				"application" or "inquiry" => await AnswersAsync(context, criterion),
				_ => await UpstreamAsync(context, criterion)
			};
			rows.Add(row); snapshots[criterion.Code] = snapshot;
		}
		var policyJson = JsonSerializer.Serialize(new { eligibility = policy }, JsonOptions);
		var factsJson = JsonSerializer.Serialize(snapshots, JsonOptions);
		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(policyJson + "\n" + factsJson)));
		return new(AdmissionEligibilityRules.Outcome(rows), fingerprint, policyJson, factsJson, rows);
	}
	private async Task<(AdmissionEligibilityCriterionResult, string)> AgeAsync(AdmissionEligibilityContext context, AdmissionEligibilityCriterion criterion)
	{
		var applicant = await applicants.GetAsync(context.Application.ApplicantId);
		DateOnly? birth = applicant.DateOfBirth.HasValue ? DateOnly.FromDateTime(applicant.DateOfBirth.Value) : null;
		var source = "Applicant date of birth";
		var identity = $"applicant:{applicant.Id}";
		if (applicant.ExistingStudentId.HasValue)
		{
			var student = await students.GetRegistrationAggregateAsync(applicant.ExistingStudentId.Value);
			var recorded = student?.AdditionalDetail?.DateOfBirth;
			birth = recorded.HasValue ? DateOnly.FromDateTime(recorded.Value.Date) : null;
			source = "Linked student date of birth"; identity = $"student:{applicant.ExistingStudentId}";
		}
		var cutoff = DateOnly.ParseExact(criterion.CutoffDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
		var outcome = AdmissionEligibilityRules.AgeOutcome(birth, cutoff, criterion.MinAgeMonths, criterion.MaxAgeMonths);
		var expected = $"{criterion.MinAgeMonths?.ToString() ?? "0"} to {criterion.MaxAgeMonths?.ToString() ?? "unlimited"} completed months on {cutoff:yyyy-MM-dd}";
		var birthValue = birth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var age = AdmissionEligibilityRules.CompletedAgeMonths(birth, cutoff);
		var actual = age.HasValue ? $"{age.Value} completed months; DOB {birthValue}" : birthValue == null ? "Date of birth unavailable" : $"DOB {birthValue} is after the cutoff";
		return (Row(criterion, outcome, expected, actual, source), $"{identity}|{birthValue}");
	}
	private async Task<(AdmissionEligibilityCriterionResult, string)> AnswersAsync(AdmissionEligibilityContext context, AdmissionEligibilityCriterion criterion)
	{
		var facts = await answers.ReadAsync(context.Application, criterion);
		var outcome = AdmissionEligibilityRules.ConditionOutcome(criterion.ConditionJson!, facts.Values, facts.Types);
		var fields = AdmissionDocumentCondition.GetFields(criterion.ConditionJson).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		var actual = string.Join("; ", fields.Select(field => $"{facts.Labels.GetValueOrDefault(field, field)}: {(facts.Values.TryGetValue(field, out var value) ? string.IsNullOrWhiteSpace(value) ? "(blank)" : value : "unavailable")}"));
		var snapshot = JsonSerializer.Serialize(new { facts.Identity, Values = new SortedDictionary<string, string>(facts.Values, StringComparer.OrdinalIgnoreCase) }, JsonOptions);
		return (Row(criterion, outcome, AdmissionEligibilityConditionDescription.Describe(criterion.ConditionJson!, facts.Labels), actual, facts.Source), snapshot);
	}
	private async Task<(AdmissionEligibilityCriterionResult, string)> UpstreamAsync(AdmissionEligibilityContext context, AdmissionEligibilityCriterion criterion)
	{
		var stage = await sources.FindCompletedAsync(context, criterion);
		var expected = criterion.Kind == "documents" ? "Required documents verified" : $"Assessment {criterion.RequiredOutcome ?? "passed"}{(criterion.MinimumPercentage.HasValue ? $", at least {criterion.MinimumPercentage}%" : "")}";
		if (stage == null) return (Row(criterion, "unknown", expected, "The selected source stage is not completed", criterion.SourceStageCode!), "unavailable-stage");
		if (criterion.Kind == "assessment")
		{
			var attempt = await assessments.GetLatestAsync(context.Application.Id, stage.Id);
			var policy = attempt == null ? null : AdmissionAssessmentPolicy.Read(attempt.PolicySnapshotJson);
			var complete = attempt?.Status == "completed" && attempt.Outcome is "passed" or "failed" or "waived" or "absent";
			var percentage = policy?.ScoringEnabled == true && attempt?.Marks != null && policy.TotalMarks > 0 ? attempt.Marks * 100 / policy.TotalMarks : null;
			var outcome = !complete || criterion.MinimumPercentage.HasValue && !percentage.HasValue ? "unknown" :
				attempt!.Outcome == (criterion.RequiredOutcome ?? "passed") && (!criterion.MinimumPercentage.HasValue || percentage >= criterion.MinimumPercentage) ? "passed" : "failed";
			var actual = attempt == null ? "Result unavailable" : $"{attempt.Outcome}{(percentage.HasValue ? $" ({percentage:0.##}%)" : "")}";
			return (Row(criterion, outcome, expected, actual, stage.Name), JsonSerializer.Serialize(new { stage.Id, AttemptId = attempt?.Id, attempt?.Status,
				attempt?.Outcome, attempt?.Marks, attempt?.ReviewedBy, ReviewedAtUtcTicks = attempt?.ReviewedAt?.ToUniversalTime().Ticks, TotalMarks = policy?.TotalMarks }, JsonOptions));
		}
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		var documentPolicy = AdmissionDocumentAccessService.ReadPolicy(definition);
		if (documentPolicy == null) return (Row(criterion, "unknown", expected, "Managed document settings unavailable", stage.Name), "unavailable-policy");
		var result = await documents.EvaluateAsync(context.Application, stage.Id, documentPolicy);
		var state = result.Requirements.Any(item => item.ConditionStatus == "undetermined") ? "unknown" : result.Blockers.Count == 0 ? "passed" : "failed";
		var snapshotJson = JsonSerializer.Serialize(new { stage.Id, Rows = result.Requirements.Select(item => new { item.Requirement.Code,
			item.ConditionStatus, item.IsRequired, item.Status, EvidenceId = item.Latest?.Id,
			ReviewIds = item.Reviews.Where(review => review.EvidenceId == item.Latest?.Id).Select(review => review.Id).OrderBy(id => id).ToList() }) }, JsonOptions);
		return (Row(criterion, state, expected, result.Blockers.Count == 0 ? "Required evidence verified" : string.Join(" ", result.Blockers), stage.Name), snapshotJson);
	}
	private static AdmissionEligibilityCriterionResult Row(AdmissionEligibilityCriterion criterion, string outcome, string expected, string actual, string source) =>
		new(criterion.Code, criterion.Name, criterion.Kind, outcome, expected, actual, source,
			outcome == "passed" ? "Meets this criterion." : outcome == "unknown" ? "Complete or correct the source information, then check eligibility again." : criterion.FailureMessage ?? "Does not meet this configured criterion.");
}
