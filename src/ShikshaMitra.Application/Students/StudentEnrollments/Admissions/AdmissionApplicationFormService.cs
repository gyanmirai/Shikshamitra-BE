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

public partial class AdmissionApplicationFormService(
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
		var context = await GetFormContextAsync(application);
		var instance = context.Instance;
		var cycle = await cycleRepository.FindAsync(application.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), application.AdmissionCycleId);
		var frozen = AdmissionEffectiveFormStore.Read(instance);
		AdmissionFormDto definitionDto;
		if (frozen != null) definitionDto = frozen.Definition;
		else
		{
			var definition = await formRepository.FindAsync(instance.FormDefinitionId) ??
				throw new EntityNotFoundException(typeof(AdmissionFormDefinition), instance.FormDefinitionId);
			AdmissionEffectiveFormStore.EnsureLegacyAllowed(definition);
			var sections = await sectionRepository.GetListAsync(section => section.FormDefinitionId == definition.Id);
			var fields = await fieldRepository.GetListAsync(field => field.FormDefinitionId == definition.Id);
			definitionDto = MapDefinition(definition, sections, fields);
		}
		var values = await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var storedValues = values.OrderBy(value => value.FieldCode).Select(value => new AdmissionFormValueDto
			{ FieldCode = value.FieldCode, ValueJson = value.ValueJson }).ToList();
		var applicant = context.Applicant;
		var prefill = applicant == null || application.Status != AdmissionApplicationStatus.Draft || application.SubmittedAt.HasValue ? new AdmissionApplicationFormDto { Values = storedValues } :
			await LazyServiceProvider.LazyGetRequiredService<AdmissionSiblingRegistrationPrefillService>()
				.ProjectAsync(applicant, instance, definitionDto, storedValues);
		var correctionBlockedReason = await GetCorrectionBlockedReasonAsync(application);
		return new AdmissionApplicationFormDto
		{
			CanCorrectForm = correctionBlockedReason == null,
			CorrectionBlockedReason = correctionBlockedReason,
			FormInstanceId = instance.Id,
			FormDefinitionId = instance.FormDefinitionId,
			AcademicYearId = cycle.AcademicYearId,
			FormVersion = instance.FormVersion,
			Status = instance.Status,
			SubmittedAt = instance.SubmittedAt,
			Definition = definitionDto,
			InquiryFormDefinitionId = frozen?.InquiryFormDefinitionId,
			InquiryContextSections = frozen?.InquiryContextSections ?? [],
			InquiryContextValues = frozen?.InquiryContextValues ?? [],
			Values = prefill.Values,
			AutoFilledFieldCodes = prefill.AutoFilledFieldCodes,
			FamilyPrefill = prefill.FamilyPrefill
		};
	}

	public async Task<AdmissionApplicationFormDto> UpdateAsync(
		Guid applicationId,
		UpdateAdmissionApplicationFormValuesDto input)
	{
		using var work = UnitOfWorkManager.Begin(new Volo.Abp.Uow.AbpUnitOfWorkOptions
		{ IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var application = await applicationReadService.GetEntityAsync(applicationId);
		application.SaveDraft();
		EnsureConcurrency(application, input.ConcurrencyStamp);
		var context = await GetFormContextAsync(application);
		var instance = context.Instance;
		var acceptedInput = await ApplyValuesAsync(application, instance, input, loadedApplicant: context.Applicant);
		instance.MarkSaved();
		await instanceRepository.UpdateAsync(instance, false);
		await historyRepository.InsertAsync(new AdmissionFormChangeHistory(GuidGenerator.Create(), CurrentTenant.Id, instance.Id, application.Id,
			"DraftSaved", System.Text.Json.JsonSerializer.Serialize(acceptedInput.Values.Select(value => value.FieldCode).Distinct(StringComparer.OrdinalIgnoreCase)), CurrentUser.Id), false);
		await applicationRepository.UpdateAsync(application, false);
		await activityRepository.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("ApplicationSaved", "Application draft saved", application.ApplicantId, application.Id), true);
		var result = await GetAsync(applicationId);
		await work.CompleteAsync();
		return result;
	}

	private static UpdateAdmissionApplicationFormValuesDto ProtectAutomaticOccupationAliases(AdmissionFormDto definition,
		UpdateAdmissionApplicationFormValuesDto input, string? relationship, IReadOnlySet<string> automaticCodes, IReadOnlySet<string> writableCodes)
	{
		if (!AdmissionSiblingParentSelection.IsRelationship(relationship)) return input;
		var role = relationship == "father" ? "Father" : "Mother";
		var aliases = definition.Sections.Where(section => !section.IsRepeatable).SelectMany(section => section.Fields).Where(field => !field.ReadOnly && writableCodes.Contains(field.Code) && field.BindingType == AdmissionFormBindingType.System &&
			(field.BindingKey == role + ".Occupation" || field.BindingKey == "Guardian.Occupation")).Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var submitted = input.Values.DistinctBy(value => value.FieldCode, StringComparer.OrdinalIgnoreCase).Where(value => aliases.Contains(value.FieldCode)).ToList();
		if (submitted.Count < 2) return input;
		string Value(AdmissionFormValueDto value) => AdmissionApplicationValueConverter.Unwrap(value.ValueJson);
		var hasClear = submitted.Any(value => string.IsNullOrWhiteSpace(Value(value)));
		var authored = submitted.Where(value => !automaticCodes.Contains(value.FieldCode)).ToList();
		var suppressed = submitted.Where(value => !string.IsNullOrWhiteSpace(Value(value)) && (hasClear || authored.Count > 0 && automaticCodes.Contains(value.FieldCode)))
			.Select(value => value.FieldCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var remaining = submitted.Where(value => !suppressed.Contains(value.FieldCode)).ToList();
		if (!hasClear && remaining.Select(Value).Distinct(StringComparer.Ordinal).Skip(1).Any())
			throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed, "Parent occupation answers disagree. Review both fields before saving.")
				.WithData("FieldErrors", remaining.ToDictionary(value => value.FieldCode, _ => "Review this parent's occupation answers."));
		return new UpdateAdmissionApplicationFormValuesDto { ConcurrencyStamp = input.ConcurrencyStamp,
			AutoFilledFieldCodes = input.AutoFilledFieldCodes?.Where(code => !suppressed.Contains(code)).ToList(),
			Values = input.Values.Where(value => !suppressed.Contains(value.FieldCode)).ToList() };
	}

	private static HashSet<string> ValidateAutomaticHints(AdmissionFormDto definition, UpdateAdmissionApplicationFormValuesDto input,
		IReadOnlySet<string> visibleCodes, IReadOnlySet<string>? writableCodes)
	{
		var allowed = definition.Sections.Where(section => !section.IsRepeatable).SelectMany(section => section.Fields)
			.Where(AdmissionSiblingRegistrationPrefillPolicy.CanAutomaticallyFill).Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var code in input.AutoFilledFieldCodes ?? [])
		{
			var value = input.Values.FirstOrDefault(value => string.Equals(value.FieldCode, code, StringComparison.OrdinalIgnoreCase));
			if (string.IsNullOrWhiteSpace(code) || !allowed.Contains(code) || !visibleCodes.Contains(code) ||
				writableCodes != null && !writableCodes.Contains(code) || value == null || string.IsNullOrWhiteSpace(AdmissionApplicationValueConverter.Unwrap(value.ValueJson)))
				throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed, "Automatic field hints must refer to writable parent answers in this save.")
					.WithData("FieldCode", code);
			hints.Add(code);
		}
		return hints;
	}

	private async Task<UpdateAdmissionApplicationFormValuesDto> ApplyValuesAsync(AdmissionApplication application, AdmissionFormInstance instance,
		UpdateAdmissionApplicationFormValuesDto input, IReadOnlySet<string>? writableCodesOverride = null, AdmissionApplicant? loadedApplicant = null)
	{
		var existingValues = await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var frozen = AdmissionEffectiveFormStore.Read(instance);
		var legacyDefinition = frozen == null ? await formRepository.GetAsync(instance.FormDefinitionId) : null;
		if (legacyDefinition != null) AdmissionEffectiveFormStore.EnsureLegacyAllowed(legacyDefinition);
		var definitions = frozen == null
			? await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId)
			: AdmissionEffectiveFormResolver.MaterializeFields(frozen.Definition);
		await LazyServiceProvider.LazyGetRequiredService<AdmissionJoiningDateCapture>()
			.GuardFormChangeAsync(application, definitions, input.Values);
		var applicant = loadedApplicant ?? await applicantRepository.FindAsync(application.ApplicantId);
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
		IReadOnlySet<string>? writableCodes = writableCodesOverride;
		if (frozen != null)
		{
			foreach (var value in input.Values)
			{
				var field = definitions.First(item => string.Equals(item.Code, value.FieldCode, StringComparison.OrdinalIgnoreCase));
				if (field.ReadOnly && AdmissionApplicationValueConverter.Unwrap(existingValues.FirstOrDefault(item => item.FieldCode == field.Code)?.ValueJson) !=
					AdmissionApplicationValueConverter.Unwrap(value.ValueJson))
					throw new BusinessException(AdmissionApplicationErrorCodes.UnknownFormField).WithData("FieldCode", field.Code);
			}
			var currentValues = existingValues.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
			foreach (var value in input.Values.Where(value => definitions.Any(field => !field.ReadOnly && string.Equals(field.Code, value.FieldCode, StringComparison.OrdinalIgnoreCase))))
				currentValues[value.FieldCode] = AdmissionApplicationValueConverter.Unwrap(value.ValueJson);
			writableCodes ??= AdmissionEffectiveFormResolver.VisibleFieldCodes(frozen.Definition, currentValues, formValidator);
		}
		var automaticCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		AdmissionApplicationFormDto? prefill = null;
		var sibling = applicant == null ? null : AdmissionSiblingParentSelection.ReadConfirmation(applicant);
		var genericParent = false;
		AdmissionFormDto? parentDefinition = null;
		if (sibling != null)
		{
			parentDefinition = frozen?.Definition ?? MapDefinition(legacyDefinition!,
				await sectionRepository.GetListAsync(section => section.FormDefinitionId == instance.FormDefinitionId), definitions);
			var current = existingValues.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
			var posted = input.Values.Where(value => definitions.Any(field => !field.ReadOnly && string.Equals(field.Code, value.FieldCode, StringComparison.OrdinalIgnoreCase)) &&
				(writableCodes == null || writableCodes.Contains(value.FieldCode))).ToList();
			foreach (var value in posted) current[value.FieldCode] = AdmissionApplicationValueConverter.Unwrap(value.ValueJson);
			var visible = AdmissionEffectiveFormResolver.VisibleFieldCodes(parentDefinition, current, formValidator);
			var identityValues = existingValues.Select(value => new AdmissionFormValueDto { FieldCode = value.FieldCode, ValueJson = value.ValueJson }).ToList();
			foreach (var value in posted.Where(value => visible.Contains(value.FieldCode)))
			{
				identityValues.RemoveAll(existing => string.Equals(existing.FieldCode, value.FieldCode, StringComparison.OrdinalIgnoreCase));
				identityValues.Add(value);
			}
			genericParent = AdmissionSiblingRegistrationPrefillPolicy.CanUseSelectedParentAliases(applicant!,
				parentDefinition.Sections.Where(section => !section.IsRepeatable).SelectMany(section => section.Fields), identityValues, sibling.Relationship);
		}
		if (application.Status == AdmissionApplicationStatus.Draft && !application.SubmittedAt.HasValue &&
			(sibling != null || input.AutoFilledFieldCodes?.Count > 0))
		{
			var definition = parentDefinition ?? frozen?.Definition ?? MapDefinition(legacyDefinition!,
				await sectionRepository.GetListAsync(section => section.FormDefinitionId == instance.FormDefinitionId), definitions);
			var current = existingValues.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
			foreach (var value in input.Values) current[value.FieldCode] = AdmissionApplicationValueConverter.Unwrap(value.ValueJson);
			var visibleCodes = AdmissionEffectiveFormResolver.VisibleFieldCodes(definition, current, formValidator);
			automaticCodes = ValidateAutomaticHints(definition, input, visibleCodes, writableCodes);
			if (!genericParent) automaticCodes.RemoveWhere(code => definitions.Any(field => string.Equals(field.Code, code, StringComparison.OrdinalIgnoreCase) &&
				field.BindingKey?.StartsWith("Guardian.", StringComparison.OrdinalIgnoreCase) == true));
			input = ProtectAutomaticOccupationAliases(definition, input, genericParent ? sibling?.Relationship : null, automaticCodes, writableCodes ?? visibleCodes);
			if (automaticCodes.Count > 0 && sibling != null)
				prefill = await LazyServiceProvider.LazyGetRequiredService<AdmissionSiblingRegistrationPrefillService>().ProjectAsync(applicant!, instance,
					definition, existingValues.Select(value => new AdmissionFormValueDto { FieldCode = value.FieldCode, ValueJson = value.ValueJson }).ToList());
		}
		else if (input.AutoFilledFieldCodes?.Count > 0)
			throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed, "Automatic field hints apply only to an initial draft.");
		var acceptedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
			.Where(item => item.Definition is { ReadOnly: false } && (writableCodes == null || writableCodes.Contains(item.Definition.Code))))
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
			acceptedCodes.Add(code);
			if (definition.BindingType == AdmissionFormBindingType.System && applicant is not null &&
				!((applicant.ExistingStudentId.HasValue || AdmissionSiblingParentSelection.ReadConfirmation(applicant) != null) && definition.BindingKey is "Guardian.FirstName" or "Guardian.LastName" or "Guardian.Mobile" or "Guardian.Email"))
			{
				var value = AdmissionApplicationValueConverter.Unwrap(valueJson);
				var bindingKey = sibling != null && genericParent && definition.BindingKey == "Guardian.Occupation"
					? (sibling.Relationship == "father" ? "Father.Occupation" : "Mother.Occupation") : definition.BindingKey!;
				parentAnswers[bindingKey] = value;
				if (!string.IsNullOrWhiteSpace(value) ||
					AdmissionSubmissionIdentityValidator.IsOptionalNamePart(definition) ||
					AdmissionAdditionalStudentFieldCatalog.Metadata.ContainsKey(definition.BindingKey!) ||
					(AdmissionFirstParentContactMapping.CanMap(applicant) && definition.BindingKey is "Guardian.Email" or "Guardian.LastName"))
					AdmissionFormBindingRegistry.ApplyValue(bindingKey, value, applicant, guardian);
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
		if (prefill?.FamilyPrefill != null)
		{
			var copied = automaticCodes.Where(acceptedCodes.Contains).ToArray();
			if (copied.Length > 0) await LazyServiceProvider.LazyGetRequiredService<AdmissionSiblingRegistrationPrefillService>()
				.RecordAsync(instance, prefill.FamilyPrefill, copied, clientReported: true);
		}
		return input;
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
		if (definitions == null)
		{
			var frozen = AdmissionEffectiveFormStore.Read(instance);
			if (frozen == null) AdmissionEffectiveFormStore.EnsureLegacyAllowed(await formRepository.GetAsync(instance.FormDefinitionId));
			definitions = frozen == null
				? await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId)
				: AdmissionEffectiveFormResolver.MaterializeFields(frozen.Definition);
		}
		values ??= await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var saves = await historyRepository.GetListAsync(item => item.FormInstanceId == instance.Id && (item.ChangeType == "DraftSaved" || item.ChangeType == "FormCorrected"));
		var editedCodes = saves.SelectMany(ReadAuthoredFieldCodes)
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
		var frozen = AdmissionEffectiveFormStore.Read(instance);
		var fields = frozen == null
			? await fieldRepository.GetListAsync(field => field.FormDefinitionId == instance.FormDefinitionId)
			: AdmissionEffectiveFormResolver.MaterializeFields(frozen.Definition);
		var cycle = await cycleRepository.FindAsync(application.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), application.AdmissionCycleId);
		var experience = frozen == null
			? await GetLegacyExperienceAsync(instance)
			: AdmissionEffectiveFormResolver.Experience(frozen.Definition);
		var sections = frozen == null
			? await sectionRepository.GetListAsync(section => section.FormDefinitionId == instance.FormDefinitionId)
			: AdmissionEffectiveFormResolver.MaterializeSections(frozen.Definition);
		var values = await valueRepository.GetListAsync(value => value.FormInstanceId == instance.Id);
		var valueMap = values.ToDictionary(
			value => value.FieldCode,
			value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson),
			StringComparer.OrdinalIgnoreCase);
		var validation = formValidator.Validate(fields, sections, valueMap, experience);
		await masterDataValidator.ValidateAsync(fields, sections, valueMap, cycle.AcademicYearId, validation, experience);
		AdmissionSubmissionIdentityValidator.Validate(await applicantRepository.GetAsync(application.ApplicantId), fields,
			frozen == null ? formValidator.GetVisibleFieldCodes(fields, sections, valueMap) :
				AdmissionEffectiveFormResolver.VisibleFieldCodes(frozen.Definition, valueMap, formValidator), valueMap, validation);
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

	private sealed record FormContext(AdmissionFormInstance Instance, AdmissionApplicant? Applicant);

	private async Task<FormContext> GetFormContextAsync(AdmissionApplication application)
	{
		if (application.Status != AdmissionApplicationStatus.Draft || application.SubmittedAt.HasValue)
			return new FormContext(await GetInstanceAsync(application.Id), null);
		var instances = await instanceRepository.GetQueryableAsync();
		var applicants = (await applicantRepository.GetQueryableAsync()).Where(applicant => applicant.Id == application.ApplicantId && applicant.TenantId == CurrentTenant.Id);
		var query = from instance in instances
			where instance.ApplicationId == application.Id
			join applicant in applicants on application.ApplicantId equals applicant.Id into targets
			from applicant in targets.DefaultIfEmpty()
			select new FormContext(instance, applicant);
		return await AsyncExecuter.FirstOrDefaultAsync(query) ?? throw new EntityNotFoundException(typeof(AdmissionFormInstance), application.Id);
	}

	private async Task<AdmissionFormExperienceMetadata> GetLegacyExperienceAsync(AdmissionFormInstance instance)
	{
		var definition = await formRepository.GetAsync(instance.FormDefinitionId);
		AdmissionEffectiveFormStore.EnsureLegacyAllowed(definition);
		return AdmissionFormMetadataStore.ReadExperience(definition);
	}

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
			CoreFieldPolicyVersion = experience.CoreFieldPolicyVersion,
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
