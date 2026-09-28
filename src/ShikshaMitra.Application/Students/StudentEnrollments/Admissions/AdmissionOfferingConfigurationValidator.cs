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

public class AdmissionOfferingConfigurationValidator(
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCampus, Guid> campusRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionIntakeProfile, Guid> intakeProfileRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> workflowStageRepository,
	IEducationBoardRepository boardRepository,
	IEducationMediumRepository mediumRepository,
	IEducationMediumStageRepository stageRepository,
	IStandardRepository standardRepository,
	AcademicOrganizationScopeAccessService scopeAccessService,
	AdmissionEligibilityOfferingValidator eligibilityValidator,
	AdmissionInquiryOfferingValidator inquiryValidator,
	AdmissionInquiryPeopleService people,
	AdmissionReviewOfferingValidator reviewValidator)
	: ApplicationService, ITransientDependency
{
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

		await ValidateHierarchyAsync(cycle, input, errors);
		ValidateDates(cycle, input, errors);
		if (input.Track == AdmissionTrack.Rte && input.EntryMode != AdmissionEntryMode.DirectRegistration)
			errors.Add(Error(AdmissionOfferingValidationCodes.RteRequiresDirectRegistration,
				"RTE offerings must start with Registration, without an Inquiry."));
		await ValidatePublishedConfigurationAsync(input, errors);
		await ValidateIntakeProfilesAsync(cycle, input, excludingOfferingId, errors);
		errors.AddRange(await inquiryValidator.ValidateAsync(cycle, input));
		errors.AddRange(await reviewValidator.ValidateAsync(cycle, input));
		await ValidateUniquenessAsync(admissionCycleId, input, excludingOfferingId, errors);
		return new AdmissionOfferingValidationResult(errors);
	}

	public Task<AdmissionOfferingValidationResult> ValidateStoredAsync(AdmissionOffering offering) =>
		ValidateAsync(offering.AdmissionCycleId, new CreateAdmissionOfferingDto
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
		}, offering.Id);

	public static void EnsureCanSave(AdmissionOfferingValidationResult validation, bool isActive)
	{
		var blockingErrors = GetBlockingErrors(validation, isActive);
		if (blockingErrors.Count == 0)
		{
			return;
		}

		var first = blockingErrors[0];
		throw new BusinessException(ToBusinessErrorCode(first.Code), first.Message)
			.WithData("ValidationErrors", blockingErrors.Select(error => error.Code).ToArray());
	}

	public static IReadOnlyList<AdmissionOfferingValidationError> GetBlockingErrors(
		AdmissionOfferingValidationResult validation,
		bool isActive) => validation.Errors
			.Where(error => isActive || !IncompleteConfigurationCodes.Contains(error.Code))
			.ToList();

	public static bool CanSave(AdmissionOfferingValidationResult validation, bool isActive) =>
		GetBlockingErrors(validation, isActive).Count == 0;

	private async Task ValidateHierarchyAsync(
		AdmissionCycle cycle,
		AdmissionOfferingWriteDto input,
		ICollection<AdmissionOfferingValidationError> errors)
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
		foreach (var error in await eligibilityValidator.ValidateAsync(
			input.WorkflowDefinitionId, input.ApplicationFormDefinitionId, input.InquiryFormDefinitionId, input.EntryMode))
			errors.Add(error);
	}

	private async Task ValidateIntakeProfilesAsync(AdmissionCycle cycle, AdmissionOfferingWriteDto input,
		Guid? existingOfferingId,
		ICollection<AdmissionOfferingValidationError> errors)
	{
		// Existing offerings retain their published workflow behavior until an admin opts in.
		var usingProfiles = input.InquiryIntakeProfileId.HasValue || input.CounsellorAssignmentProfileId.HasValue;
		if (!usingProfiles && existingOfferingId.HasValue &&
			(await offeringRepository.FindAsync(existingOfferingId.Value))?.IntakeConfigurationRequired != true) return;
		if (input.EntryMode is AdmissionEntryMode.Inquiry or AdmissionEntryMode.Both)
			await ValidateIntakeProfileAsync(input.InquiryIntakeProfileId, AdmissionIntakeProfileKind.Inquiry,
				AdmissionOfferingValidationCodes.InquiryIntakeProfileMissing,
				AdmissionOfferingValidationCodes.InquiryIntakeProfileInvalid, errors);
		else if (input.InquiryIntakeProfileId.HasValue)
			errors.Add(Error(AdmissionOfferingValidationCodes.InquiryIntakeProfileInvalid,
				"A direct-only offering cannot use an Inquiry profile."));
		await ValidateIntakeProfileAsync(input.CounsellorAssignmentProfileId, AdmissionIntakeProfileKind.CounsellorAssignment,
			AdmissionOfferingValidationCodes.CounsellorAssignmentProfileMissing,
			AdmissionOfferingValidationCodes.CounsellorAssignmentProfileInvalid, errors);
		if (input.CounsellorAssignmentProfileId.HasValue)
		{
			var profile = await intakeProfileRepository.FindAsync(input.CounsellorAssignmentProfileId.Value);
			if (profile?.Kind == AdmissionIntakeProfileKind.CounsellorAssignment)
			{
				try
				{
					var rules = JsonSerializer.Deserialize<AdmissionCounsellorAssignmentRules>(profile.ConfigurationJson,
						new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
						throw new ArgumentException("Assignment settings are missing.");
					rules.Validate();
					var eligible = false;
					foreach (var userId in rules.CounsellorUserIds)
						if (await people.CanAccessHierarchyAsync(userId, cycle, input)) { eligible = true; break; }
					if (!eligible)
						errors.Add(Error(AdmissionOfferingValidationCodes.CounsellorAssignmentProfileInvalid,
							"Select an active counsellor who has access to this class in the assignment profile."));
					if (rules.FallbackManagerUserId.HasValue &&
						!await people.CanAccessHierarchyAsync(rules.FallbackManagerUserId.Value, cycle, input))
						errors.Add(Error(AdmissionOfferingValidationCodes.CounsellorAssignmentProfileInvalid,
							"The fallback manager in the assignment profile must be active and have access to this class."));
				}
				catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
				{
					errors.Add(Error(AdmissionOfferingValidationCodes.CounsellorAssignmentProfileInvalid,
							"The assignment profile has invalid rules."));
				}
			}
		}
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
		if (!workflowId.HasValue || !formId.HasValue) return;
		var stages = await workflowStageRepository.GetListAsync(stage =>
			stage.WorkflowDefinitionId == workflowId.Value && stage.StageType == AdmissionWorkflowStageType.Form);
		if (stages.Select(stage => AdmissionFormStageAccessService.ReadFormDefinitionId(stage.ConfigurationJson))
			.Any(configured => configured.HasValue && configured.Value != formId.Value))
			errors.Add(Error(AdmissionOfferingValidationCodes.WorkflowFormMismatch,
				"Every Form stage in the workflow must use the same published Application Form selected for this offering."));
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
			if (required) errors.Add(Error(missingCode, $"Published {label} is required."));
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

	private static AdmissionOfferingValidationError Error(string code, string message) => new(code, message);

	private static string ToBusinessErrorCode(string validationCode) => validationCode switch
	{
		AdmissionOfferingValidationCodes.EligibilityFormMismatch => AdmissionEligibilityErrorCodes.OfferingFormMismatch,
		AdmissionOfferingValidationCodes.WorkflowFormMismatch => AdmissionFormStageErrorCodes.OfferingFormMismatch,
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
