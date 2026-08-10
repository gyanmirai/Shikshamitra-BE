using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicChecklistReader(
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	IRepository<AdmissionCrmApplicantChecklistProgress, Guid> progressRepository) : ApplicationService, ITransientDependency
{
	public async Task<List<AdmissionApplicantStatusChecklistItemDto>> GetAsync(AdmissionInquiryLead lead)
	{
		var configuration = await AsyncExecuter.FirstOrDefaultAsync((await cycleRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.AcademicYearId == lead.AcademicYearId));
		if (configuration == null)
		{
			return [];
		}

		var progressItems = await AsyncExecuter.ToListAsync((await progressRepository.GetQueryableAsync()).Where(item =>
			item.TenantId == CurrentTenant.Id && item.LeadId == lead.Id));
		var progressByKey = progressItems
			.GroupBy(item => item.ChecklistKey, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				group => group.Key,
				group => group.OrderByDescending(item => item.StatusChangedAt).First(),
				StringComparer.OrdinalIgnoreCase);
		return AdmissionCrmApplicantChecklistSerializer.DeserializeItems(configuration.ApplicantChecklistJson)
			.OrderBy(item => item.DisplayOrder)
			.ThenBy(item => item.Label)
			.Select(item => Map(item, progressByKey.GetValueOrDefault(item.Key)))
			.ToList();
	}

	private static AdmissionApplicantStatusChecklistItemDto Map(
		AdmissionCrmApplicantChecklistItemDto item,
		AdmissionCrmApplicantChecklistProgress? progress) => new()
	{
		Key = item.Key,
		Label = item.Label,
		Status = progress == null
			? item.IsRequired ? "Required" : "Optional"
			: GetStatusLabel(progress.Status),
		StatusCode = progress?.Status.ToString() ?? AdmissionCrmApplicantChecklistProgressStatus.Pending.ToString(),
		Details = item.Details,
		IsRequired = item.IsRequired,
		IsComplete = progress?.Status is AdmissionCrmApplicantChecklistProgressStatus.Received or
			AdmissionCrmApplicantChecklistProgressStatus.Waived,
		StatusChangedAt = progress?.StatusChangedAt
	};

	private static string GetStatusLabel(AdmissionCrmApplicantChecklistProgressStatus status) => status switch
	{
		AdmissionCrmApplicantChecklistProgressStatus.Requested => "Requested",
		AdmissionCrmApplicantChecklistProgressStatus.Received => "Received",
		AdmissionCrmApplicantChecklistProgressStatus.Waived => "Waived",
		_ => "Pending"
	};
}
