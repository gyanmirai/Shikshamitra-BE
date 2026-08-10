using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadQueryWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> mediumStageRepository,
	AdmissionCrmApplicantProgressReadService progressRead,
	AdmissionCrmPipelineQueryService pipelineQuery,
	AdmissionCrmLeadDuplicateDetector duplicateDetector,
	AdmissionCrmLeadAccess access) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmLeadDto> GetAsync(Guid id)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(id, nameof(GetAsync));
		var duplicates = await duplicateDetector.FindDuplicatesAsync(
			lead.AcademicYearId,
			lead.NormalizedPrimaryContactNo,
			lead.NormalizedEmail,
			AdmissionCrmDuplicateProfileProbe.FromLead(lead),
			lead.Id);
		var progressByLead = await progressRead.GetForLeadsAsync([lead]);

		return AdmissionCrmLeadMapper.ToDto(lead, duplicates, progressByLead[lead.Id]);
	}

	public async Task<PagedResultDto<AdmissionCrmLeadDto>> GetListAsync(
		Guid academicYearId,
		GetAdmissionCrmLeadListInput input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetListAsync));

		var queryable = await leadRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId);
		query = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);

		if (input.Stage.HasValue)
		{
			query = query.Where(x => x.Stage == input.Stage.Value);
		}
		if (input.PipelineColumn.HasValue)
		{
			query = await pipelineQuery.ApplyColumnFilterAsync(
				query,
				input.PipelineColumn.Value,
				CurrentTenant.Id);
		}

		if (input.Channel.HasValue)
		{
			query = query.Where(x => x.Channel == input.Channel.Value);
		}

		if (input.InquirySourceId.HasValue)
		{
			query = query.Where(x => x.InquirySourceId == input.InquirySourceId.Value);
		}
		if (input.InquiryTypeId.HasValue)
		{
			query = query.Where(x => x.InquiryTypeId == input.InquiryTypeId.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.CampaignName))
		{
			var campaignName = input.CampaignName.Trim().ToLowerInvariant();
			query = query.Where(x =>
				x.CampaignName != null &&
				x.CampaignName.ToLower() == campaignName);
		}

		if (input.OwnerUserId.HasValue)
		{
			query = query.Where(x => x.OwnerUserId == input.OwnerUserId.Value);
		}

		if (input.Priority.HasValue)
		{
			query = query.Where(x => x.Priority == input.Priority.Value);
		}
		if (input.IntendedRouteType.HasValue)
		{
			query = query.Where(x => x.IntendedRouteType == input.IntendedRouteType.Value);
		}
		if (!string.IsNullOrWhiteSpace(input.DesiredClassName))
		{
			var desiredClass = input.DesiredClassName.Trim().ToLowerInvariant();
			query = query.Where(x => x.DesiredClassName != null && x.DesiredClassName.ToLower().Contains(desiredClass));
		}

		if (input.AcademicMediumId.HasValue)
		{
			await scopeAccess.EnsureCanAccessMediumAsync(
				input.AcademicMediumId.Value,
				nameof(GetListAsync));
			var stageQueryable = await mediumStageRepository.GetQueryableAsync();
			var stageIds = stageQueryable
				.Where(x => x.EducationMediumId == input.AcademicMediumId.Value)
				.Select(x => x.Id);
			var standardQueryable = await standardRepository.GetQueryableAsync();
			var standardIds = standardQueryable
				.Where(x => stageIds.Contains(x.EducationMediumStageId))
				.Select(x => x.Id);
			query = query.Where(x => !x.TargetStandardId.HasValue || standardIds.Contains(x.TargetStandardId.Value));
		}

		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			var filter = input.FilterText.Trim().ToLowerInvariant();
			var applicantNumber = AdmissionCrmLeadSorting.ParseApplicantNumber(input.FilterText);
			var normalizedPhone = AdmissionCrmLeadDuplicateMatcher.NormalizePhone(input.FilterText);
			query = query.Where(x =>
				(applicantNumber.HasValue && x.ApplicantNumber == applicantNumber.Value) ||
				(x.FirstName != null && x.FirstName.ToLower().Contains(filter)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(filter)) ||
				(x.ParentGuardianName != null && x.ParentGuardianName.ToLower().Contains(filter)) ||
				(x.Email != null && x.Email.ToLower().Contains(filter)) ||
				(normalizedPhone != null &&
				 x.NormalizedPrimaryContactNo != null &&
				 x.NormalizedPrimaryContactNo.Contains(normalizedPhone)));
		}
		if (!string.IsNullOrWhiteSpace(input.ApplicantSearch))
		{
			var applicant = input.ApplicantSearch.Trim().ToLowerInvariant();
			var applicantNumber = AdmissionCrmLeadSorting.ParseApplicantNumber(input.ApplicantSearch);
			query = query.Where(x =>
				(applicantNumber.HasValue && x.ApplicantNumber == applicantNumber.Value) ||
				(x.FirstName != null && x.FirstName.ToLower().Contains(applicant)) ||
				(x.LastName != null && x.LastName.ToLower().Contains(applicant)) ||
				(x.ParentGuardianName != null && x.ParentGuardianName.ToLower().Contains(applicant)));
		}
		if (!string.IsNullOrWhiteSpace(input.RegistrationFormNumber))
		{
			var formNumber = input.RegistrationFormNumber.Trim().ToLowerInvariant();
			query = query.Where(x => x.RegistrationFormNumber != null &&
				x.RegistrationFormNumber.ToLower().Contains(formNumber));
		}
		if (input.LastActivityFrom.HasValue)
			query = query.Where(x => x.LastActivityTime >= input.LastActivityFrom.Value);
		if (input.LastActivityUntil.HasValue)
			query = query.Where(x => x.LastActivityTime < input.LastActivityUntil.Value.AddDays(1));

		var totalCount = await AsyncExecuter.CountAsync(query);
		var leads = await GetSortedLeadPageAsync(query, input);
		var duplicatesByLeadId = await duplicateDetector.FindDuplicatesForLeadsAsync(academicYearId, leads);
		var progressByLeadId = await progressRead.GetForLeadsAsync(leads);

		return new PagedResultDto<AdmissionCrmLeadDto>(
			totalCount,
			leads.Select(x => AdmissionCrmLeadMapper.ToDto(
				x,
				duplicatesByLeadId.TryGetValue(x.Id, out var duplicates)
					? duplicates
					: Array.Empty<AdmissionCrmLeadDuplicateDto>(),
				progressByLeadId.GetValueOrDefault(x.Id))).ToList());
	}

	public async Task<IReadOnlyList<AdmissionCrmLeadDuplicateDto>> PreviewDuplicatesAsync(
		Guid academicYearId,
		AdmissionCrmLeadDuplicatePreviewInput input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(PreviewDuplicatesAsync));

		return await duplicateDetector.FindDuplicatesAsync(
			academicYearId,
			AdmissionCrmLeadDuplicateMatcher.NormalizePhone(input.PrimaryContactNo),
			string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant(),
			AdmissionCrmDuplicateProfileProbe.FromInput(input),
			excludeLeadId: null);
	}

	private async Task<List<AdmissionInquiryLead>> GetSortedLeadPageAsync(
		IQueryable<AdmissionInquiryLead> query,
		GetAdmissionCrmLeadListInput input)
	{
		try
		{
			return await AsyncExecuter.ToListAsync(
				AdmissionCrmLeadSorting.ApplyLeadSorting(query, input.Sorting)
					.Skip(input.SkipCount)
					.Take(input.MaxResultCount));
		}
		catch (NotSupportedException ex) when (AdmissionCrmLeadSorting.IsDateTimeOffsetOrderingNotSupported(ex) && AdmissionCrmLeadSorting.UsesLastActivitySorting(input.Sorting))
		{
			var leads = await AsyncExecuter.ToListAsync(query);
			return AdmissionCrmLeadSorting.ApplyLeadSortingInMemory(leads, input.Sorting)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
				.ToList();
		}
	}
}