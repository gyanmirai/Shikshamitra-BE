using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Custody;

internal sealed record LibraryCustodyReferenceSnapshot(string? Custodian, string? ClassName, string? RoomName,
	DateOnly YearStartsOn, DateOnly YearEndsOn);

public class LibraryCustodyReferences(IRepository<AcademicYear, Guid> years, IRepository<Employee, Guid> employees,
	IRepository<Standard, Guid> standards, IRepository<EducationMediumStage, Guid> stages, IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationBoard, Guid> boards, IRepository<Room, Guid> rooms, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<LibraryCustodyOptionsDto> GetOptionsAsync(Guid tenant, Guid academicYear, Guid[] allowed)
	{
		var authorizedYears = await queries.ToListAsync((await years.GetQueryableAsync()).Where(row => row.TenantId == tenant &&
			Enumerable.Contains(allowed, row.Id)).OrderByDescending(row => row.StartDate).ThenBy(row => row.Id)
			.Select(row => new LibraryCustodyLabelDto { Id = row.Id, Name = row.Name }));
		var employeeQuery = await ActiveEmployeesAsync(tenant);
		var classQuery = await ClassesAsync(tenant, academicYear);
		var roomQuery = await RoomQueryAsync(tenant);
		return new(authorizedYears,
			await queries.ToListAsync(employeeQuery.OrderBy(row => row.Name).ThenBy(row => row.Id)),
			await queries.ToListAsync(classQuery.OrderBy(row => row.Name).ThenBy(row => row.Id)),
			await queries.ToListAsync(roomQuery.OrderBy(row => row.Name).ThenBy(row => row.Id)));
	}

	internal async Task<LibraryCustodyReferenceSnapshot> ReadAsync(Guid tenant, LibraryCustodyHandoffInput input)
	{
		var employee = await queries.FirstOrDefaultAsync((await ActiveEmployeesAsync(tenant)).Where(row => row.Id == input.EmployeeId));
		var year = await years.FindAsync(row => row.TenantId == tenant && row.Id == input.AcademicYearId);
		var group = input.StandardId.HasValue ? await queries.FirstOrDefaultAsync((await ClassesAsync(tenant, input.AcademicYearId))
			.Where(row => row.Id == input.StandardId)) : null;
		var room = input.RoomId.HasValue ? await queries.FirstOrDefaultAsync((await RoomQueryAsync(tenant))
			.Where(row => row.Id == input.RoomId)) : null;
		return new(employee?.Name.Trim(), group?.Name, room?.Name, DateOnly.FromDateTime(year?.StartDate ?? default),
			DateOnly.FromDateTime(year?.EndDate ?? default));
	}

	private async Task<IQueryable<LibraryCustodyLabelDto>> ActiveEmployeesAsync(Guid tenant) =>
		(await employees.GetQueryableAsync()).Where(row => row.TenantId == tenant && row.EmployeeStatusType == EmployeeStatusType.Active)
			.Select(row => new LibraryCustodyLabelDto { Id = row.Id, Name = (row.FirstName ?? "") + " " + (row.LastName ?? "") });

	private async Task<IQueryable<LibraryCustodyLabelDto>> RoomQueryAsync(Guid tenant) =>
		(await rooms.GetQueryableAsync()).Where(row => row.TenantId == tenant)
			.Select(row => new LibraryCustodyLabelDto { Id = row.Id, Name = row.RoomName });

	private async Task<IQueryable<LibraryCustodyLabelDto>> ClassesAsync(Guid tenant, Guid year)
	{
		var classRows = await standards.GetQueryableAsync();
		var stageRows = await stages.GetQueryableAsync();
		var mediumRows = await mediums.GetQueryableAsync();
		var boardRows = await boards.GetQueryableAsync();
		return from row in classRows
			join stage in stageRows on row.EducationMediumStageId equals stage.Id
			join medium in mediumRows on stage.EducationMediumId equals medium.Id
			join board in boardRows on medium.EducationBoardId equals board.Id
			where row.TenantId == tenant && stage.TenantId == tenant && medium.TenantId == tenant &&
				board.TenantId == tenant && board.AcademicYearId == year
			select new LibraryCustodyLabelDto { Id = row.Id, Name = stage.Name + " / " + row.Name };
	}
}
