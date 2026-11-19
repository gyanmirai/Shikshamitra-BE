using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>One scalar read for an already authorized application, never a general academic catalogue.</summary>
[RemoteService(false)]
public class AdmissionApplicationPrintContextReader(
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<EducationBoard, Guid> boards,
	IRepository<EducationMedium, Guid> mediums,
	IRepository<Standard, Guid> standards,
	IRepository<AcademicYear, Guid> years) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionPrintAcademicDetailsDto?> ReadAsync(AdmissionApplication application, AdmissionPrintHeaderSettingsDto header)
	{
		if (!CurrentTenant.Id.HasValue || application.TenantId != CurrentTenant.Id) return null;
		var offeringQuery = (await offerings.GetQueryableAsync()).Where(item => item.Id == application.AdmissionOfferingId &&
			item.TenantId == CurrentTenant.Id && item.AdmissionCycleId == application.AdmissionCycleId);
		var cycleQuery = (await cycles.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var boardQuery = (await boards.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var mediumQuery = (await mediums.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var standardQuery = (await standards.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var yearQuery = (await years.GetQueryableAsync()).Where(item => item.TenantId == CurrentTenant.Id);
		var row = await AsyncExecuter.FirstOrDefaultAsync(
			from offering in offeringQuery
			join cycle in cycleQuery on offering.AdmissionCycleId equals cycle.Id
			join board in boardQuery on offering.BoardId equals board.Id
			join medium in mediumQuery on offering.MediumId equals medium.Id
			join standard in standardQuery on offering.StandardId equals standard.Id
			join year in yearQuery on cycle.AcademicYearId equals year.Id
			where board.AcademicYearId == year.Id && medium.EducationBoardId == board.Id
			select new { BoardType = board.Type, MediumName = medium.Name, ClassName = standard.Name, AcademicYearName = year.Name });
		return row == null ? null : new()
		{
			Board = header.Board == true ? row.BoardType.ToString() : null,
			Medium = header.Medium == true ? row.MediumName : null,
			Class = header.Class == true ? row.ClassName : null,
			AcademicYear = header.AcademicYear == true ? row.AcademicYearName : null
		};
	}
}
