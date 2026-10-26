using ShikshaMitra.Extensions;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.AcademicSetups.Services;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class EducationMediumStageManager(IEducationMediumStageRepository educationMediumStageRepository, AcademicFieldFormattingStore formatting) : DomainService
{
	private string EntityName => this.GetType().ToEntityNameFromManager();

	public async Task<EducationMediumStage> CreateAsync(Guid educationMediumId, string name)
	{
		name = AcademicNameFormatter.Format(name, (await formatting.GetAsync()).Stage);
		await ValidateEducationMediumStageAsync(name, educationMediumId);

		var nextOrder = await AsyncExecuter.MaxAsync((await educationMediumStageRepository.GetQueryableAsync())
			.Where(x => x.EducationMediumId == educationMediumId).Select(x => (int?)x.SortOrder)) ?? -1;
		var stage = new EducationMediumStage(
			GuidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			null,
			educationMediumId
		);
		stage.SetSortOrder(checked(nextOrder + 1));
		return stage;
	}

	public void Reorder(IReadOnlyList<EducationMediumStage> siblings, IReadOnlyList<Guid> ids)
	{
		AcademicSiblingOrder.Validate(ids, siblings.Select(x => x.Id));
		var byId = siblings.ToDictionary(x => x.Id);
		for (var index = 0; index < ids.Count; index++) byId[ids[index]].SetSortOrder(index);
	}

	public async Task<EducationMediumStage> UpdateAsync(EducationMediumStage educationMediumStage, string newName)
	{
		Check.NotNull(educationMediumStage, nameof(educationMediumStage));

		if (educationMediumStage.Name == newName)
		{
			return educationMediumStage;
		}

		await ValidateEducationMediumStageAsync(newName, educationMediumStage.EducationMediumId);

		educationMediumStage.SetName(newName);

		return educationMediumStage;
	}

	private async Task ValidateEducationMediumStageAsync(string name, Guid educationMediumId)
	{
		if (await educationMediumStageRepository.FindByNameAndEducationMediumIdAsync(name, educationMediumId) != null)
		{
			throw new AlreadyExistsException(EntityName, name);
		}
	}
}
