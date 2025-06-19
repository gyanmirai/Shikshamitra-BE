using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Syllabus;
public class LearningResourceAppService(
	IRepository<LearningResource, Guid> learningResourceRepository,
	IGuidGenerator guidGenerator) : ApplicationService
{
	public async Task CreateAsync(CreateUpdateLearningResourceDto createResourceDto)
	{
		var learningResource = new LearningResource(
			guidGenerator.Create(),
			CurrentTenant.Id,
			createResourceDto.StandardId,
			createResourceDto.SubjectId,
			createResourceDto.ResourceTitle,
			createResourceDto.ChapterId);

		// upload document in storage using abp


		await learningResourceRepository.InsertAsync(learningResource);
	}
}
