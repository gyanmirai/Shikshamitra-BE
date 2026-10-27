using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization;

namespace ShikshaMitra.TenantSettings.Reports;

public partial class InstituteReportData
{
	private async Task<List<string>> ValidateFiltersAsync(InstituteReportRequest request, Guid user)
	{
		var names = new List<string>();
		var tenant = CurrentTenant.Id;
		foreach (var property in typeof(InstituteReportFilters).GetProperties())
		{
			if (property.GetValue(request.Filters) is not Guid id) continue;
			string? name;
			if (InstituteReportCatalog.IsIdentity(request.ReportType)) name = await identities.FilterNameAsync(user, property.Name, id);
			else if (property.Name == nameof(InstituteReportFilters.ShiftId))
				name = await AsyncExecuter.FirstOrDefaultAsync((await shifts.GetQueryableAsync()).Where(x => x.TenantId == tenant && x.Id == id).Select(x => x.ShiftName));
			else if (property.Name == nameof(InstituteReportFilters.BuildingId))
				name = await AsyncExecuter.FirstOrDefaultAsync((await buildings.GetQueryableAsync()).Where(x => x.TenantId == tenant && x.Id == id).Select(x => x.BuildingName));
			else if (property.Name == nameof(InstituteReportFilters.RoomTypeId))
				name = await AsyncExecuter.FirstOrDefaultAsync((await roomTypes.GetQueryableAsync()).Where(x => x.TenantId == tenant && x.Id == id).Select(x => x.RoomTypeName));
			else if (property.Name == nameof(InstituteReportFilters.FloorId))
			{
				var buildingQuery = (await buildings.GetQueryableAsync()).Where(x => x.TenantId == tenant);
				name = await AsyncExecuter.FirstOrDefaultAsync(from floor in await floors.GetQueryableAsync()
					join building in buildingQuery on floor.BuildingId equals building.Id where floor.Id == id select floor.FloorName);
			}
			else
			{
				var query = await HierarchyQueryAsync(user, new());
				var selected = property.Name switch
				{
					nameof(InstituteReportFilters.AcademicYearId) => query.Where(x => x.AcademicYearId == id).Select(x => x.Year),
					nameof(InstituteReportFilters.BoardId) => query.Where(x => x.EducationBoardId == id).Select(x => x.Board),
					nameof(InstituteReportFilters.MediumId) => query.Where(x => x.EducationMediumId == id).Select(x => x.Medium),
					nameof(InstituteReportFilters.StageId) => query.Where(x => x.EducationMediumStageId == id).Select(x => x.Stage),
					nameof(InstituteReportFilters.StandardId) => query.Where(x => x.StandardId == id).Select(x => x.Standard),
					nameof(InstituteReportFilters.DivisionId) => query.Where(x => x.DivisionId == id).Select(x => x.Division),
					_ => throw new AbpAuthorizationException()
				};
				name = await AsyncExecuter.FirstOrDefaultAsync(selected);
			}
			if (name == null) throw new AbpAuthorizationException("A report filter is outside your permitted scope.");
			if (!InstituteReportCatalog.IsIdentity(request.ReportType) && property.Name == nameof(InstituteReportFilters.BoardId))
				name = (await InstituteReportBoardNames.CustomAsync((await boards.GetQueryableAsync()).Where(x => x.TenantId == tenant), [id], AsyncExecuter)).GetValueOrDefault(id, name);
			names.Add(name);
		}
		return names;
	}
}
