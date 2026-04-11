using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Managers;

public class AdmissionManager(
	IRepository<Student, Guid> studentRepository,
	IRepository<Division, Guid> divisionRepository) : DomainService
{
	public async Task CanAssignDivisionAsync(Guid studentId, Guid divisionId)
	{
		var student = await studentRepository.GetAsync(studentId) ?? throw new Exception($"Student with ID {studentId} does not exist.");
		var division = await divisionRepository.GetAsync(divisionId) ?? throw new Exception($"Division with ID {divisionId} does not exist.");

		if (student.Status != StudentLifeCycleStatusEnum.Admission)
		{
			throw new Exception($"Cannot assign division. Student with ID {studentId} is not in Admission stage.");
		}
	}
}
