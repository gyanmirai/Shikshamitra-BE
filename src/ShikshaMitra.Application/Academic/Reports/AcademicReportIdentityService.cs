using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Reports;

[RemoteService(false)]
public class AcademicReportIdentityService(SchoolIdentityDocumentService documents,
	IRepository<Standard, Guid> standards, IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards,
	IRepository<Division, Guid> divisions) : ApplicationService, ITransientDependency
{
	public async Task PrepareAsync(AcademicReportPreviewDto report, Guid? boardId = null, Guid? mediumId = null)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
		Guid? organizationId = null;
		var classes = report.NormalizedFilters.ClassIds?.Distinct().ToList() ?? [];
		if (classes.Count == 0 && report.NormalizedFilters.DivisionIds is { Count: > 0 } divisionIds)
		{
			var divisionQuery = await divisions.GetQueryableAsync();
			var selectedDivisions = await AsyncExecuter.ToListAsync(divisionQuery
				.Where(division => division.TenantId == CurrentTenant.Id && Enumerable.Contains(divisionIds, division.Id))
				.Select(division => new { division.Id, division.StandardId }));
			if (selectedDivisions.Count != divisionIds.Distinct().Count())
				throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
			classes = selectedDivisions.Select(division => division.StandardId).Distinct().ToList();
		}
		if (classes.Count > 0)
		{
			var classQuery = await standards.GetQueryableAsync();
			var stageQuery = await stages.GetQueryableAsync();
			var mediumQuery = await mediums.GetQueryableAsync();
			var boardQuery = await boards.GetQueryableAsync();
			var contexts = await AsyncExecuter.ToListAsync((from standard in classQuery
				join stage in stageQuery on standard.EducationMediumStageId equals stage.Id
				join medium in mediumQuery on stage.EducationMediumId equals medium.Id
				join board in boardQuery on medium.EducationBoardId equals board.Id
				where standard.TenantId == CurrentTenant.Id && stage.TenantId == CurrentTenant.Id
					&& medium.TenantId == CurrentTenant.Id && board.TenantId == CurrentTenant.Id
					&& Enumerable.Contains(classes, standard.Id)
				select new { standard.Id, BoardId = board.Id, MediumId = medium.Id, OrganizationId = stage.Id, board.AcademicYearId }));
			if (contexts.Count != classes.Count || contexts.Any(context =>
				(boardId.HasValue && context.BoardId != boardId.Value) ||
				(mediumId.HasValue && context.MediumId != mediumId.Value) ||
				(report.NormalizedFilters.AcademicYearId.HasValue && context.AcademicYearId != report.NormalizedFilters.AcademicYearId.Value)))
				throw new BusinessException(SchoolIdentityErrorCodes.InvalidContext);
			boardId ??= contexts.Select(context => context.BoardId).Distinct().Count() == 1 ? contexts[0].BoardId : null;
			mediumId ??= contexts.Select(context => context.MediumId).Distinct().Count() == 1 ? contexts[0].MediumId : null;
			if (contexts.Select(context => context.OrganizationId).Distinct().Count() == 1) organizationId = contexts[0].OrganizationId;
		}
		var snapshot = await documents.CaptureAsync(boardId, mediumId, report.NormalizedFilters.AcademicYearId, signature: false, organizationId: organizationId);
		var images = await documents.LoadAsync(snapshot);
		report.IdentityLogo = images.Logo; report.IdentityBanner = images.Banner;
		report.IdentitySchoolName = CurrentTenant.Name;
	}
}
