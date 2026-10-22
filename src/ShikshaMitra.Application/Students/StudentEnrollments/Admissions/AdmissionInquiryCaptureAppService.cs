using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Create)]
public class AdmissionInquiryCaptureAppService(
	AdmissionOfferingReadService offeringReader,
	AdmissionFormAppService formReader, AdmissionInquiryPolicyResolver policies,
	IRepository<AdmissionCycle, Guid> cycles, IRepository<EducationBoard, Guid> boards,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationMediumStage, Guid> stages,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<AcademicYear, Guid> academicYears,
	IUserScopeAccessService scopes) : ApplicationService, IAdmissionInquiryCaptureAppService
{
	public async Task<AdmissionInquiryCaptureContextDto> GetContextAsync(Guid offeringId)
	{
		var available = await offeringReader.GetAvailabilityAsync(offeringId);
		var offering = await offeringReader.GetAsync(offeringId);
		// Resolve Institute Setup settings only after the offering's scope has been checked.
		var standardQuery = await standards.GetQueryableAsync();
		var ageSettings = await AsyncExecuter.FirstOrDefaultAsync(
			from cycle in await cycles.GetQueryableAsync()
			join year in await academicYears.GetQueryableAsync() on cycle.AcademicYearId equals year.Id
			from standard in standardQuery
			where cycle.Id == offering.AdmissionCycleId && standard.Id == offering.StandardId
			select new { year.StartDate, standard.AgeLowerLimit });
		return new AdmissionInquiryCaptureContextDto
		{
			AcademicYearStartDate = ageSettings == null ? null : DateOnly.FromDateTime(ageSettings.StartDate),
			MinimumAgeYears = ageSettings?.AgeLowerLimit > 0 ? ageSettings.AgeLowerLimit : null,
			Offering = offering, Availability = available,
			Form = available.CanCreateInquiry && offering.InquiryFormDefinitionId.HasValue
				? await formReader.GetPublishedIntakeAsync(offering.InquiryFormDefinitionId.Value) : null,
			Policy = (await policies.ResolveAsync(offeringId))?.Policy
		};
	}

	public async Task<AdmissionInquiryCaptureCatalogueDto> GetCatalogueAsync(Guid? academicYearId = null)
	{
		var scope = await scopes.GetCurrentUserScopeAsync();
		var tenantWide = scope.IsAdmin || scope.HasTenantWideScope();
		var cycleQuery = (await cycles.GetQueryableAsync()).Where(item => item.Status == AdmissionCycleStatus.Published &&
			(!academicYearId.HasValue || item.AcademicYearId == academicYearId) &&
			(tenantWide || scope.AcademicYearIds.Contains(item.AcademicYearId)));
		var periods = await AsyncExecuter.ToListAsync(cycleQuery.OrderBy(item => item.Name));
		var yearIds = periods.Select(item => item.AcademicYearId).Distinct().ToList();
		var divisionQuery = await divisions.GetQueryableAsync();
		var query = from board in await boards.GetQueryableAsync()
			join medium in await mediums.GetQueryableAsync() on board.Id equals medium.EducationBoardId
			join stage in await stages.GetQueryableAsync() on medium.Id equals stage.EducationMediumId
			join standard in await standards.GetQueryableAsync() on stage.Id equals standard.EducationMediumStageId
			where yearIds.Contains(board.AcademicYearId) && (tenantWide ||
				scope.WildcardBoardAcademicYearIds.Contains(board.AcademicYearId) ||
				scope.WildcardMediumBoardIds.Contains(board.Id) || scope.WildcardMediumStageMediumIds.Contains(medium.Id) ||
				scope.WildcardStandardMediumStageIds.Contains(stage.Id) || scope.WildcardDivisionStandardIds.Contains(standard.Id) ||
				divisionQuery.Any(division => division.StandardId == standard.Id && scope.DivisionIds.Contains(division.Id)))
			select new { BoardId = board.Id, board.Type, MediumId = medium.Id, MediumName = medium.Name,
				StandardId = standard.Id, StandardName = standard.Name };
		var result = new AdmissionInquiryCaptureCatalogueDto
		{
			Periods = periods.Select(item => new AdmissionInquiryCapturePeriodDto { Id = item.Id, Name = item.Name }).ToList(),
			AcademicOptions = (await AsyncExecuter.ToListAsync(query)).Select(item => new PublicAdmissionInquiryAcademicOptionDto
			{
				BoardId = item.BoardId, BoardName = item.Type.ToString(), MediumId = item.MediumId,
				MediumName = item.MediumName, StandardId = item.StandardId, StandardName = item.StandardName
			}).ToList()
		};
		foreach (var period in periods)
		{
			for (var skip = 0; ; skip += 100)
			{
				var page = await offeringReader.GetListAsync(new AdmissionOfferingListInput
				{
					AdmissionCycleId = period.Id, MaxResultCount = 100, SkipCount = skip
				});
				result.Offerings.AddRange(page.Items.Where(item => item.EntryMode != AdmissionEntryMode.DirectRegistration));
				if (skip + page.Items.Count >= page.TotalCount) break;
			}
		}
		return result;
	}
}
