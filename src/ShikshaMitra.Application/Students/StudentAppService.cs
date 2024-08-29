using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students;

public class StudentAppService(IStudentRepository studentRepository, StudentManager studentManager) :
	CrudAppService<Student, StudentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDto>(studentRepository)
	, IStudentAppService
{
	// public override async Task<StudentDto> CreateAsync(CreateUpdateStudentDto input)
	// {
	// 	Student student = ObjectMapper.Map<CreateUpdateStudentDto, Student>(input);
	// 	// Use manager to handle domain logic
	// 	
	// 	student = await studentRepository.InsertAsync(student);
	//
	// 	return ObjectMapper.Map<Student, StudentDto>(student);
	// }
}