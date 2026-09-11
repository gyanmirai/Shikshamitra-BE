using System;
using System.Collections.Generic;
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
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionApplicationStageInstance, Guid> stageRepository,
	AdmissionApplicationScopeAccessService scopeAccess,
	AdmissionReviewDecisionGuard reviewGuard)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionApplicationDto>> GetListAsync(AdmissionApplicationListInput input)
	{
		var query = await applicationRepository.GetQueryableAsync();
		var visibleOfferings = await scopeAccess.GetScopedOfferingsAsync();
		query = query.Where(application => visibleOfferings.Any(offering => offering.Id == application.AdmissionOfferingId));
		var applicantIds = await GetMatchingApplicantIdsAsync(input.Search);
		var stageIds = await GetMatchingStageIdsAsync(input.CurrentStageSearch);
		query = query.Where(application =>
			(!input.Status.HasValue || application.Status == input.Status) &&
			(!input.AdmissionCycleId.HasValue || application.AdmissionCycleId == input.AdmissionCycleId) &&
			(!input.AdmissionOfferingId.HasValue || application.AdmissionOfferingId == input.AdmissionOfferingId) &&
			(!input.CurrentStageInstanceId.HasValue || application.CurrentStageInstanceId == input.CurrentStageInstanceId) &&
			(string.IsNullOrWhiteSpace(input.CurrentStageSearch) ||
				(application.CurrentStageInstanceId.HasValue && stageIds!.Contains(application.CurrentStageInstanceId.Value))) &&
			(string.IsNullOrWhiteSpace(input.Search) || application.ApplicationNumber.Contains(input.Search) ||
				(applicantIds != null && applicantIds.Contains(application.ApplicantId))));
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(application => application.CreationTime)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionApplicationDto>(count, await MapRowsAsync(rows));
	}

	public async Task<AdmissionApplicationDto> GetAsync(Guid id)
	{
		var application = await GetEntityAsync(id);
		var applicant = await applicantRepository.FindAsync(application.ApplicantId);
		var result = AdmissionApplicationMapper.ToDto(application, applicant == null ? string.Empty : GetName(applicant));
		result.ReviewDecision = await reviewGuard.GetCapabilitiesAsync(application);
		return result;
	}

	public async Task<AdmissionApplication> GetEntityAsync(Guid id)
	{
		var application = await applicationRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionApplication), id);
		await scopeAccess.EnsureCanAccessAsync(application);
		return application;
	}

	private async Task<List<Guid>?> GetMatchingApplicantIdsAsync(string? search)
	{
		if (string.IsNullOrWhiteSpace(search)) return null;
		var query = await applicantRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query
			.Where(applicant => (applicant.FirstName + " " + (applicant.LastName ?? string.Empty)).Contains(search))
			.Select(applicant => applicant.Id));
	}

	private async Task<List<Guid>?> GetMatchingStageIdsAsync(string? search)
	{
		if (string.IsNullOrWhiteSpace(search)) return null;
		var query = await stageRepository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(query
			.Where(stage => stage.Name.Contains(search))
			.Select(stage => stage.Id));
	}

	private async Task<List<AdmissionApplicationDto>> MapRowsAsync(List<AdmissionApplication> rows)
	{
		var applicantIds = rows.Select(row => row.ApplicantId).Distinct().ToList();
		var offeringIds = rows.Select(row => row.AdmissionOfferingId).Distinct().ToList();
		var stageIds = rows.Where(row => row.CurrentStageInstanceId.HasValue)
			.Select(row => row.CurrentStageInstanceId!.Value).Distinct().ToList();
		var applicants = (await applicantRepository.GetListAsync(item => applicantIds.Contains(item.Id)))
			.ToDictionary(item => item.Id);
		var offerings = (await offeringRepository.GetListAsync(item => offeringIds.Contains(item.Id)))
			.ToDictionary(item => item.Id);
		var stages = (await stageRepository.GetListAsync(item => stageIds.Contains(item.Id)))
			.ToDictionary(item => item.Id);
		return rows.Select(row => AdmissionApplicationMapper.ToDto(
			row,
			applicants.TryGetValue(row.ApplicantId, out var applicant) ? GetName(applicant) : string.Empty,
			offerings.GetValueOrDefault(row.AdmissionOfferingId)?.Code ?? string.Empty,
			row.CurrentStageInstanceId.HasValue
				? stages.GetValueOrDefault(row.CurrentStageInstanceId.Value)?.Name ?? string.Empty
				: string.Empty)).ToList();
	}

	private static string GetName(AdmissionApplicant applicant) => string.Join(" ",
		new[] { applicant.FirstName, applicant.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
