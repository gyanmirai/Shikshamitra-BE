using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPlaybookValidator
{
	public static void NormalizeAndValidate(AdmissionCrmPlaybookUpsertDto input)
	{
		input.Name = Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name), AdmissionCrmConsts.MaxTemplateNameLength).Trim();
		AdmissionCrmPlaybookApplicability.Normalize(input.Applicability);
		var specificity = AdmissionCrmPlaybookApplicability.Specificity(input.Applicability);
		if (input.IsFallback && specificity != 0) throw new BusinessException("AdmissionCrm:FallbackPlaybookCannotBeScoped");
		if (!input.IsFallback && specificity == 0) throw new BusinessException("AdmissionCrm:ScopedPlaybookApplicabilityRequired");
		AdmissionCrmPlaybookStructureValidator.Validate(input.Stages, input.Rounds, input.SeatPlans);
		AdmissionCrmPlaybookExamValidator.Validate(input.ExamDefinitions, input.Rounds, input.SlotTemplates, input.AssignmentRules);
	}
}
