using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionInquiryGraphRules
{
	public static List<AdmissionWorkflowValidationIssue> Validate(
		IReadOnlyCollection<AdmissionWorkflowStageDefinition> stages)
	{
		var issues = new List<AdmissionWorkflowValidationIssue>();
		var managed = 0;
		foreach (var stage in stages.Where(item => item.StageType == AdmissionWorkflowStageType.Inquiry))
		{
			try
			{
				var policy = AdmissionInquiryPolicy.Read(stage.ConfigurationJson);
				if (policy == null) continue;
				if (++managed > 1) throw new ArgumentException("Use one managed Inquiry stage per workflow.");
				if (!stage.IsEntryStage) throw new ArgumentException("The managed Inquiry must be the workflow entry stage.");
				if (stage.AllowManualSkip) throw new ArgumentException("Managed Inquiry cannot allow manual skip.");
			}
			catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
			{
				issues.Add(new AdmissionWorkflowValidationIssue(
					AdmissionInquiryManagementErrorCodes.InvalidPolicy, exception.Message,
					StageId: stage.Id));
			}
		}
		return issues;
	}
}
