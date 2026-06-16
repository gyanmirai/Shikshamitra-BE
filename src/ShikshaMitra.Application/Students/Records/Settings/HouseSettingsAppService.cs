using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Students.Records.Settings;

[RemoteService(false)]
public class HouseSettingsAppService(
	IRepository<HouseSettings, Guid> houseSettingsRepository,
	IRepository<EducationMedium, Guid> academicMediumRepository,
	IGuidGenerator guidGenerator,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, IHouseSettingsAppService
{
	public async Task CreateHouseSettingsAsync(Guid academicMediumId, CreateUpdateHouseSettingsDto createHouseSettingsDto)
	{
		try
		{
			// validate academic medium exists
			var academicMedium = await academicMediumRepository.GetAsync(academicMediumId) ?? throw new EntityNotFoundException("Academic medium not found.");
			await studentScopeAccessService.EnsureCanAccessMediumAsync(
				academicMediumId,
				nameof(CreateHouseSettingsAsync));

			var normalizedName = NormalizeHouseName(createHouseSettingsDto.Name);
			await EnsureHouseNameIsUniqueAsync(academicMediumId, normalizedName);

			var houseSetting = new HouseSettings(
				guidGenerator.Create(),
				createHouseSettingsDto.Name.Trim(),
				createHouseSettingsDto.Color,
				academicMediumId,
				createHouseSettingsDto.LogoUrl,
				createHouseSettingsDto.Description
			);

			await houseSettingsRepository.InsertAsync(houseSetting, autoSave: true);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception)
		{
			throw new UserFriendlyException("An error occurred while creating house settings.");
		}
	}

	public async Task<HouseSettingsDto> GetHouseSettingsAsync(Guid houseSettingsId)
	{
		try
		{
			var houseSetting = await houseSettingsRepository.GetAsync(houseSettingsId) ?? throw new EntityNotFoundException("House setting not found.");
			await studentScopeAccessService.EnsureCanAccessMediumAsync(
				houseSetting.AcademicMediumId,
				nameof(GetHouseSettingsAsync));
			return ObjectMapper.Map<HouseSettings, HouseSettingsDto>(houseSetting);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception)
		{
			throw new UserFriendlyException("An error occurred while retrieving house settings.");
		}
	}

	public async Task<List<HouseSettingsDto>> GetHouseSettingsByAcademicMediumAsync(Guid academicMediumId)
	{
		try
		{
			// validate academic medium exists
			var academicMedium = await academicMediumRepository.GetAsync(academicMediumId) ?? throw new EntityNotFoundException("Academic medium not found.");
			await studentScopeAccessService.EnsureCanAccessMediumAsync(
				academicMediumId,
				nameof(GetHouseSettingsByAcademicMediumAsync));
			var houseSettings = await houseSettingsRepository.GetListAsync(h => h.AcademicMediumId == academicMediumId);
			return ObjectMapper.Map<List<HouseSettings>, List<HouseSettingsDto>>(houseSettings);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception)
		{
			throw new UserFriendlyException("An error occurred while retrieving house settings.");
		}
	}

	public async Task<HouseSettingsDto> UpdateHouseSettingsAsync(Guid houseSettingsId, CreateUpdateHouseSettingsDto updateHouseSettingsDto)
	{
		try
		{
			var houseSetting = await houseSettingsRepository.GetAsync(houseSettingsId) ?? throw new EntityNotFoundException("House setting not found.");
			await studentScopeAccessService.EnsureCanAccessMediumAsync(
				houseSetting.AcademicMediumId,
				nameof(UpdateHouseSettingsAsync));

			var normalizedName = NormalizeHouseName(updateHouseSettingsDto.Name);
			await EnsureHouseNameIsUniqueAsync(houseSetting.AcademicMediumId, normalizedName, houseSettingsId);

			houseSetting.UpdateDetails(
				updateHouseSettingsDto.Name.Trim(),
				updateHouseSettingsDto.Color,
				updateHouseSettingsDto.LogoUrl,
				updateHouseSettingsDto.Description
			);

			await houseSettingsRepository.UpdateAsync(houseSetting, autoSave: true);
			return ObjectMapper.Map<HouseSettings, HouseSettingsDto>(houseSetting);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception)
		{
			throw new UserFriendlyException("An error occurred while updating house settings.");
		}
	}

	public async Task DeleteHouseSettingsAsync(Guid houseSettingsId)
	{

		try
		{
			var houseSetting = await houseSettingsRepository.GetAsync(houseSettingsId) ?? throw new EntityNotFoundException("House setting not found.");
			await studentScopeAccessService.EnsureCanAccessMediumAsync(
				houseSetting.AcademicMediumId,
				nameof(DeleteHouseSettingsAsync));
			await houseSettingsRepository.DeleteAsync(houseSetting, autoSave: true);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception)
		{
			throw new UserFriendlyException("An error occurred while deleting house settings.");
		}
	}

	private async Task EnsureHouseNameIsUniqueAsync(
		Guid academicMediumId,
		string normalizedName,
		Guid? excludingHouseSettingsId = null)
	{
		var houseSettings = await houseSettingsRepository.GetListAsync(h =>
			h.AcademicMediumId == academicMediumId &&
			(!excludingHouseSettingsId.HasValue || h.Id != excludingHouseSettingsId.Value));

		if (houseSettings.Any(h => NormalizeHouseName(h.Name) == normalizedName))
		{
			throw new BusinessException(StudentErrorCodes.HouseSettingsAlreadyExists)
				.WithData("Name", normalizedName)
				.WithData("AcademicMediumId", academicMediumId);
		}
	}

	private static string NormalizeHouseName(string name)
		=> string.Join(" ", name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
			.ToUpperInvariant();
}
