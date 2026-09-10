using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationFormService(
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	IRepository<AdmissionFamily, Guid> familyRepository,
	IRepository<AdmissionGuardian, Guid> guardianRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionFormSectionDefinition, Guid> sectionRepository,
	IRepository<AdmissionFormFieldDefinition, Guid> fieldRepository,
	IRepository<AdmissionFormInstance, Guid> instanceRepository,
	IRepository<AdmissionFormFieldValue, Guid> valueRepository,
	IRepository<AdmissionFormSubmission, Guid> submissionRepository,
	IRepository<AdmissionFormChangeHistory, Guid> historyRepository,
	IRepository<AdmissionActivity, Guid> activityRepository,
	AdmissionApplicationReadService applicationReadService,
	AdmissionApplicationFormValidator formValidator,
	AdmissionFormDataSourceRegistry dataSourceRegistry,
	AdmissionApplicationWorkflowService workflowService)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicationFormDto> GetAsync(Guid applicationId)
	{
		var application = await applicationReadService.GetEntityAsync(applicationId);
		var instance = await GetInstanceAsync(applicationId);
		var cycle = await cycleRepository.FindAsync(application.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), application.AdmissionCycleId);
		var definition = await formRepository.FindAsync(instance.FormDefinitionId) ??
			throw new EntityNotFoundException(typeof(AdmissionFormDefinition), instance.FormDefinitionId);
		var sections = await sectionRepository.GetListAsync(section => section.FormDefinitionId == definition.Id);
		var fields = await fieldRepository.GetListAsync(field => field.FormDefinitionId == definition.Id);
		var values = await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		return new AdmissionApplicationFormDto
		{
			FormInstanceId = instance.Id,
			FormDefinitionId = instance.FormDefinitionId,
			AcademicYearId = cycle.AcademicYearId,
			FormVersion = instance.FormVersion,
			Status = instance.Status,
			SubmittedAt = instance.SubmittedAt,
			Definition = MapDefinition(definition, sections, fields),
			Values = values.OrderBy(value => value.FieldCode).Select(value => new AdmissionFormValueDto
			{
				FieldCode = value.FieldCode,
				ValueJson = value.ValueJson
			}).ToList()
		};
	}

	public async Task<AdmissionApplicationFormDto> UpdateAsync(
		Guid applicationId,
		UpdateAdmissionApplicationFormValuesDto input)
	{
		var application = await applicationReadService.GetEntityAsync(applicationId);
		application.SaveDraft();
		EnsureConcurrency(application, input.ConcurrencyStamp);
		var instance = await GetInstanceAsync(applicationId);
		var existingValues = await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var definitions = await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId);
		var applicant = await applicantRepository.FindAsync(application.ApplicantId);
		AdmissionGuardian? guardian = null;
		if (applicant is not null && definitions.Any(field => field.BindingType == AdmissionFormBindingType.System && field.BindingKey?.StartsWith("Guardian.", StringComparison.OrdinalIgnoreCase) == true))
		{
			var family = await familyRepository.FindAsync(applicant.FamilyId);
			if (family?.PrimaryGuardianId is Guid guardianId) guardian = await guardianRepository.FindAsync(guardianId);
		}
		var definitionCodes = definitions.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var unknownCode = input.Values.FirstOrDefault(value => !definitionCodes.Contains(value.FieldCode))?.FieldCode;
		if (!string.IsNullOrWhiteSpace(unknownCode))
			throw new BusinessException(AdmissionApplicationErrorCodes.UnknownFormField).WithData("FieldCode", unknownCode);
		foreach (var item in input.Values
			.DistinctBy(value => value.FieldCode.ToUpperInvariant())
			.Select(value => new
			{
				Input = value,
				Definition = definitions.FirstOrDefault(field => field.Code == value.FieldCode.ToUpperInvariant())
			})
			.Where(item => item.Definition is { ReadOnly: false }))
		{
			var definition = item.Definition!;
			var code = item.Input.FieldCode.ToUpperInvariant();
			var existing = existingValues.FirstOrDefault(value => value.FieldCode == code);
			if (existing == null)
				await valueRepository.InsertAsync(new AdmissionFormFieldValue(
					GuidGenerator.Create(), CurrentTenant.Id, instance.Id, code, item.Input.ValueJson), false);
			else
			{
				existing.SetValue(item.Input.ValueJson);
				await valueRepository.UpdateAsync(existing, false);
			}
			if (definition.BindingType == AdmissionFormBindingType.System && applicant is not null && !string.IsNullOrWhiteSpace(item.Input.ValueJson))
				AdmissionFormBindingRegistry.ApplyValue(definition.BindingKey!, AdmissionApplicationValueConverter.Unwrap(item.Input.ValueJson), applicant, guardian);
		}
		if (applicant is not null) await applicantRepository.UpdateAsync(applicant, false);
		if (guardian is not null) await guardianRepository.UpdateAsync(guardian, false);
		instance.MarkSaved();
		await instanceRepository.UpdateAsync(instance, false);
		await historyRepository.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id, instance.Id, application.Id,
			"DraftSaved", System.Text.Json.JsonSerializer.Serialize(input.Values.Select(value => value.FieldCode).Distinct(StringComparer.OrdinalIgnoreCase)), CurrentUser.Id), false);
		await applicationRepository.UpdateAsync(application, false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"ApplicationSaved", "Application draft saved", application.ApplicantId, application.Id), true);
		return await GetAsync(applicationId);
	}

	public async Task<AdmissionApplicationSubmitResultDto> SubmitAsync(Guid applicationId)
	{
		var application = await applicationReadService.GetEntityAsync(applicationId);
		if (application.Status == AdmissionApplicationStatus.Submitted)
			return new AdmissionApplicationSubmitResultDto { Application = AdmissionApplicationMapper.ToDto(application) };
		application.SaveDraft();
		var instance = await GetInstanceAsync(applicationId);
		var fields = await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId);
		var cycle = await cycleRepository.FindAsync(application.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), application.AdmissionCycleId);
		var sections = await sectionRepository.GetListAsync(section => section.FormDefinitionId == instance.FormDefinitionId);
		var values = await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var valueMap = values.ToDictionary(
			value => value.FieldCode,
			value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson),
			StringComparer.OrdinalIgnoreCase);
		var validation = formValidator.Validate(fields, sections, valueMap);
		await ValidateMasterDataOptionsAsync(fields, sections, valueMap, cycle.AcademicYearId, validation);
		if (validation.Errors.Count > 0)
		{
			return new AdmissionApplicationSubmitResultDto
			{
				Application = AdmissionApplicationMapper.ToDto(application),
				ValidationErrors = validation.Errors,
				FieldErrors = validation.FieldErrors
			};
		}
		instance.Submit(CurrentUser.Id);
		var attemptNumber = (await submissionRepository.GetListAsync(submission => submission.FormInstanceId == instance.Id)).Count + 1;
		await submissionRepository.InsertAsync(new AdmissionFormSubmission(GuidGenerator.Create(), CurrentTenant.Id, instance.Id, application.Id,
			attemptNumber, CurrentUser.Id, System.Text.Json.JsonSerializer.Serialize(valueMap)), false);
		await historyRepository.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id, instance.Id, application.Id,
			"Submitted", null, CurrentUser.Id), false);
		application.Submit();
		await workflowService.AdvanceAfterSubmitAsync(application);
		await applicationRepository.UpdateAsync(application, false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"ApplicationSubmitted", "Application submitted", application.ApplicantId, application.Id), true);
		return new AdmissionApplicationSubmitResultDto { Application = AdmissionApplicationMapper.ToDto(application) };
	}

	public async Task<AdmissionApplicationFormDto> ReopenAsync(Guid applicationId)
	{
		var application = await applicationReadService.GetEntityAsync(applicationId);
		var instance = await GetInstanceAsync(applicationId);
		application.Reopen();
		instance.Reopen();
		await applicationRepository.UpdateAsync(application, false);
		await instanceRepository.UpdateAsync(instance, false);
		await historyRepository.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id, instance.Id, application.Id,
			"Reopened", null, CurrentUser.Id), true);
		return await GetAsync(applicationId);
	}

	public async Task<List<AdmissionFormChangeHistoryDto>> GetHistoryAsync(Guid applicationId)
	{
		var application = await applicationReadService.GetEntityAsync(applicationId);
		var instance = await GetInstanceAsync(application.Id);
		var history = await historyRepository.GetListAsync(item => item.FormInstanceId == instance.Id);
		return history.OrderByDescending(item => item.OccurredAt).Select(item => new AdmissionFormChangeHistoryDto
		{
			Id = item.Id, ChangeType = item.ChangeType, ChangedFieldsJson = item.ChangedFieldsJson,
			ChangedBy = item.ChangedBy, OccurredAt = item.OccurredAt
		}).ToList();
	}

	private async Task<AdmissionFormInstance> GetInstanceAsync(Guid applicationId) =>
		await instanceRepository.FindAsync(instance => instance.ApplicationId == applicationId) ??
		throw new EntityNotFoundException(typeof(AdmissionFormInstance), applicationId);

	private static void EnsureConcurrency(AdmissionApplication application, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) ||
			!string.Equals(application.ConcurrencyStamp, stamp, StringComparison.Ordinal))
			throw new BusinessException(AdmissionApplicationErrorCodes.ConcurrencyConflict);
	}

	private async Task ValidateMasterDataOptionsAsync(
		IEnumerable<AdmissionFormFieldDefinition> fields,
		IEnumerable<AdmissionFormSectionDefinition> sections,
		IReadOnlyDictionary<string, string> values,
		Guid academicYearId,
		AdmissionFormValidationResult validation)
	{
		var visibleFieldCodes = formValidator.GetVisibleFieldCodes(fields, sections, values);
		foreach (var field in fields.Where(field => visibleFieldCodes.Contains(field.Code) && field.FieldType is (AdmissionFormFieldType.Select or AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.Radio)))
		{
			if (string.IsNullOrWhiteSpace(field.OptionsJson) || !TryReadSource(field.OptionsJson, out var sourceKey, out var parentField)) continue;
			var parentId = parentField != null && Guid.TryParse(values.GetValueOrDefault(parentField), out var parsedParentId) ? parsedParentId : (Guid?)null;
			var allowedValues = (await dataSourceRegistry.GetOptionsAsync(sourceKey, parentId, academicYearId)).Select(option => option.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
			IReadOnlyList<string> submitted = field.FieldType == AdmissionFormFieldType.MultiSelect
				? ReadMultipleValues(values.GetValueOrDefault(field.Code))
				: new[] { values.GetValueOrDefault(field.Code) ?? string.Empty };
			if (submitted.Any(value => !string.IsNullOrWhiteSpace(value) && !allowedValues.Contains(value)))
			{
				var message = $"{field.Code} contains an invalid option.";
				validation.Errors.Add(message);
				validation.FieldErrors.TryAdd(field.Code, message);
			}
		}
	}

	private static bool TryReadSource(string json, out string sourceKey, out string? parentField)
	{
		sourceKey = string.Empty;
		parentField = null;
		try
		{
			using var document = JsonDocument.Parse(json);
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.String) return false;
			sourceKey = source.GetString() ?? string.Empty;
			if (root.TryGetProperty("parentField", out var parent) && parent.ValueKind == JsonValueKind.String) parentField = parent.GetString();
			return !string.IsNullOrWhiteSpace(sourceKey);
		}
		catch (JsonException) { return false; }
	}

	private static IReadOnlyList<string> ReadMultipleValues(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return [];
		try
		{
			using var document = JsonDocument.Parse(value);
			return document.RootElement.ValueKind == JsonValueKind.Array
				? document.RootElement.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToList()
				: [];
		}
		catch (JsonException) { return []; }
	}

	private static AdmissionFormDto MapDefinition(
		AdmissionFormDefinition definition,
		IEnumerable<AdmissionFormSectionDefinition> sections,
		IReadOnlyCollection<AdmissionFormFieldDefinition> fields) => new()
	{
		Id = definition.Id,
		Name = definition.Name,
		Code = definition.Code,
		Description = definition.Description,
		Version = definition.Version,
		Status = definition.Status,
		LayoutType = definition.LayoutType,
		FieldsPerRow = definition.FieldsPerRow,
		ParentVersionId = definition.ParentVersionId,
		PublishedAt = definition.PublishedAt,
		PublishedBy = definition.PublishedBy,
		ConcurrencyStamp = definition.ConcurrencyStamp,
		Sections = sections.OrderBy(section => section.DisplayOrder).Select(section => new AdmissionFormSectionDto
		{
			Id = section.Id,
			FormDefinitionId = section.FormDefinitionId,
			Code = section.Code,
			Name = section.Name,
			DisplayOrder = section.DisplayOrder,
			VisibilityRuleJson = section.VisibilityRuleJson,
			StepId = section.StepId,
			StepCode = section.StepCode,
			StepName = section.StepName,
			StepDisplayOrder = section.StepDisplayOrder,
			StepVisibilityRuleJson = section.StepVisibilityRuleJson,
			Fields = fields.Where(field => field.SectionId == section.Id).OrderBy(field => field.DisplayOrder)
				.Select(MapField).ToList()
		}).ToList()
	};

	private static AdmissionFormFieldDto MapField(AdmissionFormFieldDefinition field) => new()
	{
		Id = field.Id, FormDefinitionId = field.FormDefinitionId, SectionId = field.SectionId,
		Code = field.Code, Label = field.Label, FieldType = field.FieldType, BindingType = field.BindingType,
		BindingKey = field.BindingKey, Placeholder = field.Placeholder, HelpText = field.HelpText,
		DefaultValue = field.DefaultValue, OptionsJson = field.OptionsJson, Required = field.Required, ReadOnly = field.ReadOnly,
		DisplayOrder = field.DisplayOrder, ValidationJson = field.ValidationJson,
		VisibilityRuleJson = field.VisibilityRuleJson, RequiredRuleJson = field.RequiredRuleJson
	};
}
