using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionEligibilityOfferingValidator(IRepository<AdmissionWorkflowStageDefinition, Guid> stages)
	: ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionOfferingValidationError>> ValidateAsync(
		Guid? workflowId, Guid? applicationFormId, Guid? inquiryFormId, AdmissionEntryMode entryMode)
	{
		if (!workflowId.HasValue) return [];
		var definitions = await stages.GetListAsync(stage => stage.WorkflowDefinitionId == workflowId.Value && stage.StageType == AdmissionWorkflowStageType.Eligibility);
		return Validate(definitions, applicationFormId, inquiryFormId, entryMode);
	}

	public async Task<Dictionary<Guid, List<AdmissionOfferingValidationError>>> GetErrorsAsync(IQueryable<AdmissionOffering> offerings)
	{
		var references = await AsyncExecuter.ToListAsync(offerings.Select(offering => new
		{
			offering.Id, offering.WorkflowDefinitionId, offering.ApplicationFormDefinitionId, offering.InquiryFormDefinitionId,
			offering.EntryMode
		}));
		var workflowIds = references.Where(item => item.WorkflowDefinitionId.HasValue).Select(item => item.WorkflowDefinitionId!.Value).Distinct().ToList();
		var definitions = workflowIds.Count == 0 ? [] : await stages.GetListAsync(stage => workflowIds.Contains(stage.WorkflowDefinitionId) &&
			(stage.StageType == AdmissionWorkflowStageType.Eligibility || stage.StageType == AdmissionWorkflowStageType.Inquiry));
		var grouped = definitions.ToLookup(stage => stage.WorkflowDefinitionId);
		var errors = new Dictionary<Guid, List<AdmissionOfferingValidationError>>();
		foreach (var reference in references.Where(item => item.WorkflowDefinitionId.HasValue))
		{
			var workflowStages = grouped[reference.WorkflowDefinitionId!.Value].ToList();
			var validation = Validate(workflowStages.Where(stage => stage.StageType == AdmissionWorkflowStageType.Eligibility),
				reference.ApplicationFormDefinitionId, reference.InquiryFormDefinitionId, reference.EntryMode);
			if ((reference.EntryMode is AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both) &&
				workflowStages.Any(stage => stage.StageType == AdmissionWorkflowStageType.Inquiry))
				validation.Add(new(AdmissionOfferingValidationCodes.DirectWorkflowContainsInquiry,
					"A direct Registration Offering must use a Workflow without an Inquiry stage."));
			if (validation.Count > 0) errors.Add(reference.Id, validation);
		}
		return errors;
	}

	private static List<AdmissionOfferingValidationError> Validate(
		IEnumerable<AdmissionWorkflowStageDefinition> definitions,
		Guid? applicationFormId, Guid? inquiryFormId, AdmissionEntryMode entryMode)
	{
		var errors = new List<AdmissionOfferingValidationError>();
		foreach (var stage in definitions)
		{
			try
			{
				var policy = AdmissionEligibilityAccessService.ReadPolicy(stage);
				if (policy == null) continue;
				foreach (var criterion in policy.Criteria.Where(item => item.Kind is "application" or "inquiry"))
				{
					if ((entryMode is AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both) && criterion.Kind == "inquiry")
					{
						errors.Add(new(AdmissionOfferingValidationCodes.EligibilityFormMismatch,
							$"{stage.Name} / {criterion.Name}: a direct Registration Offering cannot depend on Inquiry answers."));
						continue;
					}
					// Runtime captures these exact offering forms; stage form references are not fallbacks.
					var selected = criterion.Kind == "application" ? applicationFormId : inquiryFormId;
					// The existing missing-form checks allow unfinished inactive drafts and block activation.
					if (!selected.HasValue || selected == criterion.FormDefinitionId) continue;
					errors.Add(new(AdmissionOfferingValidationCodes.EligibilityFormMismatch,
						$"{stage.Name} / {criterion.Name}: select the same {criterion.Kind} form version in this Offering and the eligibility criterion."));
				}
			}
			catch (BusinessException)
			{
				errors.Add(new(AdmissionOfferingValidationCodes.EligibilityFormMismatch,
					$"{stage.Name}: repair the eligibility settings before using this Workflow in an Offering."));
			}
		}
		return errors;
	}
}
