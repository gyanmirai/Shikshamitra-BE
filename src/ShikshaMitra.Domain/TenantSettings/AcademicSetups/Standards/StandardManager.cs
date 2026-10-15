using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class StandardManager(IStandardRepository standardRepository, IGuidGenerator guidGenerator) : DomainService
{
	public async Task<Standard> CreateAsync(Guid educationMediumStageId, string name, int ageLowerLimit, Guid? shiftId)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		name = name.Trim();
		Check.Positive(ageLowerLimit, nameof(ageLowerLimit));
		await ValidateStandardAsync(name, educationMediumStageId);

		return new Standard(
			guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			ageLowerLimit,
			educationMediumStageId,
			shiftId
		);
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
