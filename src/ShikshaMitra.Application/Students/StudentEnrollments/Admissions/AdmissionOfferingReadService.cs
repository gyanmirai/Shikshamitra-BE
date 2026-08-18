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
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IUserScopeAccessService scopeAccessService)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionOfferingListDto>> GetListAsync(AdmissionOfferingListInput input)
	{
		var rows = await GetRowsAsync(input);
		var mapped = rows.Select(MapList).ToList();
		if (input.AvailabilityStatus.HasValue)
		{
			mapped = mapped.Where(item => item.AvailabilityStatus == input.AvailabilityStatus.Value).ToList();
		}
		if (input.ConfigurationStatus.HasValue)
		{
			mapped = mapped.Where(item => item.ConfigurationStatus == input.ConfigurationStatus.Value).ToList();
		}

		mapped = Sort(mapped, input.Sorting);
		var totalCount = mapped.Count;
		var page = mapped.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
		return new PagedResultDto<AdmissionOfferingListDto>(totalCount, page);
	}

	public async Task<AdmissionOfferingDto> GetAsync(Guid id)
	{
		var rows = await GetRowsAsync(new AdmissionOfferingListInput
		{
			MaxResultCount = 1,
			SkipCount = 0
		}, id);
		var row = rows.SingleOrDefault() ?? throw new EntityNotFoundException(typeof(AdmissionOffering), id);
		return MapDetail(row);
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

	private async Task<List<AdmissionOfferingReadRow>> GetRowsAsync(
		AdmissionOfferingListInput input,
		Guid? offeringId = null)
	{
		var offerings = await offeringRepository.GetQueryableAsync();
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
					cycle.Name.Contains(input.Search) || medium.Name.Contains(input.Search) ||
					stage.Name.Contains(input.Search) || standard.Name.Contains(input.Search))
			select new AdmissionOfferingReadRow(
				offering, cycle, board, medium, stage, standard, workflow, inquiryForm, applicationForm,
				applications.Count(application => application.AdmissionOfferingId == offering.Id));

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

		return await AsyncExecuter.ToListAsync(query);
	}

	private AdmissionOfferingListDto MapList(AdmissionOfferingReadRow row)
	{
		var validation = BuildValidation(row);
		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			row.Offering, row.Cycle, validation.IsValid, new DateTimeOffset(Clock.Now.ToUniversalTime()));
		return new AdmissionOfferingListDto
		{
			Id = row.Offering.Id,
			AdmissionCycleId = row.Cycle.Id,
			AdmissionCycleName = row.Cycle.Name,
			CampusId = row.Offering.CampusId,
			CampusName = null,
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

	private AdmissionOfferingDto MapDetail(AdmissionOfferingReadRow row)
	{
		var list = MapList(row);
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
			Configuration = MapValidation(BuildValidation(row)),
			ConcurrencyStamp = row.Offering.ConcurrencyStamp,
			CreationTime = row.Offering.CreationTime,
			CreatorId = row.Offering.CreatorId,
			LastModificationTime = row.Offering.LastModificationTime,
			LastModifierId = row.Offering.LastModifierId
		};
	}

	private static AdmissionOfferingValidationResult BuildValidation(AdmissionOfferingReadRow row)
	{
		var errors = new List<AdmissionOfferingValidationError>();
		if (row.Cycle.Status != AdmissionCycleStatus.Published)
			errors.Add(new(AdmissionOfferingValidationCodes.CycleUnavailable, "Admission Cycle is not published."));
		if (row.Board.AcademicYearId != row.Cycle.AcademicYearId || row.Medium.EducationBoardId != row.Board.Id ||
			row.Stage.EducationMediumId != row.Medium.Id || row.Standard.EducationMediumStageId != row.Stage.Id)
			errors.Add(new(AdmissionOfferingValidationCodes.HierarchyInvalid, "Academic hierarchy is invalid."));
		if (row.Offering.GetEffectiveOpenDate(row.Cycle.StartDate).Date < row.Cycle.StartDate.Date ||
			row.Offering.GetEffectiveCloseDate(row.Cycle.EndDate).Date > row.Cycle.EndDate.Date ||
			row.Offering.GetEffectiveOpenDate(row.Cycle.StartDate).Date > row.Offering.GetEffectiveCloseDate(row.Cycle.EndDate).Date)
			errors.Add(new(AdmissionOfferingValidationCodes.DateRangeInvalid, "Offering dates must fall inside the Admission Cycle."));
		if (row.Workflow == null)
			errors.Add(new(AdmissionOfferingValidationCodes.WorkflowMissing, "Published Admission Workflow is required."));
		else if (row.Workflow.Status != AdmissionWorkflowStatus.Published)
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

	private static List<AdmissionOfferingListDto> Sort(List<AdmissionOfferingListDto> rows, string? sorting)
	{
		var descending = sorting?.Contains("desc", StringComparison.OrdinalIgnoreCase) == true;
		var field = sorting?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
		IOrderedEnumerable<AdmissionOfferingListDto> ordered = field switch
		{
			"code" => descending ? rows.OrderByDescending(x => x.Code) : rows.OrderBy(x => x.Code),
			"boardname" => descending ? rows.OrderByDescending(x => x.BoardName) : rows.OrderBy(x => x.BoardName),
			"mediumname" => descending ? rows.OrderByDescending(x => x.MediumName) : rows.OrderBy(x => x.MediumName),
			"stagename" => descending ? rows.OrderByDescending(x => x.StageName) : rows.OrderBy(x => x.StageName),
			"standardname" => descending ? rows.OrderByDescending(x => x.StandardName) : rows.OrderBy(x => x.StandardName),
			"effectiveopendate" => descending ? rows.OrderByDescending(x => x.EffectiveOpenDate) : rows.OrderBy(x => x.EffectiveOpenDate),
			"effectiveclosedate" => descending ? rows.OrderByDescending(x => x.EffectiveCloseDate) : rows.OrderBy(x => x.EffectiveCloseDate),
			_ => rows.OrderBy(x => x.StandardName).ThenBy(x => x.Code)
		};
		return ordered.ToList();
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

	private sealed record AdmissionOfferingReadRow(
		AdmissionOffering Offering,
		AdmissionCycle Cycle,
		EducationBoard Board,
		EducationMedium Medium,
		EducationMediumStage Stage,
		Standard Standard,
		AdmissionWorkflowDefinition? Workflow,
		AdmissionFormDefinition? InquiryForm,
		AdmissionFormDefinition? ApplicationForm,
		int ApplicationCount);
}
