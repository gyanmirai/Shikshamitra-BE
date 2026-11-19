using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.AcademicSetups.Services;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class DivisionManager(IDivisionRepository divisionRepository, IGuidGenerator guidGenerator,
	AcademicFieldFormattingStore formatting, IRepository<Room, Guid> rooms,
	IAsyncQueryableExecuter asyncExecuter) : DomainService
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
		var roomId = await ResolveRoomAsync(roomNumber);
		var division = new Division(
			guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			standardId,
			strength,
			roomNumber
		);
		division.SetSortOrder(checked(nextOrder + 1));
		division.AssignRoom(roomNumber, roomId);
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

		var roomId = roomNumber == null ? division.RoomId : await ResolveRoomAsync(roomNumber);
		if (division.Name == newName && division.Strength == newStrength
			&& (roomNumber == null || (division.RoomNumber == roomNumber && division.RoomId == roomId)))
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
			division.AssignRoom(roomNumber, roomId);
		}

		return division;
	}

	private async Task<Guid?> ResolveRoomAsync(string roomNumber)
	{
		if (string.IsNullOrWhiteSpace(roomNumber)) return null;
		if (Guid.TryParse(roomNumber, out var id))
		{
			if (!await rooms.AnyAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id))
				throw new BusinessException(TenantSettingsDomainErrorCodes.InvalidDivisionRoom, "Select a room belonging to this school.");
			return id;
		}
		var name = roomNumber.Trim().ToUpperInvariant();
		var candidates = await asyncExecuter.ToListAsync((await rooms.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.RoomName.Trim().ToUpper() == name).Select(x => x.Id).Take(2));
		return candidates.Count == 1 ? candidates[0] : null;
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
