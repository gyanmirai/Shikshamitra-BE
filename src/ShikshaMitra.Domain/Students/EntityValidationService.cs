using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students;
public class EntityValidationService(
	IRepository<EducationMedium, Guid> academicMediumRepository,
	IRepository<Student, Guid> studentRepository) : DomainService
{
	public async Task ValidateAcademicMediumExistsAsync(Guid academicMediumId)
	{
		if (!await academicMediumRepository.AnyAsync(x => x.Id == academicMediumId))
		{
			throw new BusinessException("", "Academic Medium Not Found");
		}
	}

	public async Task ValidateStudentExistsAsync(Guid studentId)
	{
		if (!await studentRepository.AnyAsync(x => x.Id == studentId))
		{
			throw new BusinessException("", "Student Not Found");
		}
	}
}
