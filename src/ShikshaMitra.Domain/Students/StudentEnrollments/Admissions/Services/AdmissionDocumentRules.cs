using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionDocumentRules
{
	public static bool HasCollectionEvidence(AdmissionDocumentEvidence evidence, string mode) => mode switch
	{
		"upload" => evidence.BlobName != null,
		"original" => evidence.OriginalSeen,
		"either" => evidence.BlobName != null || evidence.OriginalSeen,
		"both" => evidence.BlobName != null && evidence.OriginalSeen,
		_ => false
	};
	public static string ConditionStatus(AdmissionDocumentRequirement requirement, IReadOnlyDictionary<string, string> values)
	{
		if (requirement.Requirement != "conditional") return "notConditional";
		if (AdmissionDocumentCondition.GetFields(requirement.ConditionJson).Any(field => !values.ContainsKey(field))) return "undetermined";
		return new AdmissionWorkflowConditionEvaluator().Evaluate(requirement.ConditionJson, values) ? "matched" : "notMatched";
	}
	public static string Status(AdmissionDocumentEvidence? evidence, IEnumerable<AdmissionDocumentReview> reviews) =>
		evidence == null ? "missing" : evidence.Kind == "waiver" ? "waived" :
		reviews.Where(review => review.EvidenceId == evidence.Id).OrderByDescending(review => review.ReviewedAt).FirstOrDefault()?.Decision ?? "pendingReview";
}
