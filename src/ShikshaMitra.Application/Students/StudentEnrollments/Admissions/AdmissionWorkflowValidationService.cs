using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowValidationService(
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionFormSectionDefinition, Guid> formSectionRepository,
	AdmissionWorkflowGraphValidator graphValidator,
	AdmissionWorkflowDefinitionStore definitionStore,
		AdmissionDocumentAccessService documentAccess,
		AdmissionEligibilityConfigurationService eligibilityConfiguration,
		AdmissionReviewPeopleService reviewPeople)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid workflowId)
	{
		var workflow = await definitionStore.GetAsync(workflowId);
		var stages = await definitionStore.GetStagesAsync(workflowId);
		var transitions = await definitionStore.GetTransitionsAsync(workflowId);
		var issues = graphValidator.Validate(stages, transitions)
			.Select(AdmissionWorkflowMapper.ToDto)
			.ToList();
		if (workflow.DefaultInquiryFormDefinitionId.HasValue)
		{
			var inquiryForm = await formRepository.FindAsync(workflow.DefaultInquiryFormDefinitionId.Value);
			if (inquiryForm == null || inquiryForm.Status != AdmissionFormStatus.Published ||
				inquiryForm.Purpose != AdmissionFormPurpose.Inquiry)
				issues.Add(new AdmissionWorkflowValidationIssueDto { Code = AdmissionWorkflowErrorCodes.InvalidInquiryForm,
					Message = "Select a published Inquiry form for this workflow.",
					Severity = AdmissionWorkflowValidationSeverity.Error });
		}
		foreach (var stage in stages.Where(stage => stage.StageType == AdmissionWorkflowStageType.Form))
		{
			if (issues.Any(issue => issue.StageId == stage.Id && issue.Severity == AdmissionWorkflowValidationSeverity.Error)) continue;
			if (!TryReadGuid(stage.ConfigurationJson, "formDefinitionId", out var formId)) continue;
			var form = await formRepository.FindAsync(formId);
			if (form == null || form.Status != AdmissionFormStatus.Published ||
				form.Purpose != AdmissionFormPurpose.Registration)
			{
				issues.Add(new AdmissionWorkflowValidationIssueDto
				{
					Code = AdmissionWorkflowErrorCodes.InvalidTransition,
					Message = $"Stage '{stage.Name}' must reference a published student registration form in this tenant.",
					Severity = AdmissionWorkflowValidationSeverity.Error,
					StageId = stage.Id
				});
			}
			try
			{
				var policy = AdmissionFormStagePolicy.Read(stage.ConfigurationJson);
				if (policy?.RequireSectionVerification == true && form != null)
				{
					var sectionCodes = (await formSectionRepository.GetListAsync(item => item.FormDefinitionId == form.Id))
						.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
					if (policy.VerificationSectionCodes.Any(code => !sectionCodes.Contains(code)))
						issues.Add(new AdmissionWorkflowValidationIssueDto { Code = AdmissionFormStageErrorCodes.InvalidSection,
							Message = $"Stage '{stage.Name}' references a form section that is not in the selected published form.",
							Severity = AdmissionWorkflowValidationSeverity.Error, StageId = stage.Id });
				}
			}
			catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
			{
				issues.Add(new AdmissionWorkflowValidationIssueDto { Code = AdmissionFormStageErrorCodes.InvalidPolicy,
					Message = $"Stage '{stage.Name}': {exception.Message}", Severity = AdmissionWorkflowValidationSeverity.Error,
					StageId = stage.Id });
			}
		}
		foreach (var stage in stages.Where(stage => stage.StageType == AdmissionWorkflowStageType.DocumentVerification))
		{
			if (issues.Any(issue => issue.StageId == stage.Id && issue.Severity == AdmissionWorkflowValidationSeverity.Error)) continue;
			var policy = AdmissionDocumentAccessService.ReadPolicy(stage);
			if (policy == null) continue;
			if (policy.CollectionFormStageCode != null && !AdmissionDocumentCollectionRules.IsLinkedForm(
				stages.FirstOrDefault(item => item.Code == policy.CollectionFormStageCode), stage, policy, transitions))
				issues.Add(new AdmissionWorkflowValidationIssueDto { Code = AdmissionDocumentErrorCodes.InvalidPolicy,
					Message = $"Stage '{stage.Name}': choose a Form stage with a route to this document stage for early collection.",
					Severity = AdmissionWorkflowValidationSeverity.Error, StageId = stage.Id });
			foreach (var requirement in policy.Requirements)
			{
				try
				{
					await documentAccess.ValidateMappingAsync(requirement);
					await documentAccess.ValidateClassesAsync(requirement);
				}
				catch (Volo.Abp.BusinessException exception)
				{
					issues.Add(new AdmissionWorkflowValidationIssueDto { Code = AdmissionDocumentErrorCodes.InvalidPolicy,
						Message = $"{requirement.Name}: {exception.Message}", Severity = AdmissionWorkflowValidationSeverity.Error, StageId = stage.Id });
				}
			}
		}
		foreach (var stage in stages.Where(stage => stage.StageType == AdmissionWorkflowStageType.Payment))
		{
			if (issues.Any(issue => issue.StageId == stage.Id && issue.Severity == AdmissionWorkflowValidationSeverity.Error)) continue;
			try
			{
				var policy = AdmissionPaymentAccessService.ReadPolicy(stage);
				if (policy?.AcceptedMethods.Contains("onlineGateway", StringComparer.Ordinal) == true)
					issues.Add(new AdmissionWorkflowValidationIssueDto
					{
						Code = AdmissionPaymentStageErrorCodes.InvalidPolicy,
						Message = $"Stage '{stage.Name}' uses online gateway collection, but this tenant has no admission payment gateway configured.",
						Severity = AdmissionWorkflowValidationSeverity.Error,
						StageId = stage.Id
					});
			}
			catch (Volo.Abp.BusinessException exception)
			{
				issues.Add(new AdmissionWorkflowValidationIssueDto { Code = exception.Code ?? AdmissionPaymentStageErrorCodes.InvalidPolicy,
					Message = $"Stage '{stage.Name}': {exception.Message}", Severity = AdmissionWorkflowValidationSeverity.Error,
					StageId = stage.Id });
			}
		}
		foreach (var stage in stages.Where(stage => stage.StageType == AdmissionWorkflowStageType.Offer))
		{
			if (issues.Any(issue => issue.StageId == stage.Id && issue.Severity == AdmissionWorkflowValidationSeverity.Error)) continue;
			try { _ = AdmissionOfferAccessService.ReadPolicy(stage); }
			catch (Volo.Abp.BusinessException exception)
			{
				issues.Add(new AdmissionWorkflowValidationIssueDto
				{
					Code = exception.Code ?? AdmissionOfferStageErrorCodes.InvalidPolicy,
					Message = $"Stage '{stage.Name}': {exception.Message}",
					Severity = AdmissionWorkflowValidationSeverity.Error,
					StageId = stage.Id
				});
			}
		}
		foreach (var stage in stages.Where(stage => stage.StageType == AdmissionWorkflowStageType.Seat))
		{
			if (issues.Any(issue => issue.StageId == stage.Id && issue.Severity == AdmissionWorkflowValidationSeverity.Error)) continue;
			try { _ = AdmissionSeatAccessService.ReadPolicy(stage); }
			catch (Volo.Abp.BusinessException exception)
			{
				issues.Add(new AdmissionWorkflowValidationIssueDto
				{
					Code = exception.Code ?? AdmissionSeatStageErrorCodes.InvalidPolicy,
					Message = $"Stage '{stage.Name}': {exception.Message}",
					Severity = AdmissionWorkflowValidationSeverity.Error,
					StageId = stage.Id
				});
			}
		}
		foreach (var stage in stages.Where(stage => stage.StageType == AdmissionWorkflowStageType.Eligibility &&
			!issues.Any(issue => issue.StageId == stage.Id && issue.Severity == AdmissionWorkflowValidationSeverity.Error)))
			issues.AddRange(await eligibilityConfiguration.ValidateAsync(stage));
		foreach (var stage in stages.Where(stage => stage.StageType == AdmissionWorkflowStageType.Review &&
			!issues.Any(issue => issue.StageId == stage.Id && issue.Severity == AdmissionWorkflowValidationSeverity.Error)))
			issues.AddRange(await reviewPeople.ValidatePolicyAsync(stage));
		return new AdmissionWorkflowValidationResultDto { Issues = issues };
	}

	private static bool TryReadGuid(string? json, string propertyName, out Guid value)
	{
		value = Guid.Empty;
		if (string.IsNullOrWhiteSpace(json)) return false;
		try
		{
			using var document = JsonDocument.Parse(json);
			return document.RootElement.TryGetProperty(propertyName, out var property) &&
				Guid.TryParse(property.GetString(), out value);
		}
		catch (JsonException)
		{
			return false;
		}
	}
}
