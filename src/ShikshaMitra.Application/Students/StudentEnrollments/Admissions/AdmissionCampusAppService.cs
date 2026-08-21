using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionCampusAppService(
	IRepository<AdmissionCampus, Guid> campusRepository) : ApplicationService, IAdmissionCampusAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<PagedResultDto<AdmissionCampusDto>> GetListAsync(AdmissionCampusListInput input)
	{
		var query = await campusRepository.GetQueryableAsync();
		query = query.Where(campus =>
			(string.IsNullOrWhiteSpace(input.Search) || campus.Name.Contains(input.Search) || campus.Code.Contains(input.Search)) &&
			(!input.IsActive.HasValue || campus.IsActive == input.IsActive.Value));

		var totalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderBy(campus => campus.Name)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));

		return new PagedResultDto<AdmissionCampusDto>(totalCount, rows.Select(Map).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<AdmissionCampusDto> GetAsync(Guid id) => Map(await GetCampusAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<AdmissionCampusDto> CreateAsync(CreateUpdateAdmissionCampusDto input)
	{
		var code = NormalizeCode(input.Code);
		await EnsureCodeIsUniqueAsync(code);
		var campus = new AdmissionCampus(GuidGenerator.Create(), CurrentTenant.Id, code, input.Name);
		await campusRepository.InsertAsync(campus, autoSave: true);
		return Map(campus);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<AdmissionCampusDto> UpdateAsync(Guid id, CreateUpdateAdmissionCampusDto input)
	{
		var campus = await GetCampusAsync(id);
		var code = NormalizeCode(input.Code);
		await EnsureCodeIsUniqueAsync(code, id);
		campus.UpdateDetails(code, input.Name);
		await campusRepository.UpdateAsync(campus, autoSave: true);
		return Map(campus);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<AdmissionCampusDto> ActivateAsync(Guid id)
	{
		var campus = await GetCampusAsync(id);
		campus.Activate();
		await campusRepository.UpdateAsync(campus, autoSave: true);
		return Map(campus);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<AdmissionCampusDto> DeactivateAsync(Guid id)
	{
		var campus = await GetCampusAsync(id);
		campus.Deactivate();
		await campusRepository.UpdateAsync(campus, autoSave: true);
		return Map(campus);
	}

	private async Task<AdmissionCampus> GetCampusAsync(Guid id) =>
		await campusRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionCampus), id);

	private async Task EnsureCodeIsUniqueAsync(string code, Guid? excludingId = null)
	{
		var query = await campusRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(campus =>
			campus.Code == code && (!excludingId.HasValue || campus.Id != excludingId.Value))))
		{
			throw new BusinessException(AdmissionErrorCodes.CampusCodeAlreadyExists)
				.WithData("Code", code);
		}
	}

	private static string NormalizeCode(string code) =>
		Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();

	private static AdmissionCampusDto Map(AdmissionCampus campus) => new()
	{
		Id = campus.Id,
		Code = campus.Code,
		Name = campus.Name,
		IsActive = campus.IsActive,
		CreationTime = campus.CreationTime,
		CreatorId = campus.CreatorId,
		LastModificationTime = campus.LastModificationTime,
		LastModifierId = campus.LastModifierId,
		IsDeleted = campus.IsDeleted,
		DeleterId = campus.DeleterId,
		DeletionTime = campus.DeletionTime
	};
}
