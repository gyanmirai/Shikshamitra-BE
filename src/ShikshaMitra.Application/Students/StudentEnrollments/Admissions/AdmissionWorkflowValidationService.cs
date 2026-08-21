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
	AdmissionWorkflowGraphValidator graphValidator,
	AdmissionWorkflowDefinitionStore definitionStore)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid workflowId)
	{
		await definitionStore.GetAsync(workflowId);
		var stages = await definitionStore.GetStagesAsync(workflowId);
		var transitions = await definitionStore.GetTransitionsAsync(workflowId);
		var issues = graphValidator.Validate(stages, transitions)
			.Select(AdmissionWorkflowMapper.ToDto)
			.ToList();
		foreach (var stage in stages.Where(stage => stage.StageType == AdmissionWorkflowStageType.Form))
		{
			if (!TryReadGuid(stage.ConfigurationJson, "formDefinitionId", out var formId)) continue;
			var form = await formRepository.FindAsync(formId);
			if (form == null || form.Status != AdmissionFormStatus.Published)
			{
				issues.Add(new AdmissionWorkflowValidationIssueDto
				{
					Code = AdmissionWorkflowErrorCodes.InvalidTransition,
					Message = $"Stage '{stage.Name}' must reference a published form in this tenant.",
					Severity = AdmissionWorkflowValidationSeverity.Error,
					StageId = stage.Id
				});
			}
		}
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
