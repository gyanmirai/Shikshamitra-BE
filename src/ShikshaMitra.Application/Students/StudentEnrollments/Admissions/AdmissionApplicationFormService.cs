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
	AdmissionFormMasterDataValidator masterDataValidator,
	AdmissionApplicationWorkflowService workflowService,
	AdmissionFormStageRuntimeService formStageRuntime,
	AdmissionReviewDecisionGuard reviewGuard)
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
		if (applicant != null)
			await RecoverFirstParentContactAsync(application, applicant, instance, definitions, existingValues);
		var parentAnswers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
			var valueJson = AdmissionFormEmailValueNormalizer.Normalize(definition, item.Input.ValueJson);
			if (definition.BindingType == AdmissionFormBindingType.System && definition.BindingKey == "Family.SiblingStudentIds")
			{
				var siblings = AdmissionStudentProfileMapping.ReadSiblings(AdmissionApplicationValueConverter.Unwrap(item.Input.ValueJson)) ?? [];
				if (siblings.Contains(applicant?.ExistingStudentId ?? applicant?.ReservedStudentId ?? Guid.Empty))
					throw new BusinessException(AdmissionApplicationErrorCodes.UnknownFormField).WithData("FieldCode", code);
				if (!await dataSourceRegistry.AreSiblingIdsAllowedAsync(siblings))
					throw new BusinessException("Admissions:InvalidSiblingSelection");
			}
			var existing = existingValues.FirstOrDefault(value => value.FieldCode == code);
			if (existing == null)
				await valueRepository.InsertAsync(new AdmissionFormFieldValue(
					GuidGenerator.Create(), CurrentTenant.Id, instance.Id, code, valueJson), false);
			else
			{
				existing.SetValue(valueJson);
				await valueRepository.UpdateAsync(existing, false);
			}
			if (definition.BindingType == AdmissionFormBindingType.System && applicant is not null &&
				!((applicant.ExistingStudentId.HasValue || AdmissionSiblingParentSelection.ReadConfirmation(applicant) != null) && definition.BindingKey is "Guardian.FirstName" or "Guardian.LastName" or "Guardian.Mobile" or "Guardian.Email"))
			{
				var value = AdmissionApplicationValueConverter.Unwrap(valueJson);
				parentAnswers[definition.BindingKey!] = value;
				if (!string.IsNullOrWhiteSpace(value) ||
					AdmissionAdditionalStudentFieldCatalog.Metadata.ContainsKey(definition.BindingKey!) ||
					(AdmissionFirstParentContactMapping.CanMap(applicant) && definition.BindingKey is "Guardian.Email" or "Guardian.LastName"))
					AdmissionFormBindingRegistry.ApplyValue(definition.BindingKey!, value, applicant, guardian);
			}
		}
		if (applicant is not null)
		{
			AdmissionFirstParentContactMapping.Apply(applicant, parentAnswers, guardian);
			await applicantRepository.UpdateAsync(applicant, false);
			if (application.InquiryId.HasValue)
			{
				var inquiries = LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionInquiry, Guid>>();
				var inquiry = await inquiries.FindAsync(application.InquiryId.Value);
				if (inquiry?.ApplicantId == applicant.Id)
				{
					AdmissionFamilyContactCapture.CopyToInquiry(applicant, inquiry);
					await inquiries.UpdateAsync(inquiry, false);
				}
			}
		}
		if (guardian is not null && applicant?.ExistingStudentId.HasValue != true && (applicant == null || AdmissionSiblingParentSelection.ReadConfirmation(applicant) == null)) await guardianRepository.UpdateAsync(guardian, false);
		instance.MarkSaved();
		await instanceRepository.UpdateAsync(instance, false);
		await historyRepository.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id, instance.Id, application.Id,
			"DraftSaved", System.Text.Json.JsonSerializer.Serialize(input.Values.Select(value => value.FieldCode).Distinct(StringComparer.OrdinalIgnoreCase)), CurrentUser.Id), false);
		await applicationRepository.UpdateAsync(application, false);
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("ApplicationSaved", "Application draft saved", application.ApplicantId, application.Id), true);
		return await GetAsync(applicationId);
	}

	internal async Task RecoverFirstParentContactAsync(AdmissionApplication application, AdmissionApplicant applicant,
		AdmissionFormInstance? instance = null, IReadOnlyCollection<AdmissionFormFieldDefinition>? definitions = null,
		IReadOnlyCollection<AdmissionFormFieldValue>? values = null)
	{
		if (!AdmissionFirstParentContactMapping.CanMap(applicant)) return;
		var answers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (application.InquiryId.HasValue)
		{
			var inquiry = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionInquiry, Guid>>().FindAsync(application.InquiryId.Value);
			if (inquiry?.ApplicantId == applicant.Id && inquiry.FormResponseDefinitionId.HasValue && !string.IsNullOrWhiteSpace(inquiry.FormResponseSnapshotJson))
			{
				var inquiryFields = await fieldRepository.GetListAsync(field => field.FormDefinitionId == inquiry.FormResponseDefinitionId.Value);
				answers = AdmissionFirstParentContactMapping.ReadInquiryAnswers(inquiry, inquiryFields);
			}
		}
		instance ??= await GetInstanceAsync(application.Id);
		definitions ??= await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId);
		values ??= await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var saves = await historyRepository.GetListAsync(item => item.FormInstanceId == instance.Id && item.ChangeType == "DraftSaved");
		var editedCodes = saves.SelectMany(save => JsonSerializer.Deserialize<string[]>(save.ChangedFieldsJson ?? "[]") ?? [])
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		// Initial prefills are not authored answers and cannot claim structured ownership over an Inquiry clear.
		foreach (var field in definitions.Where(field => field.BindingType == AdmissionFormBindingType.System && field.BindingKey != null && !field.ReadOnly && editedCodes.Contains(field.Code)))
		{
			var value = values.FirstOrDefault(value => string.Equals(value.FieldCode, field.Code, StringComparison.OrdinalIgnoreCase));
			if (value != null) answers[field.BindingKey!] = AdmissionApplicationValueConverter.Unwrap(value.ValueJson);
		}
		// Only accepted, server-saved answers repair pre-fix unfinalized records; no blanket shared-contact fallback.
		AdmissionFirstParentContactMapping.Apply(applicant, answers, recoverSavedAnswers: true);
	}

	public async Task<AdmissionApplicationSubmitResultDto> SubmitAsync(Guid applicationId)
	{
		var application = await applicationReadService.GetEntityAsync(applicationId);
		if (application.Status == AdmissionApplicationStatus.Submitted)
		{
			// A legacy workflow can enter its Form stage only after the same submit request
			// completes an older Inquiry entry stage. Retrying a successful submission must
			// therefore repair the workflow handoff instead of returning the stuck record.
			await workflowService.AdvanceAfterSubmitAsync(application);
			return new AdmissionApplicationSubmitResultDto { Application = AdmissionApplicationMapper.ToDto(application) };
		}
		application.SaveDraft();
		await formStageRuntime.EnsureCanSubmitAsync(applicationId);
		var instance = await GetInstanceAsync(applicationId);
		var fields = await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId);
		var cycle = await cycleRepository.FindAsync(application.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), application.AdmissionCycleId);
		var definition = await formRepository.GetAsync(instance.FormDefinitionId);
		var experience = AdmissionFormMetadataStore.ReadExperience(definition);
		var sections = await sectionRepository.GetListAsync(section => section.FormDefinitionId == instance.FormDefinitionId);
		var values = await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var valueMap = values.ToDictionary(
			value => value.FieldCode,
			value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson),
			StringComparer.OrdinalIgnoreCase);
		var validation = formValidator.Validate(fields, sections, valueMap, experience);
		await masterDataValidator.ValidateAsync(fields, sections, valueMap, cycle.AcademicYearId, validation, experience);
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
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("ApplicationSubmitted", "Application submitted", application.ApplicantId, application.Id), true);
		return new AdmissionApplicationSubmitResultDto { Application = AdmissionApplicationMapper.ToDto(application) };
	}

	public async Task<AdmissionApplicationFormDto> ReopenAsync(Guid applicationId)
	{
		var application = await applicationReadService.GetEntityAsync(applicationId);
		await formStageRuntime.EnsureDirectReopenAllowedAsync(applicationId);
		var instance = await GetInstanceAsync(applicationId);
		var review = await reviewGuard.GetStateAsync(application);
		if (review?.Context.IsActive == true)
		{
			if (review.Decision?.Decision != "requestCorrection") throw new BusinessException(AdmissionReviewErrorCodes.DecisionNotAllowed);
			application.ReopenForManagedReview();
		}
		else application.Reopen();
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

	internal static AdmissionFormDto MapDefinition(
		AdmissionFormDefinition definition,
		IEnumerable<AdmissionFormSectionDefinition> sections,
		IReadOnlyCollection<AdmissionFormFieldDefinition> fields)
	{
		var experience = AdmissionFormMetadataStore.ReadExperience(definition);
		return new AdmissionFormDto
		{
			Id = definition.Id, Name = definition.Name, Code = definition.Code, Description = definition.Description,
			Version = definition.Version, Status = definition.Status, LayoutType = definition.LayoutType, Purpose = definition.Purpose,
			FieldsPerRow = definition.FieldsPerRow, DefaultLanguage = experience.DefaultLanguage,
			SupportedLanguages = experience.SupportedLanguages, ParentVersionId = definition.ParentVersionId,
			PublishedAt = definition.PublishedAt, PublishedBy = definition.PublishedBy,
			ConcurrencyStamp = definition.ConcurrencyStamp,
			Sections = sections.OrderBy(section => section.DisplayOrder).Select(section =>
			{
				experience.Sections.TryGetValue(section.Code, out var sectionExperience);
				return new AdmissionFormSectionDto
				{
					Id = section.Id, FormDefinitionId = section.FormDefinitionId, Code = section.Code,
					Name = section.Name, DisplayOrder = section.DisplayOrder, VisibilityRuleJson = section.VisibilityRuleJson,
					StepId = section.StepId, StepCode = section.StepCode, StepName = section.StepName,
					StepDisplayOrder = section.StepDisplayOrder, StepVisibilityRuleJson = section.StepVisibilityRuleJson,
					IsRepeatable = sectionExperience?.IsRepeatable ?? false,
					MinEntries = sectionExperience?.MinEntries ?? 1,
					MaxEntries = sectionExperience?.MaxEntries ?? 1,
					EntryLabel = sectionExperience?.EntryLabel,
					Translations = sectionExperience?.Translations ?? new(StringComparer.OrdinalIgnoreCase),
					Fields = fields.Where(field => field.SectionId == section.Id).OrderBy(field => field.DisplayOrder)
						.Select(field => MapField(field, sectionExperience?.Fields.GetValueOrDefault(field.Code))).ToList()
				};
			}).ToList()
		};
	}

	private static AdmissionFormFieldDto MapField(AdmissionFormFieldDefinition field, Dictionary<string, AdmissionFormTranslationDto>? translations) => new()
	{
		Id = field.Id, FormDefinitionId = field.FormDefinitionId, SectionId = field.SectionId,
		Code = field.Code, Label = field.Label, FieldType = field.FieldType, BindingType = field.BindingType,
		BindingKey = field.BindingKey, Placeholder = field.Placeholder, HelpText = field.HelpText,
		DefaultValue = field.DefaultValue, OptionsJson = field.OptionsJson, Required = field.Required, ReadOnly = field.ReadOnly,
		DisplayOrder = field.DisplayOrder, ValidationJson = field.ValidationJson,
		VisibilityRuleJson = field.VisibilityRuleJson, RequiredRuleJson = field.RequiredRuleJson,
		Translations = translations ?? new(StringComparer.OrdinalIgnoreCase)
	};
}
