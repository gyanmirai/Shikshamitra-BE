using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.AcademicSetups.Services;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class DivisionManager(IDivisionRepository divisionRepository, IGuidGenerator guidGenerator, AcademicFieldFormattingStore formatting) : DomainService
{
	public async Task<Division> CreateAsync(
		Guid standardId,
		string name,
		int strength,
		string roomNumber = "")
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		name = AcademicNameFormatter.Format(name, (await formatting.GetAsync()).Division);
		name = name.Trim();
		await ValidateDivisionAsync(name, standardId);

		var nextOrder = await AsyncExecuter.MaxAsync((await divisionRepository.GetQueryableAsync())
			.Where(x => x.StandardId == standardId).Select(x => (int?)x.SortOrder)) ?? -1;
		var division = new Division(
			guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			standardId,
			strength,
			roomNumber
		);
		division.SetSortOrder(checked(nextOrder + 1));
		return division;
	}

	public void Reorder(IReadOnlyList<Division> siblings, IReadOnlyList<Guid> ids)
	{
		AcademicSiblingOrder.Validate(ids, siblings.Select(x => x.Id));
		var byId = siblings.ToDictionary(x => x.Id);
		for (var index = 0; index < ids.Count; index++) byId[ids[index]].SetSortOrder(index);
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
