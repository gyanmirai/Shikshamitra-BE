using ShikshaMitra.Students.StudentRecords.StudentGroups;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupAppService(IRepository<StudentGroup,Guid> studentGroupRepository) :
	CrudAppService<StudentGroup, StudentGroupDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentGroupDto>(studentGroupRepository),
	IStudentGroupAppService
{

}