using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records.HouseManagement;

[RemoteService(false)]
public class HouseManagementQueryService(
	HouseManagementAccessService access,
	StudentRecordsEnrollmentScopeService enrollmentScope,
	IStudentRepository students,
	IRepository<HouseSettings, Guid> houses,
	IRepository<Division, Guid> divisions,
	IRepository<Standard, Guid> standards,
	IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationBoard, Guid> boards,
	StudentScopeAccessService scope,
	StudentRecordsPolicyResolver policies,
	StudentRecordsAccessService recordsAccess)
	: ApplicationService, ITransientDependency
{
	public async Task<IQueryable<HouseManagementStudentDto>> StudentsAsync(Guid mediumId)
	{
		var context = await access.GetAsync(mediumId);
		var roles = await recordsAccess.GetDatabaseRoleNamesAsync();
		var hideIdentity = (await policies.ResolveAsync(context.YearId)).Policy.IsMasked("Identity", roles);
		var eq = await enrollmentScope.GetQueryableAsync();
		var sq = await students.GetQueryableWithoutAutoIncludesAsync();
		var hq = (await houses.GetQueryableAsync()).Where(h => h.TenantId == CurrentTenant.Id && h.AcademicMediumId == mediumId);
		var dq = (await divisions.GetQueryableAsync()).Where(d => d.TenantId == CurrentTenant.Id);
		return
			from e in eq
			join s in sq on e.StudentId equals s.Id
			join c in await standards.GetQueryableAsync() on e.StandardId equals (Guid?)c.Id
			join stage in await stages.GetQueryableAsync() on c.EducationMediumStageId equals stage.Id
			join m in await mediums.GetQueryableAsync() on stage.EducationMediumId equals m.Id
			join b in await boards.GetQueryableAsync() on m.EducationBoardId equals b.Id
			join d in dq on e.DivisionId equals (Guid?)d.Id into ds
			from d in ds.DefaultIfEmpty()
			join h in hq on e.HouseId equals (Guid?)h.Id into hs
			from h in hs.DefaultIfEmpty()
			where e.IsActive && s.Status == StudentLifeCycleStatusEnum.Enrolled && m.Id == mediumId &&
				b.AcademicYearId == e.AcademicYearId && s.TenantId == CurrentTenant.Id && c.TenantId == CurrentTenant.Id &&
				stage.TenantId == CurrentTenant.Id && m.TenantId == CurrentTenant.Id && b.TenantId == CurrentTenant.Id &&
				(e.DivisionId == null || d != null && d.StandardId == c.Id)
			select new HouseManagementStudentDto
			{
				EnrollmentId = e.Id,
				StudentId = s.Id,
				StudentName = hideIdentity ? "Restricted" : (s.FirstName ?? "") + " " + (s.LastName ?? ""),
				AdmissionNumber = hideIdentity ? null : s.GrNumber,
				StandardId = c.Id,
				StandardName = c.Name,
				DivisionId = e.DivisionId,
				DivisionName = d == null ? null : d.Name,
				RollNumber = e.RollNumber,
				HouseId = h == null ? null : h.Id,
				HouseName = h == null ? null : h.Name,
				IsHouseLeader = h != null && h.LeaderEnrollmentId == e.Id,
				IsDivisionLeader = d != null && d.LeaderEnrollmentId == e.Id,
				ConcurrencyStamp = e.ConcurrencyStamp
			};
	}

	public async Task<PagedResultDto<HouseManagementStudentDto>> GetStudentsAsync(HouseManagementQuery input)
	{
		Validate(input);
		var q = await StudentsAsync(input.AcademicMediumId);
		if (input.StandardId.HasValue)
			q = q.Where(x => x.StandardId == input.StandardId);
		if (input.DivisionId.HasValue)
			q = q.Where(x => x.DivisionId == input.DivisionId);
		if (input.HouseId.HasValue)
			q = q.Where(x => x.HouseId == input.HouseId);
		if (input.UnassignedOnly)
			q = q.Where(x => x.HouseId == null);
		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var term = input.Filter.Trim().ToLower();
			q = q.Where(x => x.StudentName.ToLower().Contains(term));
		}

		var count = await AsyncExecuter.CountAsync(q);
		var sorted = input.Sorting switch
		{
			"studentName desc" => q.OrderByDescending(x => x.StudentName),
			"standardName asc" => q.OrderBy(x => x.StandardName),
			"standardName desc" => q.OrderByDescending(x => x.StandardName),
			"houseName asc" => q.OrderBy(x => x.HouseName),
			"houseName desc" => q.OrderByDescending(x => x.HouseName),
			_ => q.OrderBy(x => x.StudentName)
		};
		return new(count, await AsyncExecuter.ToListAsync(sorted.ThenBy(x => x.EnrollmentId).Skip(input.SkipCount).Take(input.MaxResultCount)));
	}

	public async Task<HouseManagementHousePageDto> GetHousesAsync(HouseManagementQuery input)
	{
		Validate(input);
		var context = await access.GetAsync(input.AcademicMediumId);
		var roster = await StudentsAsync(input.AcademicMediumId);
		var q = (await houses.GetQueryableAsync()).Where(h => h.TenantId == CurrentTenant.Id && h.AcademicMediumId == input.AcademicMediumId);
		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var term = input.Filter.Trim().ToLower();
			q = q.Where(x => x.Name.ToLower().Contains(term));
		}

		var count = await AsyncExecuter.CountAsync(q);
		var sorted = input.Sorting == "name desc" ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name);
		var items = await AsyncExecuter.ToListAsync(sorted.ThenBy(x => x.Id).Skip(input.SkipCount).Take(input.MaxResultCount)
			.Select(h => new HouseManagementHouseDto
			{
				Id = h.Id,
				Name = h.Name,
				Color = h.Color,
				Description = h.Description,
				LogoUrl = h.LogoUrl,
				ConcurrencyStamp = h.ConcurrencyStamp,
				CanManage = context.CanManage,
				MemberCount = roster.Count(s => s.HouseId == h.Id)
			}));
		var ids = items.Select(h => h.Id).ToArray();
		var leaders = await AsyncExecuter.ToListAsync(roster.Where(s => s.IsHouseLeader && s.HouseId.HasValue && Enumerable.Contains(ids, s.HouseId.Value)));
		foreach (var item in items)
			item.Leader = leaders.SingleOrDefault(s => s.HouseId == item.Id);
		return new HouseManagementHousePageDto
		{
			TotalCount = count,
			Items = items,
			CanManage = context.CanManage
		};
	}

	public async Task<PagedResultDto<HouseManagementDivisionDto>> GetDivisionsAsync(HouseManagementQuery input)
	{
		Validate(input);
		var context = await access.GetAsync(input.AcademicMediumId);
		var roster = await StudentsAsync(input.AcademicMediumId);
		var dq = await scope.ApplyCurrentUserDivisionScopeAsync(await divisions.GetQueryableAsync(), context.YearId);
		var q =
			from d in dq
			join c in await standards.GetQueryableAsync() on d.StandardId equals c.Id
			join stage in await stages.GetQueryableAsync() on c.EducationMediumStageId equals stage.Id
			where stage.EducationMediumId == input.AcademicMediumId && c.TenantId == CurrentTenant.Id &&
				stage.TenantId == CurrentTenant.Id && (!input.StandardId.HasValue || c.Id == input.StandardId)
			select new HouseManagementDivisionDto
			{
				Id = d.Id,
				Name = d.Name,
				StandardId = c.Id,
				StandardName = c.Name,
				ConcurrencyStamp = d.ConcurrencyStamp,
				CanManage = true,
				StudentCount = roster.Count(s => s.DivisionId == d.Id)
			};
		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var term = input.Filter.Trim().ToLower();
			q = q.Where(x => x.Name.ToLower().Contains(term) || x.StandardName.ToLower().Contains(term));
		}

		var count = await AsyncExecuter.CountAsync(q);
		var sorted = input.Sorting == "name desc" ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name);
		var items = await AsyncExecuter.ToListAsync(sorted.ThenBy(x => x.Id).Skip(input.SkipCount).Take(input.MaxResultCount));
		var ids = items.Select(d => d.Id).ToArray();
		var leaders = await AsyncExecuter.ToListAsync(roster.Where(s => s.IsDivisionLeader && s.DivisionId.HasValue && Enumerable.Contains(ids, s.DivisionId.Value)));
		foreach (var item in items)
			item.Leader = leaders.SingleOrDefault(s => s.DivisionId == item.Id);
		return new(count, items);
	}

	private static void Validate(HouseManagementQuery input)
	{
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.Filter?.Length > 150)
			throw new BusinessException(HouseManagementErrorCodes.InvalidInput);
	}
}
