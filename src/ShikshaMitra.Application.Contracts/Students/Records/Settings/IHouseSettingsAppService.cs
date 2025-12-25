using ShikshaMitra.Common.Responses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.Records.Settings;

public interface IHouseSettingsAppService
{
    Task CreateHouseSettingsAsync(Guid academicMediumId, CreateUpdateHouseSettingsDto createHouseSettingsDto);
	Task<HouseSettingsDto> UpdateHouseSettingsAsync(Guid houseSettingsId, CreateUpdateHouseSettingsDto updateHouseSettingsDto);
	Task<HouseSettingsDto> GetHouseSettingsAsync(Guid houseSettingsId);
	Task<List<HouseSettingsDto>> GetHouseSettingsByAcademicMediumAsync(Guid academicMediumId);
	Task DeleteHouseSettingsAsync(Guid houseSettingsId);
}
