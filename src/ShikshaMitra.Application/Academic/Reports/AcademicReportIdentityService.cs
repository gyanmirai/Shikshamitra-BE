using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
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
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards) : ApplicationService, ITransientDependency
{
	public async Task PrepareAsync(AcademicReportPreviewDto report, Guid? boardId = null, Guid? mediumId = null)
	{
		Guid? organizationId = null;
		if (!boardId.HasValue && report.NormalizedFilters.ClassIds is { Count: > 0 } classes)
		{
			var classQuery = await standards.GetQueryableAsync();
			var stageQuery = await stages.GetQueryableAsync();
			var mediumQuery = await mediums.GetQueryableAsync();
			var boardQuery = await boards.GetQueryableAsync();
			var contexts = await AsyncExecuter.ToListAsync((from standard in classQuery
				join stage in stageQuery on standard.EducationMediumStageId equals stage.Id
				join medium in mediumQuery on stage.EducationMediumId equals medium.Id
				join board in boardQuery on medium.EducationBoardId equals board.Id
				where standard.TenantId == CurrentTenant.Id && classes.Contains(standard.Id)
				select new { BoardId = board.Id, MediumId = medium.Id, OrganizationId = stage.Id }).Distinct());
			if (contexts.Count > 0 && contexts.Select(x => x.BoardId).Distinct().Count() == 1)
			{
				boardId = contexts[0].BoardId;
				if (contexts.Select(x => x.MediumId).Distinct().Count() == 1) mediumId = contexts[0].MediumId;
				if (contexts.Select(x => x.OrganizationId).Distinct().Count() == 1) organizationId = contexts[0].OrganizationId;
			}
		}
		var snapshot = await documents.CaptureAsync(boardId, mediumId, report.NormalizedFilters.AcademicYearId, signature: false, organizationId: organizationId);
		var images = await documents.LoadAsync(snapshot);
		report.IdentityLogo = images.Logo; report.IdentityBanner = images.Banner;
		report.IdentitySchoolName = CurrentTenant.Name;
	}
}
