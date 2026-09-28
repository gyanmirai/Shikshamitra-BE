using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionDocumentRequirementEvaluation(AdmissionDocumentRequirement Requirement, string ConditionStatus,
	bool IsRequired, string Status, List<AdmissionDocumentEvidence> Evidence, List<AdmissionDocumentReview> Reviews, List<string> Blockers)
{
	public AdmissionDocumentEvidence? Latest => Evidence.LastOrDefault();
}
public sealed record AdmissionDocumentEvaluation(List<AdmissionDocumentRequirementEvaluation> Requirements,
	IReadOnlyDictionary<string, string> Values)
{
	public List<string> Blockers => Requirements.SelectMany(item => item.Blockers).ToList();
	public Dictionary<string, string> Facts => new(StringComparer.OrdinalIgnoreCase)
	{
		["documents.status"] = Blockers.Count == 0 ? "verified" : "pending",
		["documents.missingCount"] = Requirements.Count(item => item.IsRequired && item.Status == "missing").ToString(),
		["documents.pendingCount"] = Requirements.Count(item => item.Status == "pendingReview").ToString(),
		["documents.rejectedCount"] = Requirements.Count(item => item.Status == "rejected").ToString(),
		["documents.waivedCount"] = Requirements.Count(item => item.Status == "waived").ToString(),
		["documents.verifiedCount"] = Requirements.Count(item => item.Status == "accepted").ToString()
	};
	public void EnsureComplete()
	{
		if (Blockers.Count > 0) throw new BusinessException(AdmissionDocumentErrorCodes.EvidenceRequired, string.Join(" ", Blockers));
	}
}

[RemoteService(false)]
public class AdmissionDocumentEvaluationService(IRepository<AdmissionDocumentEvidence, Guid> evidence,
	IRepository<AdmissionDocumentReview, Guid> reviews, AdmissionWorkflowRuntimeValueService runtimeValues,
	IRepository<AdmissionOffering, Guid> offerings) : ITransientDependency
{
	public async Task<AdmissionDocumentEvaluation> EvaluateAsync(AdmissionApplication application, Guid stageId, AdmissionDocumentPolicy policy)
	{
		var values = await runtimeValues.ReadAsync(application, protectManagedFacts: true);
		var offering = policy.Requirements.Any(item => item.StandardIds != null)
			? await offerings.FindAsync(application.AdmissionOfferingId) : null;
		var rows = await evidence.GetListAsync(item => item.ApplicationId == application.Id && item.StageInstanceId == stageId);
		var decisions = await reviews.GetListAsync(item => item.ApplicationId == application.Id && item.StageInstanceId == stageId);
		return new(policy.Requirements.Select(requirement => Evaluate(requirement, policy, values, rows, decisions, offering?.StandardId)).ToList(), values);
	}

	private static AdmissionDocumentRequirementEvaluation Evaluate(AdmissionDocumentRequirement requirement, AdmissionDocumentPolicy policy,
		IReadOnlyDictionary<string, string> values, List<AdmissionDocumentEvidence> rows, List<AdmissionDocumentReview> decisions, Guid? standardId)
	{
		var versions = rows.Where(item => item.RequirementCode == requirement.Code).OrderBy(item => item.VersionNumber).ToList();
		var condition = AdmissionDocumentRules.ConditionStatus(requirement, values, standardId);
		var required = condition != "notMatched" && requirement.Requirement is "required" or "conditional";
		var latest = versions.LastOrDefault();
		var status = condition == "notMatched" ? "notRequired" : AdmissionDocumentRules.Status(latest, decisions);
		var blockers = new List<string>();
		if (condition == "undetermined") blockers.Add($"{requirement.Name}: complete the application information needed to determine this requirement.");
		else if (required && !(status == "waived" && policy.AllowWaivers) &&
			!(status == "accepted" && latest != null && AdmissionDocumentRules.HasCollectionEvidence(latest, requirement.CollectionMode)))
			blockers.Add($"{requirement.Name}: {status switch { "missing" => "provide the required evidence", "rejected" => "replace the rejected evidence and request review", "pendingReview" => "complete the evidence collection and obtain acceptance", _ => "obtain accepted evidence or an authorized waiver" }}.");
		return new(requirement, condition, required, status, versions, decisions.Where(item => versions.Any(version => version.Id == item.EvidenceId)).ToList(), blockers);
	}
}
