using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionReviewGraphRules
{
	public static List<AdmissionWorkflowValidationIssue> Validate(IReadOnlyCollection<AdmissionWorkflowStageDefinition> stages,
		IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> links)
	{
		var issues = new List<AdmissionWorkflowValidationIssue>(); var managed = 0;
		foreach (var target in stages.Where(item => item.StageType == AdmissionWorkflowStageType.Review))
		{
			try
			{
				var policy = AdmissionReviewPolicy.Read(target.ConfigurationJson); if (policy == null) continue;
				if (++managed > 1) throw new ArgumentException("Use one managed Review stage for this application decision workflow.");
				foreach (var prerequisite in policy.Prerequisites)
				{
					var source = stages.SingleOrDefault(item => item.Code.Equals(prerequisite.SourceStageCode, StringComparison.OrdinalIgnoreCase));
					var entry = stages.SingleOrDefault(item => item.IsEntryStage);
					if (source == null || !Matches(source, prerequisite.Kind) || source.Id == target.Id || !CanReach(source.Id, target.Id, links) ||
						entry != null && CanReach(entry.Id, target.Id, links, source.Id))
						throw new ArgumentException($"{prerequisite.SourceStageCode}: select an earlier managed {prerequisite.Kind} stage required on every path to Review.");
				}
			}
			catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
			{ issues.Add(new(AdmissionReviewErrorCodes.InvalidPolicy, exception.Message, StageId: target.Id)); }
		}
		return issues;
	}
	public static bool Matches(AdmissionWorkflowStageDefinition stage, string kind) => kind switch
	{
		"assessment" => stage.StageType == AdmissionWorkflowStageType.Assessment && AdmissionAssessmentPolicy.Read(stage.ConfigurationJson) != null,
		"documents" => stage.StageType == AdmissionWorkflowStageType.DocumentVerification && AdmissionDocumentPolicy.Read(stage.ConfigurationJson) != null,
		"eligibility" => stage.StageType == AdmissionWorkflowStageType.Eligibility && AdmissionEligibilityPolicy.Read(stage.ConfigurationJson) != null,
		_ => false
	};
	private static bool CanReach(Guid start, Guid target, IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> links, Guid? excluded = null)
	{
		var seen = new HashSet<Guid>(); var pending = new Queue<Guid>(); pending.Enqueue(start);
		while (pending.TryDequeue(out var current))
		{
			if (current == excluded || !seen.Add(current)) continue;
			if (current == target) return true;
			foreach (var link in links.Where(item => item.FromStageId == current)) pending.Enqueue(link.ToStageId);
		}
		return false;
	}
}
