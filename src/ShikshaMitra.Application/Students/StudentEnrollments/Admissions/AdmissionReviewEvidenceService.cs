using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionReviewEvidence(string Fingerprint, string PolicyJson, List<AdmissionReviewEvidenceDto> Items)
{
	public bool IsKnown => Items.All(item => !item.IsStale && item.Status is not ("missing" or "pending" or "notChecked"));
	public bool Passed => IsKnown && Items.All(item => item.Passed);
}

[RemoteService(false)]
public class AdmissionReviewEvidenceService(IRepository<AdmissionApplicant, Guid> applicants, IStudentRepository students,
	IRepository<AdmissionFormInstance, Guid> forms, IRepository<AdmissionFormFieldValue, Guid> values,
	IRepository<AdmissionFormSubmission, Guid> submissions, IRepository<AdmissionInquiry, Guid> inquiries,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions, IRepository<AdmissionWorkflowTransitionDefinition, Guid> links,
	IRepository<AdmissionApplicationStageInstance, Guid> stages, AdmissionAssessmentRuntimeService assessments,
	AdmissionDocumentEvaluationService documents, AdmissionEligibilityAccessService eligibilityAccess,
	AdmissionEligibilityStateService eligibilityStates) : ITransientDependency
{
	public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	public async Task<AdmissionReviewEvidence> ReadAsync(AdmissionReviewContext context)
	{
		var policy = context.Policy ?? throw new BusinessException(AdmissionReviewErrorCodes.InvalidPolicy);
		var sources = await definitions.GetListAsync(item => item.WorkflowDefinitionId == context.Application.WorkflowDefinitionId);
		if (AdmissionReviewGraphRules.Validate(sources, await links.GetListAsync(item => item.WorkflowDefinitionId == context.Application.WorkflowDefinitionId))
			.Any(issue => issue.StageId == context.Definition.Id)) throw new BusinessException(AdmissionReviewErrorCodes.InvalidPolicy);
		var runtime = await stages.GetListAsync(item => item.WorkflowInstanceId == context.Stage.WorkflowInstanceId);
		var snapshots = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["application"] = await ApplicantSnapshotAsync(context.Application) };
		var items = new List<AdmissionReviewEvidenceDto>();
		foreach (var prerequisite in policy.Prerequisites)
		{
			var definition = sources.Single(item => item.Code.Equals(prerequisite.SourceStageCode, StringComparison.OrdinalIgnoreCase));
			var stage = runtime.SingleOrDefault(item => item.DefinitionStageId == definition.Id);
			var item = new AdmissionReviewEvidenceDto { Kind = prerequisite.Kind, SourceStageCode = definition.Code,
				SourceStageName = definition.Name, StageId = stage?.Id, Status = "missing", Summary = "Complete this prerequisite stage first." };
			var snapshot = "missing";
			if (stage?.Status == AdmissionRuntimeStageStatus.Completed)
				(snapshot, item.Status, item.Passed, item.IsStale, item.Summary) = await ReadSourceAsync(context, definition, stage, prerequisite.Kind);
			snapshots[definition.Code] = definition.ConfigurationJson + "|" + stage?.Id + "|" + stage?.Status + "|" + snapshot;
			items.Add(item);
		}
		var policyJson = JsonSerializer.Serialize(new { review = policy }, JsonOptions);
		var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(policyJson + "\n" + JsonSerializer.Serialize(snapshots, JsonOptions))));
		return new(hash, policyJson, items);
	}
	private async Task<string> ApplicantSnapshotAsync(AdmissionApplication application)
	{
		var applicant = await applicants.GetAsync(application.ApplicantId);
		var student = applicant.ExistingStudentId.HasValue ? await students.GetRegistrationAggregateAsync(applicant.ExistingStudentId.Value) : null;
		if (applicant.ExistingStudentId.HasValue && student == null) throw new BusinessException(AdmissionReviewErrorCodes.EvidenceRequired);
		var identity = student == null ? JsonSerializer.Serialize(new { Source = applicant.Id, applicant.FirstName, applicant.MiddleName, applicant.LastName,
			applicant.DateOfBirth, applicant.Gender }, JsonOptions) : JsonSerializer.Serialize(new { Source = student.Id, student.FirstName,
			student.MiddleName, student.LastName, student.AdditionalDetail?.DateOfBirth, student.AdditionalDetail?.Gender }, JsonOptions);
		var form = await forms.FindAsync(item => item.ApplicationId == application.Id && item.FormDefinitionId == application.FormDefinitionId);
		var formValues = form == null ? [] : await values.GetListAsync(item => item.FormInstanceId == form.Id);
		var submitted = form == null ? [] : await submissions.GetListAsync(item => item.FormInstanceId == form.Id);
		var inquiry = application.InquiryId.HasValue ? await inquiries.FindAsync(application.InquiryId.Value) : null;
		return JsonSerializer.Serialize(new { identity, FormId = form?.Id, form?.FormVersion,
			Values = formValues.OrderBy(item => item.FieldCode, StringComparer.Ordinal).Select(item => new { item.FieldCode, item.ValueJson }),
			Submissions = submitted.Select(item => item.Id).Order(), Inquiry = inquiry?.FormResponseSnapshotJson }, JsonOptions);
	}
	private async Task<(string Snapshot, string Status, bool Passed, bool Stale, string Summary)> ReadSourceAsync(
		AdmissionReviewContext context, AdmissionWorkflowStageDefinition definition, AdmissionApplicationStageInstance stage, string kind)
	{
		if (kind == "assessment")
		{
			var attempt = await assessments.GetLatestAsync(context.Application.Id, stage.Id);
			var ready = attempt?.Status == "completed" && attempt.Outcome is "passed" or "failed" or "waived" or "absent";
			return (JsonSerializer.Serialize(attempt == null ? null : new { attempt.Id, attempt.Outcome, attempt.Status, attempt.Marks, attempt.Attendance,
				attempt.ReviewedBy, attempt.ReviewedAt }, JsonOptions), ready ? attempt!.Outcome! : "pending", ready && attempt!.Outcome is "passed" or "waived", false,
				ready ? $"Assessment result: {attempt!.Outcome}; marks: {attempt.Marks?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "not scored"}." : "Complete and review this assessment result.");
		}
		if (kind == "documents")
		{
			var evaluation = await documents.EvaluateAsync(context.Application, stage.Id, AdmissionDocumentAccessService.ReadPolicy(definition)!);
			var known = evaluation.Requirements.All(item => item.ConditionStatus != "undetermined" && (!item.IsRequired || item.Status is "accepted" or "waived" or "rejected"));
			var snapshot = JsonSerializer.Serialize(evaluation.Requirements.Select(item => new { item.Requirement.Code,
				Evidence = item.Evidence.Select(row => new { row.Id, row.VersionNumber, row.Sha256Hash, row.OriginalSeen, row.OriginalSeenBy }),
				Reviews = item.Reviews.OrderBy(row => row.Id).Select(row => new { row.Id, row.Decision, row.ReviewedBy }) }), JsonOptions);
			return (snapshot, evaluation.Blockers.Count == 0 ? "verified" : known ? "rejected" : "pending", evaluation.Blockers.Count == 0, false,
				evaluation.Blockers.Count == 0 ? "Required document evidence is verified." : string.Join(" ", evaluation.Blockers));
		}
		var state = await eligibilityStates.ReadAsync(await eligibilityAccess.GetContextAsync(context.Application.Id, stage.Id));
		return (JsonSerializer.Serialize(new { EvaluationId = state.Latest?.Id, Fingerprint = state.Check?.Fingerprint, state.Status,
			Decisions = state.Exceptions.Select(item => item.Id).Order() }, JsonOptions), state.Latest == null ? "notChecked" : state.Status == "needsInformation" ? "pending" : state.Status,
			state.Passed, state.IsStale, state.IsStale ? "Eligibility information changed; check it again." : $"Eligibility result: {state.Status}.");
	}
}
