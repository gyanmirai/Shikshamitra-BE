using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Students.Attendance;

internal sealed record AttendanceApiSeedContext(Guid StandardId, Guid MediumId, Guid StageId,
	Guid BoardId, Guid YearId, Guid DivisionId, DateTime YearStart, DateTime YearEnd)
{
	public static async Task<AttendanceApiSeedContext> ReadAsync(IServiceProvider services, Guid tenantId,
		Guid? selectedDivisionId, DateTime schoolToday)
	{
		var query = from division in await services.GetRequiredService<IRepository<Division, Guid>>().GetQueryableAsync()
			join standard in await services.GetRequiredService<IRepository<Standard, Guid>>().GetQueryableAsync() on division.StandardId equals standard.Id
			join stage in await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetQueryableAsync() on standard.EducationMediumStageId equals stage.Id
			join medium in await services.GetRequiredService<IRepository<EducationMedium, Guid>>().GetQueryableAsync() on stage.EducationMediumId equals medium.Id
			join board in await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetQueryableAsync() on medium.EducationBoardId equals board.Id
			join year in await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetQueryableAsync() on board.AcademicYearId equals year.Id
			where year.TenantId == tenantId && (selectedDivisionId.HasValue ? division.Id == selectedDivisionId.Value : year.StartDate <= schoolToday && year.EndDate >= schoolToday)
			orderby board.Id, medium.Id, stage.Id, standard.Id, division.Id
			select new AttendanceApiSeedContext(standard.Id, medium.Id, stage.Id, board.Id, year.Id, division.Id, year.StartDate, year.EndDate);
		return await services.GetRequiredService<IAsyncQueryableExecuter>().FirstAsync(query);
	}
}
