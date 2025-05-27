using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectManager(IGuidGenerator guidGenerator, IRepository<Subject> subjectRepository) : DomainService
{
    public async Task<Subject> CreateSubject(
		   string name,
           string color,
		   Guid academicMediumId)
    {
        try
        {
			var existingSubject = await subjectRepository.FindAsync(s => s.Name == name && s.AcademicMediumId == academicMediumId);
			if (existingSubject != null)
			{
				throw new BusinessException("Subject with the same name already exists for this medium.");
			}

			return new Subject(
                guidGenerator.Create(),
                CurrentTenant.Id,
				name,
                color,
				academicMediumId);
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as needed  
            throw new BusinessException("An error occurred while creating the subject.", ex.Message);
        }
    }

    public async Task UpdateAsync(Subject subject, string name, string? color, Guid academicMediumId)
    {
        try
        {
            var existingSubject = await subjectRepository.FindAsync(s =>
                s.Id != subject.Id &&
                s.AcademicMediumId == academicMediumId
			);

            subject.SetName(name);
            subject.SetColor(color);
        }
        catch (BusinessException ex)
        {
            // Log or handle business-specific exceptions
            throw;
        }
        catch (Exception ex)
        {
            // Log or handle unexpected exceptions  
            throw new BusinessException("An error occurred while updating the subject.", ex.Message);
        }
    }
}
