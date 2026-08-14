using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionCycleAppService(
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AcademicYear, Guid> academicYearRepository) : ApplicationService, IAdmissionCycleAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<PagedResultDto<AdmissionCycleDto>> GetListAsync(AdmissionCycleListInput input)
	{
		var queryable = await cycleRepository.GetQueryableAsync();
		queryable = queryable.Where(x =>
			(string.IsNullOrWhiteSpace(input.Search) || x.Name.Contains(input.Search) || x.Code.Contains(input.Search)) &&
			(!input.AcademicYearId.HasValue || x.AcademicYearId == input.AcademicYearId.Value) &&
			(!input.Status.HasValue || x.Status == input.Status.Value));

		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var rows = await AsyncExecuter.ToListAsync(queryable
			.OrderByDescending(x => x.CreationTime)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCycleDto>(totalCount, rows.Select(Map).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.View)]
	public async Task<AdmissionCycleDto> GetAsync(Guid id) => Map(await GetCycleAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Create)]
	public async Task<AdmissionCycleDto> CreateAsync(CreateUpdateAdmissionCycleDto input)
	{
		await EnsureAcademicYearExistsAsync(input.AcademicYearId);
		await EnsureCodeIsUniqueAsync(input.Code);

		var cycle = new AdmissionCycle(GuidGenerator.Create(), CurrentTenant.Id, input.Name, input.Code,
			input.AcademicYearId, input.StartDate, input.EndDate);
		await cycleRepository.InsertAsync(cycle, autoSave: true);
		return Map(cycle);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Edit)]
	public async Task<AdmissionCycleDto> UpdateAsync(Guid id, CreateUpdateAdmissionCycleDto input)
	{
		var cycle = await GetCycleAsync(id);
		await EnsureAcademicYearExistsAsync(input.AcademicYearId);
		await EnsureCodeIsUniqueAsync(input.Code, id);
		cycle.UpdateDetails(input.Name, input.Code, input.AcademicYearId, input.StartDate, input.EndDate);
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		return Map(cycle);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Publish)]
	public async Task<AdmissionCycleDto> PublishAsync(Guid id)
	{
		var cycle = await GetCycleAsync(id);
		cycle.Publish(CurrentUser.Id, Clock.Now);
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		return Map(cycle);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Close)]
	public async Task<AdmissionCycleDto> CloseAsync(Guid id)
	{
		var cycle = await GetCycleAsync(id);
		cycle.Close();
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		return Map(cycle);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Admissions.Archive)]
	public async Task<AdmissionCycleDto> ArchiveAsync(Guid id)
	{
		var cycle = await GetCycleAsync(id);
		cycle.Archive();
		await cycleRepository.UpdateAsync(cycle, autoSave: true);
		return Map(cycle);
	}

	private async Task<AdmissionCycle> GetCycleAsync(Guid id) =>
		await cycleRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionCycle), id);

	private async Task EnsureAcademicYearExistsAsync(Guid id)
	{
		if (id == Guid.Empty || await academicYearRepository.FindAsync(id) == null)
		{
			throw new EntityNotFoundException(typeof(AcademicYear), id);
		}
	}

	private async Task EnsureCodeIsUniqueAsync(string code, Guid? excludingId = null)
	{
		var normalized = code.Trim().ToUpperInvariant();
		var queryable = await cycleRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(queryable.Where(x => x.Code == normalized &&
			(!excludingId.HasValue || x.Id != excludingId.Value)));
		if (exists)
		{
			throw new BusinessException(AdmissionErrorCodes.CycleCodeAlreadyExists).WithData("Code", normalized);
		}
	}

	private static AdmissionCycleDto Map(AdmissionCycle cycle) => new()
	{
		Id = cycle.Id,
		Name = cycle.Name,
		Code = cycle.Code,
		AcademicYearId = cycle.AcademicYearId,
		StartDate = cycle.StartDate,
		EndDate = cycle.EndDate,
		Status = cycle.Status,
		PublishedAt = cycle.PublishedAt,
		CreationTime = cycle.CreationTime,
		CreatorId = cycle.CreatorId,
		LastModificationTime = cycle.LastModificationTime,
		LastModifierId = cycle.LastModifierId
	};
}
