using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.ClassTeacher;

[RemoteService(false)]
public class ClassTeacherAppService(IRepository<ClassTeacher, Guid> assignmentRepo, IRepository<Employee, Guid> employeeRepo, IGuidGenerator guidGenerator) : ApplicationService, IClassTeacherAppService
{
    public async Task AssignOrUpdateTeachersAsync(List<AssignClassTeacherDto> assignments, Guid academicMediumId)
    {
        foreach (var item in assignments)
        {
            try
            {
                Employee employee = await employeeRepo.FindAsync(item.TeacherId) ?? throw new Exception($"Teacher with ID {item.TeacherId} does not exist or is not of type 'Teacher'.");

                var assignment = await assignmentRepo.FindAsync(x =>
                   x.StandardId == item.ClassId &&
                   x.DivisionId == item.DivisionId);

                if (assignment == null)
                {
                    assignment = new ClassTeacher(guidGenerator.Create(), CurrentTenant.Id, item.ClassId, item.DivisionId, item.TeacherId, academicMediumId);
                    await assignmentRepo.InsertAsync(assignment);
                }
                else
                {
                    assignment.TeacherId = item.TeacherId;
                    await assignmentRepo.UpdateAsync(assignment);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    public async Task<List<ClassTeacherDto>> GetAllClassTeachersAsync(Guid educationMediumId)
    {
        try
        {
            var classTeachers = await assignmentRepo.GetListAsync(x => x.AcademicMediumId == educationMediumId);

            var result = new List<ClassTeacherDto>();

            foreach (var classTeacher in classTeachers)
            {
                result.Add(new ClassTeacherDto
                {
                    Id = classTeacher.Id,
                    TeacherId = classTeacher.TeacherId,
                    ClassId = classTeacher.StandardId,
                    DivisionId = classTeacher.DivisionId
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new UserFriendlyException("An error occurred while retrieving class teachers.", ex.Message);
        }
    }
}
