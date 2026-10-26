using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
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

public class StandardManager(IStandardRepository standardRepository, IGuidGenerator guidGenerator, AcademicFieldFormattingStore formatting) : DomainService
{
	public async Task<Standard> CreateAsync(Guid educationMediumStageId, string name, int ageLowerLimit, Guid? shiftId)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		name = AcademicNameFormatter.Format(name, (await formatting.GetAsync()).Standard);
		name = name.Trim();
		Check.Positive(ageLowerLimit, nameof(ageLowerLimit));
		await ValidateStandardAsync(name, educationMediumStageId);

		var nextOrder = await AsyncExecuter.MaxAsync((await standardRepository.GetQueryableAsync())
			.Where(x => x.EducationMediumStageId == educationMediumStageId).Select(x => (int?)x.SortOrder)) ?? -1;
		var standard = new Standard(
			guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			ageLowerLimit,
			educationMediumStageId,
			shiftId
		);
		standard.SetSortOrder(checked(nextOrder + 1));
		return standard;
	}

	public void Reorder(IReadOnlyList<Standard> siblings, IReadOnlyList<Guid> ids)
	{
		AcademicSiblingOrder.Validate(ids, siblings.Select(x => x.Id));
		var byId = siblings.ToDictionary(x => x.Id);
		for (var index = 0; index < ids.Count; index++) byId[ids[index]].SetSortOrder(index);
	}

	public async Task<Standard> UpdateAsync(Standard standard, string newName, int newAgeLowerLimit, Guid? shiftId)
	{
		Check.NotNull(standard, nameof(standard));
		Check.NotNullOrWhiteSpace(newName, nameof(newName));
		newName = newName.Trim();
		Check.Positive(newAgeLowerLimit, nameof(newAgeLowerLimit));
		if (standard.Name == newName && standard.AgeLowerLimit == newAgeLowerLimit && standard.ShiftId == shiftId)
		{
			return standard;
		}

		if (standard.Name != newName)
		{
			await ValidateStandardAsync(newName, standard.EducationMediumStageId, standard.Id);
		}
		standard.SetName(newName);
		standard.AgeLowerLimit = newAgeLowerLimit;
		standard.ShiftId = shiftId;
		return standard;
	}

	private async Task ValidateStandardAsync(string name, Guid educationMediumStageId, Guid? excludedId = null)
	{
		if (await standardRepository.FindByNameAndEducationMediumStageIdAsync(name, educationMediumStageId, excludedId) != null)
		{
			throw new BusinessException(TenantSettingsDomainErrorCodes.DuplicateStandardName)
				.WithData("name", name);
		}
	}
}
