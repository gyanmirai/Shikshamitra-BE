using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class DivisionManager(IDivisionRepository divisionRepository, IGuidGenerator guidGenerator) : DomainService
{
	public async Task<Division> CreateAsync(
		Guid standardId,
		string name,
		int strength,
		string roomNumber = "")
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		name = name.Trim();
		await ValidateDivisionAsync(name, standardId);

		return new Division(
			guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			standardId,
			strength,
			roomNumber
		);
	}

	public async Task<Division> UpdateAsync(
		Division division,
		string newName,
		int newStrength,
		string? roomNumber = null)
	{
		Check.NotNull(division, nameof(division));
		Check.NotNullOrWhiteSpace(newName, nameof(newName));
		newName = newName.Trim();

		if (division.Name == newName && division.Strength == newStrength && (roomNumber == null || division.RoomNumber == roomNumber))
		{
			return division;
		}

		if (division.Name != newName)
		{
			await ValidateDivisionAsync(newName, division.StandardId, division.Id);
		}

		division.SetName(newName);
		division.SetStrength(newStrength);
		if (roomNumber != null)
		{
			division.RoomNumber = roomNumber;
		}

		return division;
	}

	private async Task ValidateDivisionAsync(string name, Guid standardId, Guid? excludedId = null)
	{
		if (await divisionRepository.FindByNameAndStandardIdAsync(name, standardId, excludedId) != null)
		{
			throw new BusinessException(TenantSettingsDomainErrorCodes.DuplicateDivisionName)
				.WithData("name", name);
		}
	}
}
