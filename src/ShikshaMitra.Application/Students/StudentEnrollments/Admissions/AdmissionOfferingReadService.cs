using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
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
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IUserScopeAccessService scopeAccessService,
	AdmissionEligibilityOfferingValidator eligibilityValidator)
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
		return MapDetail(row, read.Errors.GetValueOrDefault(row.Offering.Id));
	}

	public async Task<AdmissionOfferingAvailabilityDto> GetAvailabilityAsync(Guid id)
	{
		var detail = await GetAsync(id);
		return new AdmissionOfferingAvailabilityDto
		{
			IsAvailable = detail.AvailabilityStatus == AdmissionOfferingAvailabilityStatus.Open,
			Status = detail.AvailabilityStatus,
			CanCreateInquiry = detail.AvailabilityStatus == AdmissionOfferingAvailabilityStatus.Open,
			CanStartApplication = detail.AvailabilityStatus == AdmissionOfferingAvailabilityStatus.Open,
			ReasonCode = detail.Configuration.IsValid ? StatusReasonCode(detail.AvailabilityStatus) : AdmissionErrorCodes.OfferingConfigurationIncomplete,
			ReasonMessage = detail.Configuration.IsValid ? StatusReasonMessage(detail.AvailabilityStatus) :
				detail.Configuration.Errors.FirstOrDefault()?.Message
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
		var applications = await applicationRepository.GetQueryableAsync();
		var divisions = await divisionRepository.GetQueryableAsync();

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
			where (!offeringId.HasValue || offering.Id == offeringId.Value) &&
				(!input.AdmissionCycleId.HasValue || offering.AdmissionCycleId == input.AdmissionCycleId.Value) &&
				(!input.CampusId.HasValue || offering.CampusId == input.CampusId.Value) &&
				(!input.BoardId.HasValue || offering.BoardId == input.BoardId.Value) &&
				(!input.MediumId.HasValue || offering.MediumId == input.MediumId.Value) &&
				(!input.StageId.HasValue || offering.StageId == input.StageId.Value) &&
				(!input.StandardId.HasValue || offering.StandardId == input.StandardId.Value) &&
				(!input.IsActive.HasValue || offering.IsActive == input.IsActive.Value) &&
				(string.IsNullOrWhiteSpace(input.Search) || offering.Code.Contains(input.Search) ||
					cycle.Name.Contains(input.Search) || (campus != null && campus.Name.Contains(input.Search)) || medium.Name.Contains(input.Search) ||
					stage.Name.Contains(input.Search) || standard.Name.Contains(input.Search))
			select new AdmissionOfferingReadRow
			{
				Offering = offering,
				Campus = campus,
				Cycle = cycle,
				Board = board,
				Medium = medium,
				Stage = stage,
				Standard = standard,
				Workflow = workflow,
				InquiryForm = inquiryForm,
				ApplicationForm = applicationForm,
				ApplicationCount = applications.Count(application => application.AdmissionOfferingId == offering.Id)
			};

		var scope = await scopeAccessService.GetCurrentUserScopeAsync();
		if (!scope.IsAdmin && !scope.HasTenantWideScope())
		{
			query = query.Where(row =>
				scope.WildcardBoardAcademicYearIds.Contains(row.Cycle.AcademicYearId) ||
				scope.BoardIds.Contains(row.Offering.BoardId) || scope.WildcardMediumBoardIds.Contains(row.Offering.BoardId) ||
				scope.MediumIds.Contains(row.Offering.MediumId) || scope.WildcardMediumStageMediumIds.Contains(row.Offering.MediumId) ||
				scope.MediumStageIds.Contains(row.Offering.StageId) || scope.WildcardStandardMediumStageIds.Contains(row.Offering.StageId) ||
				scope.StandardIds.Contains(row.Offering.StandardId) || scope.WildcardDivisionStandardIds.Contains(row.Offering.StandardId) ||
				divisions.Any(division => division.StandardId == row.Offering.StandardId && scope.DivisionIds.Contains(division.Id)));
		}

		var eligibilityErrors = await eligibilityValidator.GetErrorsAsync(query.Select(row => row.Offering));
		query = ApplyDerivedFilters(query, input, Clock.Now.Date, eligibilityErrors.Keys.ToList());

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
			row.Board.AcademicYearId == row.Cycle.AcademicYearId &&
			row.Medium.EducationBoardId == row.Board.Id &&
			row.Stage.EducationMediumId == row.Medium.Id &&
			row.Standard.EducationMediumStageId == row.Stage.Id &&
			(!row.Offering.OpenDate.HasValue || row.Offering.OpenDate.Value.Date >= row.Cycle.StartDate.Date) &&
			(!row.Offering.CloseDate.HasValue || row.Offering.CloseDate.Value.Date <= row.Cycle.EndDate.Date) &&
			(!row.Offering.OpenDate.HasValue || !row.Offering.CloseDate.HasValue ||
				row.Offering.OpenDate.Value.Date <= row.Offering.CloseDate.Value.Date) &&
			row.Workflow != null &&
			(row.Workflow.Status == AdmissionWorkflowStatus.Published ||
				row.Workflow.Status == AdmissionWorkflowStatus.Superseded) &&
			row.InquiryForm != null && row.InquiryForm.Status == AdmissionFormStatus.Published &&
			row.ApplicationForm != null && row.ApplicationForm.Status == AdmissionFormStatus.Published);

	private static IQueryable<AdmissionOfferingReadRow> ApplyConfigurationIncompleteFilter(
		IQueryable<AdmissionOfferingReadRow> query, List<Guid> incompatibleOfferingIds) => query.Where(row =>
			incompatibleOfferingIds.Contains(row.Offering.Id) ||
			(row.Offering.CampusId.HasValue && (row.Campus == null || !row.Campus.IsActive)) ||
			row.Cycle.Status != AdmissionCycleStatus.Published ||
			row.Board.AcademicYearId != row.Cycle.AcademicYearId ||
			row.Medium.EducationBoardId != row.Board.Id ||
			row.Stage.EducationMediumId != row.Medium.Id ||
			row.Standard.EducationMediumStageId != row.Stage.Id ||
			(row.Offering.OpenDate.HasValue && row.Offering.OpenDate.Value.Date < row.Cycle.StartDate.Date) ||
			(row.Offering.CloseDate.HasValue && row.Offering.CloseDate.Value.Date > row.Cycle.EndDate.Date) ||
			(row.Offering.OpenDate.HasValue && row.Offering.CloseDate.HasValue &&
				row.Offering.OpenDate.Value.Date > row.Offering.CloseDate.Value.Date) ||
			row.Workflow == null ||
			(row.Workflow.Status != AdmissionWorkflowStatus.Published &&
				row.Workflow.Status != AdmissionWorkflowStatus.Superseded) ||
			row.InquiryForm == null || row.InquiryForm.Status != AdmissionFormStatus.Published ||
			row.ApplicationForm == null || row.ApplicationForm.Status != AdmissionFormStatus.Published);

	private AdmissionOfferingListDto MapList(AdmissionOfferingReadRow row, List<AdmissionOfferingValidationError>? eligibilityErrors)
	{
		var validation = BuildValidation(row, eligibilityErrors);
		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			row.Offering, row.Cycle, validation.IsValid, new DateTimeOffset(Clock.Now.ToUniversalTime()));
		return new AdmissionOfferingListDto
		{
			Id = row.Offering.Id,
			AdmissionCycleId = row.Cycle.Id,
			AdmissionCycleName = row.Cycle.Name,
			CampusId = row.Offering.CampusId,
			CampusName = row.Campus?.Name,
			BoardId = row.Board.Id,
			BoardName = row.Board.Type.ToString(),
			MediumId = row.Medium.Id,
			MediumName = row.Medium.Name,
			StageId = row.Stage.Id,
			StageName = row.Stage.Name,
			StandardId = row.Standard.Id,
			StandardName = row.Standard.Name,
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
		if (row.Board.AcademicYearId != row.Cycle.AcademicYearId || row.Medium.EducationBoardId != row.Board.Id ||
			row.Stage.EducationMediumId != row.Medium.Id || row.Standard.EducationMediumStageId != row.Stage.Id)
			errors.Add(new(AdmissionOfferingValidationCodes.HierarchyInvalid, "Academic hierarchy is invalid."));
		if (row.Offering.GetEffectiveOpenDate(row.Cycle.StartDate).Date < row.Cycle.StartDate.Date ||
			row.Offering.GetEffectiveCloseDate(row.Cycle.EndDate).Date > row.Cycle.EndDate.Date ||
			row.Offering.GetEffectiveOpenDate(row.Cycle.StartDate).Date > row.Offering.GetEffectiveCloseDate(row.Cycle.EndDate).Date)
			errors.Add(new(AdmissionOfferingValidationCodes.DateRangeInvalid, "Offering dates must fall inside the Admission Cycle."));
		if (row.Workflow == null)
			errors.Add(new(AdmissionOfferingValidationCodes.WorkflowMissing, "Published Admission Workflow is required."));
		else if (!row.Workflow.Status.CanConfigureOffering())
			errors.Add(new(AdmissionOfferingValidationCodes.WorkflowNotPublished, "Admission Workflow is not published."));
		if (row.InquiryForm == null)
			errors.Add(new(AdmissionOfferingValidationCodes.InquiryFormMissing, "Published Inquiry Form is required."));
		else if (row.InquiryForm.Status != AdmissionFormStatus.Published)
			errors.Add(new(AdmissionOfferingValidationCodes.InquiryFormNotPublished, "Inquiry Form is not published."));
		if (row.ApplicationForm == null)
			errors.Add(new(AdmissionOfferingValidationCodes.ApplicationFormMissing, "Published Application Form is required."));
		else if (row.ApplicationForm.Status != AdmissionFormStatus.Published)
			errors.Add(new(AdmissionOfferingValidationCodes.ApplicationFormNotPublished, "Application Form is not published."));
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
			"code" => descending ? query.OrderByDescending(x => x.Offering.Code) : query.OrderBy(x => x.Offering.Code),
			"boardname" => descending ? query.OrderByDescending(x => x.Board.Type) : query.OrderBy(x => x.Board.Type),
			"mediumname" => descending ? query.OrderByDescending(x => x.Medium.Name) : query.OrderBy(x => x.Medium.Name),
			"stagename" => descending ? query.OrderByDescending(x => x.Stage.Name) : query.OrderBy(x => x.Stage.Name),
			"standardname" => descending ? query.OrderByDescending(x => x.Standard.Name) : query.OrderBy(x => x.Standard.Name),
			"effectiveopendate" => descending
				? query.OrderByDescending(x => x.Offering.OpenDate.HasValue ? x.Offering.OpenDate.Value.Date : x.Cycle.StartDate.Date)
				: query.OrderBy(x => x.Offering.OpenDate.HasValue ? x.Offering.OpenDate.Value.Date : x.Cycle.StartDate.Date),
			"effectiveclosedate" => descending
				? query.OrderByDescending(x => x.Offering.CloseDate.HasValue ? x.Offering.CloseDate.Value.Date : x.Cycle.EndDate.Date)
				: query.OrderBy(x => x.Offering.CloseDate.HasValue ? x.Offering.CloseDate.Value.Date : x.Cycle.EndDate.Date),
			_ => query.OrderBy(x => x.Standard.Name).ThenBy(x => x.Offering.Code)
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
		public required EducationBoard Board { get; init; }
		public required EducationMedium Medium { get; init; }
		public required EducationMediumStage Stage { get; init; }
		public required Standard Standard { get; init; }
		public AdmissionWorkflowDefinition? Workflow { get; init; }
		public AdmissionFormDefinition? InquiryForm { get; init; }
		public AdmissionFormDefinition? ApplicationForm { get; init; }
		public int ApplicationCount { get; init; }
	}
}
