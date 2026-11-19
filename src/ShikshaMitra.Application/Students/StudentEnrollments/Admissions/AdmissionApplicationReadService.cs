using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationReadService(
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionApplicationStageInstance, Guid> stageRepository,
	AdmissionApplicationWorkspaceQueryService workspace,
	AdmissionApplicationListQueryService listQuery,
	AdmissionApplicationScopeAccessService scopeAccess,
	AdmissionReviewDecisionGuard reviewGuard)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input)
	{
		var query = await scopeAccess.GetScopedApplicationsAsync();
		var visibleOfferings = await scopeAccess.GetScopedOfferingsAsync();
		var cycles = await cycleRepository.GetQueryableAsync();
		query = query.Where(application => visibleOfferings.Any(offering =>
			offering.Id == application.AdmissionOfferingId &&
			(!input.BoardId.HasValue || offering.BoardId == input.BoardId.Value) &&
			(!input.MediumId.HasValue || offering.MediumId == input.MediumId.Value) &&
			(!input.StandardId.HasValue || offering.StandardId == input.StandardId.Value) &&
			(input.StandardIds.Count == 0 || Enumerable.Contains(input.StandardIds, offering.StandardId))) &&
			(!input.AcademicYearId.HasValue || cycles.Any(cycle =>
				cycle.Id == application.AdmissionCycleId && cycle.AcademicYearId == input.AcademicYearId.Value)));
		var stageQuery = await stageRepository.GetQueryableAsync();
		query = query.Where(application =>
			(!input.Status.HasValue || application.Status == input.Status) &&
			(!input.AdmissionCycleId.HasValue || application.AdmissionCycleId == input.AdmissionCycleId) &&
			(!input.AdmissionOfferingId.HasValue || application.AdmissionOfferingId == input.AdmissionOfferingId) &&
			(!input.CurrentStageInstanceId.HasValue || application.CurrentStageInstanceId == input.CurrentStageInstanceId) &&
			(string.IsNullOrWhiteSpace(input.CurrentStageSearch) ||
				stageQuery.Any(stage => stage.Id == application.CurrentStageInstanceId && stage.Name.Contains(input.CurrentStageSearch!))));
		query = await LazyServiceProvider.LazyGetRequiredService<AdmissionDashboardQueryService>().ApplyAsync(query, input.ActiveOnly, input.PendingDecisionOnly, input.DivisionId);
		var workspaces = await workspace.ProjectAsync(query);
		if (input.DecisionWorkspaceOnly && !input.PendingDecisionOnly) workspaces = workspaces.Where(row => row.HasReachedDecision ||
			row.Application.Status == AdmissionApplicationStatus.Withdrawn);
		var projection = await listQuery.ProjectAsync(workspaces);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			projection = projection.Where(row => row.Application.ApplicationNumber.ToLower().Contains(search) || row.ApplicantName.ToLower().Contains(search) ||
				row.ContactPhone != null && row.ContactPhone.Contains(search) || row.ContactEmail != null && row.ContactEmail.ToLower().Contains(search) ||
				row.StandardName != null && row.StandardName.ToLower().Contains(search) || row.AssignedCounsellorName != null && row.AssignedCounsellorName.ToLower().Contains(search));
		}
		projection = AdmissionQueueColumnFilters.Applications(projection, input.ColumnFilters);
		var count = await AsyncExecuter.CountAsync(projection);
		var rows = await AsyncExecuter.ToListAsync(AdmissionApplicationListQueryService.Sort(projection, input.Sorting)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionApplicationDto>(count, rows.Select(MapListRow).ToList());
	}

	public async Task<AdmissionApplicationDto> GetAsync(Guid id)
	{
		var application = await GetEntityAsync(id);
		return await MapAsync(application);
	}

	public async Task<AdmissionApplicationDto?> FindAsync(Guid id)
	{
		var application = await FindEntityAsync(id);
		return application == null ? null : await MapAsync(application);
	}

	public async Task<AdmissionApplication?> FindEntityAsync(Guid id)
	{
		var application = await applicationRepository.FindAsync(id);
		if (application == null || !await scopeAccess.CanAccessAsync(application))
			return null;
		return application;
	}

	public async Task<AdmissionApplication> GetEntityAsync(Guid id) =>
		await FindEntityAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionApplication), id);

	private async Task<AdmissionApplicationDto> MapAsync(AdmissionApplication application)
	{
		var query = (await applicationRepository.GetQueryableAsync()).Where(item => item.Id == application.Id);
		var projected = await AsyncExecuter.FirstAsync(await listQuery.ProjectAsync(await workspace.ProjectAsync(query)));
		var result = MapListRow(projected);
		result.ReviewDecision = await reviewGuard.GetCapabilitiesAsync(application);
		return result;
	}

	private static AdmissionApplicationDto MapListRow(AdmissionApplicationListRow row)
	{
		var result = AdmissionApplicationMapper.ToDto(row.Application, row.ApplicantName.Trim(), row.OfferingCode, row.CurrentStageName);
		result.AssignedCounsellorName = row.AssignedCounsellorName;
		ApplyWorkspaceDetails(result, row);
		return result;
	}

	private static void ApplyWorkspaceDetails(AdmissionApplicationDto result, AdmissionApplicationListRow row)
	{
		result.HasReachedDecision = row.HasReachedDecision;
		result.DecisionEnteredAt = row.HasReachedDecision && row.DecisionEnteredAt.HasValue ? AdmissionOfferUtcTime.Normalize(row.DecisionEnteredAt.Value) : null;
		result.StandardName = row.StandardName;
		result.ContactPhone = row.ContactPhone;
		result.ContactEmail = row.ContactEmail;
	}
}
