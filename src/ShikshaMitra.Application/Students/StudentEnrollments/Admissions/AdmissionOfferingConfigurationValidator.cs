using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferingConfigurationValidator
	: ApplicationService, ITransientDependency
{
	private readonly IRepository<AdmissionOffering, Guid> offeringRepository;
	private readonly IRepository<AdmissionApplication, Guid> applicationRepository;
	private readonly IRepository<AdmissionCampus, Guid> campusRepository;
	private readonly IRepository<AdmissionCycle, Guid> cycleRepository;
	private readonly IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository;
	private readonly IRepository<AdmissionFormDefinition, Guid> formRepository;
	private readonly IRepository<AdmissionIntakeProfile, Guid> intakeProfileRepository;
	private readonly IRepository<AdmissionWorkflowStageDefinition, Guid> workflowStageRepository;
	private readonly IEducationBoardRepository boardRepository;
	private readonly IEducationMediumRepository mediumRepository;
	private readonly IEducationMediumStageRepository stageRepository;
	private readonly IStandardRepository standardRepository;
	private readonly AcademicOrganizationScopeAccessService scopeAccessService;
	private readonly AdmissionEligibilityOfferingValidator eligibilityValidator;
	private readonly AdmissionInquiryOfferingValidator inquiryValidator;
	private readonly AdmissionReviewOfferingValidator reviewValidator;
	private readonly AdmissionOfferingWorkflowConfigurationResolver workflowConfiguration;

	public AdmissionOfferingConfigurationValidator(
		IRepository<AdmissionOffering, Guid> offeringRepository,
		IRepository<AdmissionApplication, Guid> applicationRepository,
		IRepository<AdmissionCampus, Guid> campusRepository,
		IRepository<AdmissionCycle, Guid> cycleRepository,
		IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
		IRepository<AdmissionFormDefinition, Guid> formRepository,
		IRepository<AdmissionIntakeProfile, Guid> intakeProfileRepository,
		IRepository<AdmissionIntakeSetup, Guid> intakeSetupRepository,
		IRepository<AdmissionWorkflowStageDefinition, Guid> workflowStageRepository,
		IEducationBoardRepository boardRepository,
		IEducationMediumRepository mediumRepository,
		IEducationMediumStageRepository stageRepository,
		IStandardRepository standardRepository,
		AcademicOrganizationScopeAccessService scopeAccessService,
		AdmissionEligibilityOfferingValidator eligibilityValidator,
		AdmissionInquiryOfferingValidator inquiryValidator,
		AdmissionInquiryPeopleService people,
		AdmissionReviewOfferingValidator reviewValidator,
		AdmissionOfferingWorkflowConfigurationResolver workflowConfiguration
	)
	{
		this.offeringRepository = offeringRepository;
		this.applicationRepository = applicationRepository;
		this.campusRepository = campusRepository;
		this.cycleRepository = cycleRepository;
		this.workflowRepository = workflowRepository;
		this.formRepository = formRepository;
		this.intakeProfileRepository = intakeProfileRepository;
		_ = intakeSetupRepository;
		this.workflowStageRepository = workflowStageRepository;
		this.boardRepository = boardRepository;
		this.mediumRepository = mediumRepository;
		this.stageRepository = stageRepository;
		this.standardRepository = standardRepository;
		this.scopeAccessService = scopeAccessService;
		this.eligibilityValidator = eligibilityValidator;
		this.inquiryValidator = inquiryValidator;
		_ = people;
		this.reviewValidator = reviewValidator;
		this.workflowConfiguration = workflowConfiguration;
	}

	private static readonly HashSet<string> IncompleteConfigurationCodes =
	[
		AdmissionOfferingValidationCodes.CycleUnavailable,
		AdmissionOfferingValidationCodes.WorkflowMissing,
		AdmissionOfferingValidationCodes.InquiryFormMissing,
		AdmissionOfferingValidationCodes.ApplicationFormMissing
		,AdmissionOfferingValidationCodes.InquiryIntakeProfileMissing
		,AdmissionOfferingValidationCodes.CounsellorAssignmentProfileMissing
	];

	public async Task<AdmissionOfferingValidationResult> ValidateAsync(
		Guid admissionCycleId,
		AdmissionOfferingWriteDto input,
		Guid? excludingOfferingId = null)
	{
		var existing = excludingOfferingId.HasValue ? await offeringRepository.FindAsync(excludingOfferingId.Value) : null;
		var preserveRouting = existing != null && await applicationRepository.AnyAsync(application => application.AdmissionOfferingId == existing.Id);
		await workflowConfiguration.ResolveAsync(input, existing, preserveRouting);
		var changedJourney = existing == null || existing.WorkflowDefinitionId != input.WorkflowDefinitionId ||
			existing.InquiryFormDefinitionId != input.InquiryFormDefinitionId || existing.ApplicationFormDefinitionId != input.ApplicationFormDefinitionId ||
			existing.EntryMode != input.EntryMode;
		return await ValidateCoreAsync(admissionCycleId, input, excludingOfferingId, enforceUserScope: true, validateJourneyIdentity: changedJourney);
	}

	internal Task<AdmissionOfferingValidationResult> ValidateResolvedAsync(Guid admissionCycleId, AdmissionOfferingWriteDto input) =>
		ValidateCoreAsync(admissionCycleId, input, null, enforceUserScope: true);

	private async Task<AdmissionOfferingValidationResult> ValidateCoreAsync(Guid admissionCycleId,
		AdmissionOfferingWriteDto input, Guid? excludingOfferingId, bool enforceUserScope, bool validateJourneyIdentity = true)
	{
		var errors = new List<AdmissionOfferingValidationError>();
		var cycle = await cycleRepository.FindAsync(admissionCycleId);
		if (cycle == null)
		{
			errors.Add(Error(AdmissionOfferingValidationCodes.CycleMissing, "Admission Cycle was not found."));
			return new AdmissionOfferingValidationResult(errors);
		}

		if (cycle.Status != AdmissionCycleStatus.Published)
		{
			errors.Add(Error(AdmissionOfferingValidationCodes.CycleUnavailable,
				"Admission Cycle must be published before this Offering can be activated."));
		}

		await ValidateHierarchyAsync(cycle, input, errors, enforceUserScope);
		ValidateDates(cycle, input, errors);
		if (input.Track == AdmissionTrack.Rte && input.EntryMode != AdmissionEntryMode.DirectRegistration)
			errors.Add(Error(AdmissionOfferingValidationCodes.RteRequiresDirectRegistration,
				"RTE offerings must start with Registration, without an Inquiry."));
		await ValidatePublishedConfigurationAsync(input, errors);
		if (validateJourneyIdentity) await ValidateJourneyIdentityAsync(input, errors);
		await ValidateAssessmentProfilesAsync(input, errors);
		await ValidateIntakeProfilesAsync(cycle, input, errors);
		errors.AddRange(await inquiryValidator.ValidateAsync(cycle, input));
		errors.AddRange(await reviewValidator.ValidateAsync(cycle, input));
		await ValidateUniquenessAsync(admissionCycleId, input, excludingOfferingId, errors);
		return new AdmissionOfferingValidationResult(errors);
	}

	public Task<AdmissionOfferingValidationResult> ValidateStoredAsync(AdmissionOffering offering) =>
		ValidateStoredCoreAsync(offering, enforceUserScope: true);

	public Task<AdmissionOfferingValidationResult> ValidateForActivationAsync(AdmissionOffering offering) =>
		ValidateStoredCoreAsync(offering, enforceUserScope: true, validateJourneyIdentity: true);

	// The public inquiry caller has already resolved the offering under its verified link's tenant and cycle.
	internal Task<AdmissionOfferingValidationResult> ValidateStoredForPublicAsync(AdmissionOffering offering) =>
		ValidateStoredCoreAsync(offering, enforceUserScope: false);

	private Task<AdmissionOfferingValidationResult> ValidateStoredCoreAsync(AdmissionOffering offering, bool enforceUserScope, bool validateJourneyIdentity = false) =>
		ValidateCoreAsync(offering.AdmissionCycleId, new CreateAdmissionOfferingDto
		{
			Name = offering.Name,
			Track = offering.Track,
			EntryMode = offering.EntryMode,
			CampusId = offering.CampusId,
			BoardId = offering.BoardId,
			MediumId = offering.MediumId,
			StageId = offering.StageId,
			StandardId = offering.StandardId,
			Code = offering.Code,
			UseCycleDates = !offering.OpenDate.HasValue && !offering.CloseDate.HasValue,
			OpenDate = offering.OpenDate,
			CloseDate = offering.CloseDate,
			WorkflowDefinitionId = offering.WorkflowDefinitionId,
			InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
			ApplicationFormDefinitionId = offering.ApplicationFormDefinitionId,
			InquiryIntakeProfileId = offering.InquiryIntakeProfileId,
			CounsellorAssignmentProfileId = offering.CounsellorAssignmentProfileId,
			IsActive = offering.IsActive
		}, offering.Id, enforceUserScope, validateJourneyIdentity);

	public static void EnsureCanSave(AdmissionOfferingValidationResult validation, bool isActive)
	{
		var blockingErrors = GetBlockingErrors(validation, isActive);
		if (blockingErrors.Count == 0)
		{
			return;
		}

		var first = blockingErrors[0];
		throw new BusinessException(ToBusinessErrorCode(first.Code), first.Message)
			.WithData("ValidationErrors", blockingErrors.Select(error => error.Code).Distinct().ToArray())
			.WithData("ValidationMessages", blockingErrors.Select(error => error.Message).Distinct().ToArray());
	}

	public static IReadOnlyList<AdmissionOfferingValidationError> GetBlockingErrors(
		AdmissionOfferingValidationResult validation,
		bool isActive) => validation.Errors
			.Where(error => isActive || (!error.ActivationOnly && !IncompleteConfigurationCodes.Contains(error.Code)))
			.ToList();

	public static bool CanSave(AdmissionOfferingValidationResult validation, bool isActive) =>
		GetBlockingErrors(validation, isActive).Count == 0;

	private async Task ValidateHierarchyAsync(
		AdmissionCycle cycle,
		AdmissionOfferingWriteDto input,
		ICollection<AdmissionOfferingValidationError> errors, bool enforceUserScope)
	{
		if (input.CampusId.HasValue)
		{
			var campus = await campusRepository.FindAsync(input.CampusId.Value);
			if (campus == null)
			{
				errors.Add(Error(AdmissionOfferingValidationCodes.CampusMissing,
					"The selected Campus was not found in this tenant."));
			}
			else if (!campus.IsActive)
			{
				errors.Add(Error(AdmissionOfferingValidationCodes.CampusInactive,
					"The selected Campus is inactive."));
			}
		}

		var board = await boardRepository.FindAsync(input.BoardId);
		var medium = await mediumRepository.FindAsync(input.MediumId);
		var stage = await stageRepository.FindAsync(input.StageId);
		var standard = await standardRepository.FindAsync(input.StandardId);
		if (board == null || medium == null || stage == null || standard == null ||
			board.AcademicYearId != cycle.AcademicYearId || medium.EducationBoardId != board.Id ||
			stage.EducationMediumId != medium.Id || standard.EducationMediumStageId != stage.Id)
		{
			errors.Add(Error(AdmissionOfferingValidationCodes.HierarchyInvalid,
				"The selected Board, Medium, Stage, and Standard do not form a valid hierarchy for this Admission Cycle."));
			return;
		}

		if (!enforceUserScope) return;
		var canAccessStage = await scopeAccessService.CanAccessMediumStageAsync(
			stage.Id, medium.Id, board.Id, cycle.AcademicYearId);
		var visibleStandards = await scopeAccessService.FilterStandardsAsync([standard]);
		if (!canAccessStage || visibleStandards.All(item => item.Id != standard.Id))
		{
			errors.Add(Error(AdmissionOfferingValidationCodes.HierarchyOutOfScope,
				"You do not have access to configure an Admission Offering for the selected hierarchy."));
		}
	}

	private static void ValidateDates(
		AdmissionCycle cycle,
		AdmissionOfferingWriteDto input,
		ICollection<AdmissionOfferingValidationError> errors)
	{
		if (input.UseCycleDates)
		{
			return;
		}

		if (!input.OpenDate.HasValue || !input.CloseDate.HasValue ||
			input.OpenDate.Value.Date > input.CloseDate.Value.Date ||
			input.OpenDate.Value.Date < cycle.StartDate.Date ||
			input.CloseDate.Value.Date > cycle.EndDate.Date)
		{
			errors.Add(Error(AdmissionOfferingValidationCodes.DateRangeInvalid,
				"Offering dates must be valid and fall inside the Admission Cycle dates."));
		}
	}

	private async Task ValidatePublishedConfigurationAsync(
		AdmissionOfferingWriteDto input,
		ICollection<AdmissionOfferingValidationError> errors)
	{
		if (!input.WorkflowDefinitionId.HasValue)
		{
			errors.Add(Error(AdmissionOfferingValidationCodes.WorkflowMissing, "Published Admission Workflow is required."));
		}
		else
		{
			var workflow = await workflowRepository.FindAsync(input.WorkflowDefinitionId.Value);
			if (workflow == null || !workflow.Status.CanConfigureOffering())
			{
				errors.Add(Error(AdmissionOfferingValidationCodes.WorkflowNotPublished,
					"The selected Admission Workflow must be a published version."));
			}
		}

		await ValidateFormAsync(input.InquiryFormDefinitionId,
			AdmissionOfferingValidationCodes.InquiryFormMissing,
			AdmissionOfferingValidationCodes.InquiryFormNotPublished,
			AdmissionOfferingValidationCodes.InquiryFormPurposeMismatch,
			AdmissionFormPurpose.Inquiry, "Inquiry Form", errors,
			required: input.EntryMode is AdmissionEntryMode.Inquiry or AdmissionEntryMode.Both);
		await ValidateFormAsync(input.ApplicationFormDefinitionId,
			AdmissionOfferingValidationCodes.ApplicationFormMissing,
			AdmissionOfferingValidationCodes.ApplicationFormNotPublished,
			AdmissionOfferingValidationCodes.ApplicationFormPurposeMismatch,
			AdmissionFormPurpose.Registration, "Application Form", errors);
		await ValidateWorkflowFormCompatibilityAsync(input.WorkflowDefinitionId, input.ApplicationFormDefinitionId, errors);
		await ValidateEntryModeWorkflowCompatibilityAsync(input.WorkflowDefinitionId, input.Track, input.EntryMode, errors);
		await ValidateInheritanceCompatibilityAsync(input, errors);
		foreach (var error in await eligibilityValidator.ValidateAsync(
			input.WorkflowDefinitionId, input.ApplicationFormDefinitionId, input.InquiryFormDefinitionId, input.EntryMode))
			errors.Add(error);
	}

	private async Task ValidateInheritanceCompatibilityAsync(AdmissionOfferingWriteDto input, ICollection<AdmissionOfferingValidationError> errors)
	{
		if (!input.ApplicationFormDefinitionId.HasValue) return;
		var registration = await formRepository.FindAsync(input.ApplicationFormDefinitionId.Value);
		if (registration == null) return;
		var policy = AdmissionFormInheritanceMetadataStore.ReadPolicy(registration);
		if (policy?.Enabled != true) return;
		if (input.InquiryFormDefinitionId.HasValue && policy.Sources.All(source => source.InquiryFormDefinitionId != input.InquiryFormDefinitionId.Value))
			errors.Add(Error(AdmissionFormInheritanceErrorCodes.SourceNotApproved,
				"Review and approve this Inquiry version in the workflow's Registration form before activating the Offering.", activationOnly: true));
		if (input.EntryMode is AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both)
		{
			var fieldRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>();
			if (!await fieldRepository.AnyAsync(field => field.FormDefinitionId == registration.Id && field.FieldType != AdmissionFormFieldType.Static))
				errors.Add(Error(AdmissionFormInheritanceErrorCodes.IncompatibleSource,
					"Direct Registration needs questions in its Registration form because no Inquiry supplies inherited questions.", activationOnly: true));
		}
	}

	private async Task ValidateJourneyIdentityAsync(AdmissionOfferingWriteDto input, ICollection<AdmissionOfferingValidationError> errors)
	{
		if (!input.ApplicationFormDefinitionId.HasValue) return;
		var registrationEntity = await formRepository.FindAsync(input.ApplicationFormDefinitionId.Value);
		if (registrationEntity is not { Status: AdmissionFormStatus.Published, Purpose: AdmissionFormPurpose.Registration }) return;
		var inheritance = LazyServiceProvider.LazyGetRequiredService<AdmissionFormInheritanceService>();
		var registration = await inheritance.ReadDefinitionAsync(registrationEntity.Id);
		var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (input.EntryMode is AdmissionEntryMode.Inquiry or AdmissionEntryMode.Both && input.InquiryFormDefinitionId.HasValue)
		{
			var inquiryEntity = await formRepository.FindAsync(input.InquiryFormDefinitionId.Value);
			if (inquiryEntity is { Status: AdmissionFormStatus.Published, Purpose: AdmissionFormPurpose.Inquiry })
			{
				var inquiry = await inheritance.ReadDefinitionAsync(inquiryEntity.Id);
				var effective = registration;
				if (registration.Inheritance?.Enabled == true)
				{
					var preview = AdmissionFormInheritanceService.Compose(registration, inquiry, requireApprovedSource: true);
					if (!preview.IsValid)
					{
						errors.Add(Error(AdmissionFormInheritanceErrorCodes.IncompatibleSource,
							string.Join("; ", preview.Issues.Select(issue => issue.Message)), activationOnly: true));
						return;
					}
					effective = preview.Definition;
				}
				missing.UnionWith(AdmissionJourneyIdentityConfiguration.MissingBindings(ToSchema(effective), InquiryIdentitySchema(inquiry)));
			}
		}
		if (input.EntryMode is AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both)
			missing.UnionWith(AdmissionJourneyIdentityConfiguration.MissingBindings(ToSchema(registration), null,
				AdmissionJourneyIdentityConfiguration.DirectEntryCapturedBindings));
		if (missing.Count > 0)
			errors.Add(Error(AdmissionOfferingValidationCodes.ApplicationFormIdentityIncomplete,
				"Before activating this Offering, configure required, reachable student identity fields across its Inquiry and Registration journey: " +
				string.Join(", ", AdmissionJourneyIdentityConfiguration.CoreBindings.Where(missing.Contains)) + ". A full-name question does not replace separate first name and surname fields. Keep a full-name reference read-only when collecting separate name parts.", activationOnly: true));
	}

	private static AdmissionFormSchema InquiryIdentitySchema(AdmissionFormDto inquiry)
	{
		var schema = ToSchema(inquiry);
		var fields = AdmissionEffectiveFormResolver.MaterializeFields(inquiry);
		foreach (var key in AdmissionJourneyIdentityConfiguration.CoreBindings)
		{
			var captured = AdmissionInquiryConfiguredFields.Find(fields, key);
			// Inquiry's historical structured name and DOB aliases are still read by its
			// capture service. Arbitrary custom questions (including Gender aliases) are not.
			if (captured == null || key == "Applicant.Gender" &&
				(captured.BindingType != AdmissionFormBindingType.System || captured.BindingKey != key)) continue;
			var source = schema.Sections.SelectMany(section => section.Fields).Single(field => field.Id == captured.Id);
			source.BindingType = AdmissionFormBindingType.System;
			source.BindingKey = key;
		}
		return schema;
	}

	private static AdmissionFormSchema ToSchema(AdmissionFormDto form) =>
		JsonSerializer.Deserialize<AdmissionFormSchema>(JsonSerializer.Serialize(form))!;

	private async Task ValidateAssessmentProfilesAsync(AdmissionOfferingWriteDto input, ICollection<AdmissionOfferingValidationError> errors)
	{
		if (!input.WorkflowDefinitionId.HasValue) return;
		var assessmentStages = await workflowStageRepository.GetListAsync(item => item.WorkflowDefinitionId == input.WorkflowDefinitionId.Value && item.StageType == AdmissionWorkflowStageType.Assessment);
		foreach (var stage in assessmentStages)
		{
			try { _ = AdmissionAssessmentPolicy.Read(stage.ConfigurationJson)?.Resolve(input.BoardId, input.MediumId, input.StandardId); }
			catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
			{
				errors.Add(Error(AdmissionOfferingValidationCodes.AssessmentProfileMissing, $"Stage '{stage.Name}': {exception.Message}"));
			}
		}
	}

	private Task ValidateIntakeProfilesAsync(AdmissionCycle cycle, AdmissionOfferingWriteDto input,
		ICollection<AdmissionOfferingValidationError> errors)
	{
		return Task.CompletedTask;
	}

	private async Task ValidateIntakeProfileAsync(Guid? id, AdmissionIntakeProfileKind kind,
		string missingCode, string invalidCode, ICollection<AdmissionOfferingValidationError> errors)
	{
		if (!id.HasValue)
		{
			errors.Add(Error(missingCode, $"Select a {kind} profile."));
			return;
		}
		var profile = await intakeProfileRepository.FindAsync(id.Value);
		if (profile == null || profile.Kind != kind)
			errors.Add(Error(invalidCode, $"The selected {kind} profile is unavailable."));
	}

	private async Task ValidateEntryModeWorkflowCompatibilityAsync(
		Guid? workflowId,
		AdmissionTrack track,
		AdmissionEntryMode entryMode,
		ICollection<AdmissionOfferingValidationError> errors)
	{
		if (!workflowId.HasValue || (entryMode is not (AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both) && track != AdmissionTrack.Rte)) return;
		var stages = await workflowStageRepository.GetListAsync(stage =>
			stage.WorkflowDefinitionId == workflowId.Value);
		if (entryMode is AdmissionEntryMode.DirectRegistration or AdmissionEntryMode.Both)
		{
			if (stages.Any(stage => stage.StageType == AdmissionWorkflowStageType.Inquiry))
				errors.Add(Error(AdmissionOfferingValidationCodes.DirectWorkflowContainsInquiry,
					"A direct Registration Offering must use a Workflow without an Inquiry stage."));
			if (stages.FirstOrDefault(stage => stage.IsEntryStage)?.StageType != AdmissionWorkflowStageType.Form)
				errors.Add(Error(AdmissionOfferingValidationCodes.DirectWorkflowMustStartWithForm,
					"A direct Registration Offering must start at the Registration Form stage."));
		}
		if (track == AdmissionTrack.Rte && stages.Any(stage => stage.StageType == AdmissionWorkflowStageType.Assessment))
			errors.Add(Error(AdmissionOfferingValidationCodes.RteWorkflowContainsAssessment,
				"RTE offerings cannot use a Workflow with an Entrance Assessment stage."));
	}

	private async Task ValidateWorkflowFormCompatibilityAsync(
		Guid? workflowId,
		Guid? formId,
		ICollection<AdmissionOfferingValidationError> errors)
	{
		if (!workflowId.HasValue) return;
		var stages = await workflowStageRepository.GetListAsync(stage =>
			stage.WorkflowDefinitionId == workflowId.Value && stage.StageType == AdmissionWorkflowStageType.Form);
		var configuredForms = stages.Select(stage => AdmissionFormStageAccessService.ReadFormDefinitionId(stage.ConfigurationJson))
			.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
		if (configuredForms.Length > 1 || formId.HasValue && configuredForms.Any(id => id != formId.Value))
			errors.Add(Error(AdmissionOfferingValidationCodes.WorkflowFormMismatch,
				"Every Form stage in the workflow must use the same published Registration Form version."));
	}

	private async Task ValidateFormAsync(
		Guid? formId,
		string missingCode,
		string unpublishedCode,
		string purposeMismatchCode,
		AdmissionFormPurpose purpose,
		string label,
		ICollection<AdmissionOfferingValidationError> errors,
		bool required = true)
	{
		if (!formId.HasValue)
		{
			if (required) errors.Add(Error(missingCode, purpose == AdmissionFormPurpose.Registration
				? "Select a published Registration Form in the workflow's Form stage before activating this offering."
				: $"Published {label} is required."));
			return;
		}

		var form = await formRepository.FindAsync(formId.Value);
		if (form == null || form.Status != AdmissionFormStatus.Published)
		{
			errors.Add(Error(unpublishedCode, $"The selected {label} must be a published version."));
		}
		else if (form.Purpose != purpose)
		{
			errors.Add(Error(purposeMismatchCode,
				$"The selected {label} must have the {purpose} purpose."));
		}
	}

	private async Task ValidateUniquenessAsync(
		Guid admissionCycleId,
		AdmissionOfferingWriteDto input,
		Guid? excludingOfferingId,
		ICollection<AdmissionOfferingValidationError> errors)
	{
		var query = await offeringRepository.GetQueryableAsync();
		var duplicate = await AsyncExecuter.AnyAsync(query.Where(offering =>
			offering.AdmissionCycleId == admissionCycleId && offering.CampusId == input.CampusId &&
			offering.BoardId == input.BoardId && offering.MediumId == input.MediumId &&
			offering.StageId == input.StageId && offering.StandardId == input.StandardId &&
			offering.Track == input.Track &&
			(!excludingOfferingId.HasValue || offering.Id != excludingOfferingId.Value)));
		if (duplicate)
		{
			errors.Add(Error(AdmissionOfferingValidationCodes.Duplicate,
				"An Admission Offering already exists for this Cycle, class, and admission track."));
		}
	}

	private static AdmissionOfferingValidationError Error(string code, string message, bool activationOnly = false) =>
		new(code, message, activationOnly);

	private static string ToBusinessErrorCode(string validationCode) => validationCode switch
	{
		AdmissionOfferingValidationCodes.EligibilityFormMismatch => AdmissionEligibilityErrorCodes.OfferingFormMismatch,
		AdmissionOfferingValidationCodes.WorkflowFormMismatch => AdmissionFormStageErrorCodes.OfferingFormMismatch,
		AdmissionOfferingValidationCodes.AssessmentProfileMissing => AdmissionAssessmentErrorCodes.InvalidPolicy,
		AdmissionOfferingValidationCodes.InquiryCounsellorUnavailable => AdmissionErrorCodes.InquiryCounsellorInvalid,
		AdmissionOfferingValidationCodes.ReviewAssigneeUnavailable => AdmissionReviewErrorCodes.ReviewerNotAllowed,
		AdmissionOfferingValidationCodes.HierarchyInvalid => AdmissionErrorCodes.OfferingAcademicHierarchyInvalid,
		AdmissionOfferingValidationCodes.HierarchyOutOfScope => AdmissionErrorCodes.OfferingHierarchyOutOfScope,
		AdmissionOfferingValidationCodes.DateRangeInvalid => AdmissionErrorCodes.OfferingDateRangeInvalid,
		AdmissionOfferingValidationCodes.Duplicate => AdmissionErrorCodes.OfferingDuplicate,
		AdmissionOfferingValidationCodes.WorkflowNotPublished => AdmissionErrorCodes.OfferingWorkflowNotPublished,
		AdmissionOfferingValidationCodes.InquiryFormNotPublished or AdmissionOfferingValidationCodes.ApplicationFormNotPublished => AdmissionErrorCodes.OfferingFormNotPublished,
		AdmissionOfferingValidationCodes.CampusMissing or AdmissionOfferingValidationCodes.CampusInactive => AdmissionErrorCodes.OfferingCampusInvalid,
		AdmissionOfferingValidationCodes.CycleMissing => AdmissionErrorCodes.OfferingCycleNotFound,
		_ => AdmissionErrorCodes.OfferingConfigurationIncomplete
	};
}
