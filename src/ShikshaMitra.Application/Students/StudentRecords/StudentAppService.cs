using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentAppService(IStudentRepository studentRepository) : CrudAppService<Student, StudentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDto>(studentRepository),
	IStudentAppService
{
}
