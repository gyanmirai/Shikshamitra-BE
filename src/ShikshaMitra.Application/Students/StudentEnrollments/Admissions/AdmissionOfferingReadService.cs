using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferingReadService(
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCampus, Guid> campusRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionIntakeProfile, Guid> intakeProfileRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IUserScopeAccessService scopeAccessService,
	AdmissionEligibilityOfferingValidator eligibilityValidator,
	AdmissionOfferingConfigurationValidator configurationValidator,
	AdmissionClassCapacityAppService classCapacityService,
	AdmissionInquiryAvailabilityService inquiryAvailability)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionOfferingListDto>> GetListAsync(AdmissionOfferingListInput input)
	{
		var read = await GetRowsQueryAsync(input);
		var query = read.Query;
		var totalCount = await AsyncExecuter.CountAsync(query);
		var pageRows = await AsyncExecuter.ToListAsync(
			ApplySorting(query, input.Sorting)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionOfferingListDto>(
			totalCount,
			pageRows.Select(row => MapList(row, read.Errors.GetValueOrDefault(row.Offering.Id))).ToList());
	}

	public async Task<AdmissionOfferingDto> GetAsync(Guid id)
	{
		var read = await GetRowsQueryAsync(new AdmissionOfferingListInput
		{
			MaxResultCount = 1,
			SkipCount = 0
		}, id);
		var rows = await AsyncExecuter.ToListAsync(read.Query);
		var row = rows.SingleOrDefault() ?? throw new EntityNotFoundException(typeof(AdmissionOffering), id);
		var detail = MapDetail(row, read.Errors.GetValueOrDefault(row.Offering.Id));
		var validation = await configurationValidator.ValidateStoredAsync(row.Offering);
		detail.Configuration = MapValidation(validation);
		detail.ConfigurationStatus = validation.Status;
		detail.AvailabilityStatus = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			row.Offering, row.Cycle, validation.IsValid, new DateTimeOffset(Clock.Now.ToUniversalTime())).Status;
		detail.ClassCapacity = await classCapacityService.ReadForOfferingAsync(id);
		return detail;
	}

	public async Task<AdmissionOfferingAvailabilityDto> GetAvailabilityAsync(Guid id)
	{
		var detail = await GetAsync(id);
		var offering = await AsyncExecuter.FirstAsync(
			(await offeringRepository.GetQueryableAsync()).Where(item => item.Id == id));
		var intake = await inquiryAvailability.ReadAsync(offering);
		return new AdmissionOfferingAvailabilityDto
		{
			ClassCapacity = detail.ClassCapacity,
			IsAvailable = detail.AvailabilityStatus == AdmissionOfferingAvailabilityStatus.Open,
			Status = detail.AvailabilityStatus,
			CanCreateInquiry = intake.CanCreateInquiry,
			CanStartApplication = detail.AvailabilityStatus == AdmissionOfferingAvailabilityStatus.Open,
			ReasonCode = detail.Configuration.IsValid ? StatusReasonCode(detail.AvailabilityStatus) : AdmissionErrorCodes.OfferingConfigurationIncomplete,
			ReasonMessage = intake.Reason ?? (detail.Configuration.IsValid ? StatusReasonMessage(detail.AvailabilityStatus) :
				detail.Configuration.Errors.FirstOrDefault()?.Message)
		};
	}

	private async Task<AdmissionOfferingReadQuery> GetRowsQueryAsync(
		AdmissionOfferingListInput input,
		Guid? offeringId = null)
	{
		var offerings = await offeringRepository.GetQueryableAsync();
		var campuses = await campusRepository.GetQueryableAsync();
		var cycles = await cycleRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();
		var mediums = await mediumRepository.GetQueryableAsync();
		var stages = await stageRepository.GetQueryableAsync();
		var standards = await standardRepository.GetQueryableAsync();
		var workflows = await workflowRepository.GetQueryableAsync();
		var forms = await formRepository.GetQueryableAsync();
		var intakeProfiles = await intakeProfileRepository.GetQueryableAsync();
		var applications = await applicationRepository.GetQueryableAsync();
		var divisions = await divisionRepository.GetQueryableAsync();
		var scope = await scopeAccessService.GetCurrentUserScopeAsync();
		offerings = AdmissionOfferingScopeQuery.Apply(offerings.Where(offering => offering.TenantId == CurrentTenant.Id), cycles, divisions, scope);

		var query =
			from offering in offerings
			join campusValue in campuses on offering.CampusId equals campusValue.Id into campusGroup
			from campus in campusGroup.DefaultIfEmpty()
			join cycle in cycles on offering.AdmissionCycleId equals cycle.Id
			join board in boards on offering.BoardId equals board.Id
			join medium in mediums on offering.MediumId equals medium.Id
			join stage in stages on offering.StageId equals stage.Id
			join standard in standards on offering.StandardId equals standard.Id
			join workflowValue in workflows on offering.WorkflowDefinitionId equals workflowValue.Id into workflowGroup
			from workflow in workflowGroup.DefaultIfEmpty()
			join inquiryFormValue in forms on offering.InquiryFormDefinitionId equals inquiryFormValue.Id into inquiryFormGroup
			from inquiryForm in inquiryFormGroup.DefaultIfEmpty()
			join applicationFormValue in forms on offering.ApplicationFormDefinitionId equals applicationFormValue.Id into applicationFormGroup
			from applicationForm in applicationFormGroup.DefaultIfEmpty()
			join inquiryProfileValue in intakeProfiles on offering.InquiryIntakeProfileId equals inquiryProfileValue.Id into inquiryProfileGroup
			from inquiryProfile in inquiryProfileGroup.DefaultIfEmpty()
			join assignmentProfileValue in intakeProfiles on offering.CounsellorAssignmentProfileId equals assignmentProfileValue.Id into assignmentProfileGroup
			from assignmentProfile in assignmentProfileGroup.DefaultIfEmpty()
			where (!offeringId.HasValue || offering.Id == offeringId.Value) &&
				(!input.AdmissionCycleId.HasValue || offering.AdmissionCycleId == input.AdmissionCycleId.Value) &&
				(!input.Track.HasValue || offering.Track == input.Track.Value) &&
				(!input.EntryMode.HasValue || offering.EntryMode == input.EntryMode.Value) &&
				(!input.CampusId.HasValue || offering.CampusId == input.CampusId.Value) &&
				(!input.BoardId.HasValue || offering.BoardId == input.BoardId.Value) &&
				(!input.MediumId.HasValue || offering.MediumId == input.MediumId.Value) &&
				(!input.StageId.HasValue || offering.StageId == input.StageId.Value) &&
				(!input.StandardId.HasValue || offering.StandardId == input.StandardId.Value) &&
				(input.StandardIds.Count == 0 || Enumerable.Contains(input.StandardIds, offering.StandardId)) &&
				(!input.IsActive.HasValue || offering.IsActive == input.IsActive.Value) &&
				(string.IsNullOrWhiteSpace(input.Search) || offering.Name.Contains(input.Search) || offering.Code.Contains(input.Search) ||
					cycle.Name.Contains(input.Search) || (campus != null && campus.Name.Contains(input.Search)) || medium.Name.Contains(input.Search) ||
					stage.Name.Contains(input.Search) || standard.Name.Contains(input.Search))
			select new AdmissionOfferingReadRow
			{
				Offering = offering,
				Campus = campus,
				Cycle = cycle,
				// Scalar academic columns avoid the model's recursive hierarchy AutoIncludes.
				BoardId = board.Id,
				BoardAcademicYearId = board.AcademicYearId,
				BoardType = board.Type,
				MediumId = medium.Id,
				MediumBoardId = medium.EducationBoardId,
				MediumName = medium.Name,
				StageId = stage.Id,
				StageMediumId = stage.EducationMediumId,
				StageName = stage.Name,
				StandardId = standard.Id,
				StandardStageId = standard.EducationMediumStageId,
				StandardName = standard.Name,
				Workflow = workflow,
				InquiryForm = inquiryForm,
				ApplicationForm = applicationForm,
				InquiryProfile = inquiryProfile,
				AssignmentProfile = assignmentProfile,
				ApplicationCount = applications.Count(application => application.AdmissionOfferingId == offering.Id)
			};

		// Filter before Count/Skip/Take so inquiry-first rows cannot hide eligible later pages.
		if (input.ForSpecialRegistration)
		{
			query = query.Where(row =>
				(row.Offering.Track == AdmissionTrack.Rte && row.Offering.EntryMode == AdmissionEntryMode.DirectRegistration) ||
				((row.Offering.Track == AdmissionTrack.Management || row.Offering.Track == AdmissionTrack.Staff) &&
					(row.Offering.EntryMode == AdmissionEntryMode.DirectRegistration || row.Offering.EntryMode == AdmissionEntryMode.Both)));
		}

		var eligibilityErrors = await eligibilityValidator.GetErrorsAsync(query.Select(row => row.Offering));
		// Availability uses the institution's calendar day, including before 05:30 IST.
		var today = DateTime.SpecifyKind(AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now.ToUniversalTime()), DateTimeKind.Utc);
		query = ApplyDerivedFilters(query, input, today, eligibilityErrors.Keys.ToList());

		return new(query, eligibilityErrors);
	}

	private static IQueryable<AdmissionOfferingReadRow> ApplyDerivedFilters(
		IQueryable<AdmissionOfferingReadRow> query,
		AdmissionOfferingListInput input,
		DateTime today,
		List<Guid> incompatibleOfferingIds)
	{
		var ready = ApplyConfigurationReadyFilter(query, incompatibleOfferingIds);
		var incomplete = ApplyConfigurationIncompleteFilter(query, incompatibleOfferingIds);

		if (input.ConfigurationStatus == AdmissionOfferingConfigurationStatus.Ready)
			query = ready;
		else if (input.ConfigurationStatus == AdmissionOfferingConfigurationStatus.Incomplete)
			query = incomplete;

		if (!input.AvailabilityStatus.HasValue)
			return query;

		var status = input.AvailabilityStatus.Value;

		query = status switch
		{
			AdmissionOfferingAvailabilityStatus.Inactive => query.Where(row => !row.Offering.IsActive),
			AdmissionOfferingAvailabilityStatus.ConfigurationIncomplete => ApplyConfigurationIncompleteFilter(query, incompatibleOfferingIds)
				.Where(row => row.Offering.IsActive),
			AdmissionOfferingAvailabilityStatus.Open => ApplyConfigurationReadyFilter(query, incompatibleOfferingIds)
				.Where(row => row.Offering.IsActive)
				.Where(row =>
					(row.Offering.OpenDate.HasValue && row.Offering.OpenDate.Value.Date <= today) ||
					(!row.Offering.OpenDate.HasValue && row.Cycle.StartDate.Date <= today))
				.Where(row =>
					(row.Offering.CloseDate.HasValue && row.Offering.CloseDate.Value.Date >= today) ||
					(!row.Offering.CloseDate.HasValue && row.Cycle.EndDate.Date >= today)),
			AdmissionOfferingAvailabilityStatus.NotOpenYet => ApplyConfigurationReadyFilter(query, incompatibleOfferingIds)
				.Where(row => row.Offering.IsActive)
				.Where(row => row.Cycle.Status != AdmissionCycleStatus.Closed && row.Cycle.Status != AdmissionCycleStatus.Archived)
				.Where(row =>
					(row.Offering.OpenDate.HasValue && row.Offering.OpenDate.Value.Date > today) ||
					(!row.Offering.OpenDate.HasValue && row.Cycle.StartDate.Date > today)),
			AdmissionOfferingAvailabilityStatus.Closed => ApplyConfigurationReadyFilter(query, incompatibleOfferingIds)
				.Where(row => row.Offering.IsActive)
				.Where(row => row.Cycle.Status == AdmissionCycleStatus.Closed || row.Cycle.Status == AdmissionCycleStatus.Archived ||
					(row.Offering.CloseDate.HasValue && row.Offering.CloseDate.Value.Date < today) ||
					(!row.Offering.CloseDate.HasValue && row.Cycle.EndDate.Date < today)),
			_ => query
		};

		return query;
	}

	private static IQueryable<AdmissionOfferingReadRow> ApplyConfigurationReadyFilter(
		IQueryable<AdmissionOfferingReadRow> query, List<Guid> incompatibleOfferingIds) => query.Where(row =>
			!incompatibleOfferingIds.Contains(row.Offering.Id) &&
			(!row.Offering.CampusId.HasValue || row.Campus != null && row.Campus.IsActive) &&
			row.Cycle.Status == AdmissionCycleStatus.Published &&
			row.BoardAcademicYearId == row.Cycle.AcademicYearId &&
			row.MediumBoardId == row.BoardId &&
			row.StageMediumId == row.MediumId &&
			row.StandardStageId == row.StageId &&
			(!row.Offering.OpenDate.HasValue || row.Offering.OpenDate.Value.Date >= row.Cycle.StartDate.Date) &&
			(!row.Offering.CloseDate.HasValue || row.Offering.CloseDate.Value.Date <= row.Cycle.EndDate.Date) &&
			(!row.Offering.OpenDate.HasValue || !row.Offering.CloseDate.HasValue ||
				row.Offering.OpenDate.Value.Date <= row.Offering.CloseDate.Value.Date) &&
			row.Workflow != null &&
			(row.Workflow.Status == AdmissionWorkflowStatus.Published ||
				row.Workflow.Status == AdmissionWorkflowStatus.Superseded) &&
			(row.Offering.EntryMode == AdmissionEntryMode.DirectRegistration ||
				row.InquiryForm != null && row.InquiryForm.Status == AdmissionFormStatus.Published &&
				row.InquiryForm.Purpose == AdmissionFormPurpose.Inquiry) &&
			(!row.Offering.InquiryFormDefinitionId.HasValue ||
				row.InquiryForm != null && row.InquiryForm.Purpose == AdmissionFormPurpose.Inquiry) &&
			row.ApplicationForm != null && row.ApplicationForm.Status == AdmissionFormStatus.Published &&
			row.ApplicationForm.Purpose == AdmissionFormPurpose.Registration);

	private static IQueryable<AdmissionOfferingReadRow> ApplyConfigurationIncompleteFilter(
		IQueryable<AdmissionOfferingReadRow> query, List<Guid> incompatibleOfferingIds) => query.Where(row =>
			incompatibleOfferingIds.Contains(row.Offering.Id) ||
			(row.Offering.CampusId.HasValue && (row.Campus == null || !row.Campus.IsActive)) ||
			row.Cycle.Status != AdmissionCycleStatus.Published ||
			row.BoardAcademicYearId != row.Cycle.AcademicYearId ||
			row.MediumBoardId != row.BoardId ||
			row.StageMediumId != row.MediumId ||
			row.StandardStageId != row.StageId ||
			(row.Offering.OpenDate.HasValue && row.Offering.OpenDate.Value.Date < row.Cycle.StartDate.Date) ||
			(row.Offering.CloseDate.HasValue && row.Offering.CloseDate.Value.Date > row.Cycle.EndDate.Date) ||
			(row.Offering.OpenDate.HasValue && row.Offering.CloseDate.HasValue &&
				row.Offering.OpenDate.Value.Date > row.Offering.CloseDate.Value.Date) ||
			row.Workflow == null ||
			(row.Workflow.Status != AdmissionWorkflowStatus.Published &&
				row.Workflow.Status != AdmissionWorkflowStatus.Superseded) ||
			(row.Offering.EntryMode != AdmissionEntryMode.DirectRegistration &&
				(row.InquiryForm == null || row.InquiryForm.Status != AdmissionFormStatus.Published ||
				row.InquiryForm.Purpose != AdmissionFormPurpose.Inquiry)) ||
			(row.Offering.InquiryFormDefinitionId.HasValue &&
				(row.InquiryForm == null || row.InquiryForm.Purpose != AdmissionFormPurpose.Inquiry)) ||
			row.ApplicationForm == null || row.ApplicationForm.Status != AdmissionFormStatus.Published ||
			row.ApplicationForm.Purpose != AdmissionFormPurpose.Registration);

	private AdmissionOfferingListDto MapList(AdmissionOfferingReadRow row, List<AdmissionOfferingValidationError>? eligibilityErrors)
	{
		var validation = BuildValidation(row, eligibilityErrors);
		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			row.Offering, row.Cycle, validation.IsValid, new DateTimeOffset(Clock.Now.ToUniversalTime()));
		return new AdmissionOfferingListDto
		{
			Id = row.Offering.Id,
			Name = row.Offering.Name,
			AdmissionCycleId = row.Cycle.Id,
			AdmissionCycleName = row.Cycle.Name,
			CampusId = row.Offering.CampusId,
			CampusName = row.Campus?.Name,
			BoardId = row.BoardId,
			BoardName = row.BoardType.ToString(),
			MediumId = row.MediumId,
			MediumName = row.MediumName,
			StageId = row.StageId,
			StageName = row.StageName,
			StandardId = row.StandardId,
			StandardName = row.StandardName,
			Track = row.Offering.Track,
			EntryMode = row.Offering.EntryMode,
			Code = row.Offering.Code,
			EffectiveOpenDate = row.Offering.GetEffectiveOpenDate(row.Cycle.StartDate),
			EffectiveCloseDate = row.Offering.GetEffectiveCloseDate(row.Cycle.EndDate),
			WorkflowName = row.Workflow?.Name,
			WorkflowVersion = row.Workflow?.Version,
			IsActive = row.Offering.IsActive,
			AvailabilityStatus = availability.Status,
			ConfigurationStatus = validation.Status,
			ApplicationCount = row.ApplicationCount
		};
	}

	private AdmissionOfferingDto MapDetail(AdmissionOfferingReadRow row, List<AdmissionOfferingValidationError>? eligibilityErrors)
	{
		var list = MapList(row, eligibilityErrors);
		return new AdmissionOfferingDto
		{
			Id = list.Id,
			Name = list.Name,
			AdmissionCycleId = list.AdmissionCycleId,
			AdmissionCycleName = list.AdmissionCycleName,
			CampusId = list.CampusId,
			CampusName = list.CampusName,
			BoardId = list.BoardId,
			BoardName = list.BoardName,
			MediumId = list.MediumId,
			MediumName = list.MediumName,
			StageId = list.StageId,
			StageName = list.StageName,
			StandardId = list.StandardId,
			StandardName = list.StandardName,
			Track = list.Track,
			EntryMode = list.EntryMode,
			Code = list.Code,
			EffectiveOpenDate = list.EffectiveOpenDate,
			EffectiveCloseDate = list.EffectiveCloseDate,
			WorkflowName = list.WorkflowName,
			WorkflowVersion = list.WorkflowVersion,
			IsActive = list.IsActive,
			AvailabilityStatus = list.AvailabilityStatus,
			ConfigurationStatus = list.ConfigurationStatus,
			ApplicationCount = list.ApplicationCount,
			UseCycleDates = !row.Offering.OpenDate.HasValue && !row.Offering.CloseDate.HasValue,
			OpenDate = row.Offering.OpenDate,
			CloseDate = row.Offering.CloseDate,
			WorkflowDefinitionId = row.Offering.WorkflowDefinitionId,
			InquiryFormDefinitionId = row.Offering.InquiryFormDefinitionId,
			ApplicationFormDefinitionId = row.Offering.ApplicationFormDefinitionId,
			InquiryIntakeProfileId = row.Offering.InquiryIntakeProfileId,
			CounsellorAssignmentProfileId = row.Offering.CounsellorAssignmentProfileId,
			InquiryFormName = row.InquiryForm?.Name,
			InquiryFormVersion = row.InquiryForm?.Version,
			ApplicationFormName = row.ApplicationForm?.Name,
			ApplicationFormVersion = row.ApplicationForm?.Version,
			Configuration = MapValidation(BuildValidation(row, eligibilityErrors)),
			ConcurrencyStamp = row.Offering.ConcurrencyStamp,
			CreationTime = row.Offering.CreationTime,
			CreatorId = row.Offering.CreatorId,
			LastModificationTime = row.Offering.LastModificationTime,
			LastModifierId = row.Offering.LastModifierId
		};
	}

	private static AdmissionOfferingValidationResult BuildValidation(AdmissionOfferingReadRow row, List<AdmissionOfferingValidationError>? eligibilityErrors)
	{
		var errors = new List<AdmissionOfferingValidationError>(eligibilityErrors ?? []);
		if (row.Cycle.Status != AdmissionCycleStatus.Published)
			errors.Add(new(AdmissionOfferingValidationCodes.CycleUnavailable, "Admission Cycle is not published."));
		if (row.Offering.CampusId.HasValue && row.Campus == null)
			errors.Add(new(AdmissionOfferingValidationCodes.CampusMissing, "Selected Campus was not found."));
		else if (row.Campus is { IsActive: false })
			errors.Add(new(AdmissionOfferingValidationCodes.CampusInactive, "Selected Campus is inactive."));
		if (row.BoardAcademicYearId != row.Cycle.AcademicYearId || row.MediumBoardId != row.BoardId ||
			row.StageMediumId != row.MediumId || row.StandardStageId != row.StageId)
			errors.Add(new(AdmissionOfferingValidationCodes.HierarchyInvalid, "Academic hierarchy is invalid."));
		if (row.Offering.GetEffectiveOpenDate(row.Cycle.StartDate).Date < row.Cycle.StartDate.Date ||
			row.Offering.GetEffectiveCloseDate(row.Cycle.EndDate).Date > row.Cycle.EndDate.Date ||
			row.Offering.GetEffectiveOpenDate(row.Cycle.StartDate).Date > row.Offering.GetEffectiveCloseDate(row.Cycle.EndDate).Date)
			errors.Add(new(AdmissionOfferingValidationCodes.DateRangeInvalid, "Offering dates must fall inside the Admission Cycle."));
		if (row.Workflow == null)
			errors.Add(new(AdmissionOfferingValidationCodes.WorkflowMissing, "Published Admission Workflow is required."));
		else if (!row.Workflow.Status.CanConfigureOffering())
			errors.Add(new(AdmissionOfferingValidationCodes.WorkflowNotPublished, "Admission Workflow is not published."));
		if (row.Offering.EntryMode != AdmissionEntryMode.DirectRegistration && row.InquiryForm == null)
			errors.Add(new(AdmissionOfferingValidationCodes.InquiryFormMissing, "Published Inquiry Form is required."));
		else if (row.InquiryForm != null && row.InquiryForm.Status != AdmissionFormStatus.Published)
			errors.Add(new(AdmissionOfferingValidationCodes.InquiryFormNotPublished, "Inquiry Form is not published."));
		else if (row.InquiryForm != null && row.InquiryForm.Purpose != AdmissionFormPurpose.Inquiry)
			errors.Add(new(AdmissionOfferingValidationCodes.InquiryFormPurposeMismatch, "The selected Inquiry Form must have the Inquiry purpose."));
		if (row.ApplicationForm == null)
			errors.Add(new(AdmissionOfferingValidationCodes.ApplicationFormMissing, "Published Application Form is required."));
		else if (row.ApplicationForm.Status != AdmissionFormStatus.Published)
			errors.Add(new(AdmissionOfferingValidationCodes.ApplicationFormNotPublished, "Application Form is not published."));
		else if (row.ApplicationForm.Purpose != AdmissionFormPurpose.Registration)
			errors.Add(new(AdmissionOfferingValidationCodes.ApplicationFormPurposeMismatch, "The selected Application Form must have the Registration purpose."));
		var usingIntake = row.Offering.InquiryIntakeProfileId.HasValue || row.Offering.CounsellorAssignmentProfileId.HasValue;
		if (row.Offering.InquiryIntakeProfileId.HasValue && row.InquiryProfile?.Kind != AdmissionIntakeProfileKind.Inquiry ||
			usingIntake && row.Offering.EntryMode != AdmissionEntryMode.DirectRegistration && row.InquiryProfile == null)
			errors.Add(new(AdmissionOfferingValidationCodes.InquiryIntakeProfileInvalid, "Inquiry profile is unavailable."));
		if (row.Offering.CounsellorAssignmentProfileId.HasValue && row.AssignmentProfile?.Kind != AdmissionIntakeProfileKind.CounsellorAssignment ||
			usingIntake && row.AssignmentProfile == null)
			errors.Add(new(AdmissionOfferingValidationCodes.CounsellorAssignmentProfileInvalid, "Counsellor assignment profile is unavailable."));
		return new AdmissionOfferingValidationResult(errors);
	}

	public static AdmissionOfferingConfigurationValidationDto MapValidation(AdmissionOfferingValidationResult validation) => new()
	{
		IsValid = validation.IsValid,
		Status = validation.Status,
		Errors = validation.Errors.Select(error => new AdmissionOfferingValidationErrorDto
		{
			Code = error.Code,
			Message = error.Message
		}).ToList()
	};

	private static IQueryable<AdmissionOfferingReadRow> ApplySorting(
		IQueryable<AdmissionOfferingReadRow> query,
		string? sorting)
	{
		var descending = sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		return field switch
		{
			"name" => descending ? query.OrderByDescending(x => x.Offering.Name) : query.OrderBy(x => x.Offering.Name),
			"code" => descending ? query.OrderByDescending(x => x.Offering.Code) : query.OrderBy(x => x.Offering.Code),
			"boardname" => descending ? query.OrderByDescending(x => x.BoardType) : query.OrderBy(x => x.BoardType),
			"mediumname" => descending ? query.OrderByDescending(x => x.MediumName) : query.OrderBy(x => x.MediumName),
			"stagename" => descending ? query.OrderByDescending(x => x.StageName) : query.OrderBy(x => x.StageName),
			"standardname" => descending ? query.OrderByDescending(x => x.StandardName) : query.OrderBy(x => x.StandardName),
			"effectiveopendate" => descending
				? query.OrderByDescending(x => x.Offering.OpenDate.HasValue ? x.Offering.OpenDate.Value.Date : x.Cycle.StartDate.Date)
				: query.OrderBy(x => x.Offering.OpenDate.HasValue ? x.Offering.OpenDate.Value.Date : x.Cycle.StartDate.Date),
			"effectiveclosedate" => descending
				? query.OrderByDescending(x => x.Offering.CloseDate.HasValue ? x.Offering.CloseDate.Value.Date : x.Cycle.EndDate.Date)
				: query.OrderBy(x => x.Offering.CloseDate.HasValue ? x.Offering.CloseDate.Value.Date : x.Cycle.EndDate.Date),
			_ => query.OrderBy(x => x.StandardName).ThenBy(x => x.Offering.Code)
		};
	}

	private static string? StatusReasonCode(AdmissionOfferingAvailabilityStatus status) => status switch
	{
		AdmissionOfferingAvailabilityStatus.Inactive => AdmissionErrorCodes.OfferingInactive,
		AdmissionOfferingAvailabilityStatus.NotOpenYet => AdmissionErrorCodes.OfferingNotOpenYet,
		AdmissionOfferingAvailabilityStatus.Closed => AdmissionErrorCodes.OfferingClosed,
		_ => null
	};

	private static string? StatusReasonMessage(AdmissionOfferingAvailabilityStatus status) => status switch
	{
		AdmissionOfferingAvailabilityStatus.Inactive => "This Admission Offering is inactive.",
		AdmissionOfferingAvailabilityStatus.NotOpenYet => "Applications for this Admission Offering are not open yet.",
		AdmissionOfferingAvailabilityStatus.Closed => "Applications for this Admission Offering are closed.",
		_ => null
	};

	private sealed record AdmissionOfferingReadQuery(IQueryable<AdmissionOfferingReadRow> Query,
		Dictionary<Guid, List<AdmissionOfferingValidationError>> Errors);

	private sealed class AdmissionOfferingReadRow
	{
		public required AdmissionOffering Offering { get; init; }
		public AdmissionCampus? Campus { get; init; }
		public required AdmissionCycle Cycle { get; init; }
		public Guid BoardId { get; init; }
		public Guid BoardAcademicYearId { get; init; }
		public EducationBoardType BoardType { get; init; }
		public Guid MediumId { get; init; }
		public Guid MediumBoardId { get; init; }
		public required string MediumName { get; init; }
		public Guid StageId { get; init; }
		public Guid StageMediumId { get; init; }
		public required string StageName { get; init; }
		public Guid StandardId { get; init; }
		public Guid StandardStageId { get; init; }
		public required string StandardName { get; init; }
		public AdmissionWorkflowDefinition? Workflow { get; init; }
		public AdmissionFormDefinition? InquiryForm { get; init; }
		public AdmissionFormDefinition? ApplicationForm { get; init; }
		public AdmissionIntakeProfile? InquiryProfile { get; init; }
		public AdmissionIntakeProfile? AssignmentProfile { get; init; }
		public int ApplicationCount { get; init; }
	}
}
