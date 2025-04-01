using ShikshaMitra.Students.StudentRecords.StudentGroups;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupAppService(IRepository<StudentGroup,Guid> studentGroupRepository) :
	CrudAppService<StudentGroup, StudentGroupDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentGroupDto, CreateUpdateStudentGroupDto>(studentGroupRepository),
	IStudentGroupAppService
{
	//public override async Task<StudentGroupDto> CreateAsync(CreateUpdateStudentGroupDto input)
	//{
	//	await CheckCreatePolicyAsync();

	//	var entity = await MapToEntityAsync(input);

	//	TryToSetTenantId(entity);

	//	await Repository.InsertAsync(entity, autoSave: true);

	//	return await MapToGetOutputDtoAsync(entity);
	//}
}