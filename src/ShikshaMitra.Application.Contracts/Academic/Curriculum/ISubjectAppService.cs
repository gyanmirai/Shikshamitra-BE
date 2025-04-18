
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

public interface ISubjectAppService
{
	Task CreateAsync(CreateSubjectDto input);
}
