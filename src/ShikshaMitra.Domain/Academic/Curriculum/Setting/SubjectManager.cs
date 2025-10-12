using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum.Setting;

public class SubjectManager(
	IGuidGenerator guidGenerator,
	IRepository<Subject> subjectRepository,
	ILogger<Subject> logger
) : DomainService
{
    public async Task<Subject> CreateSubject(string name, Guid educationMediumId, string color)
    {
        try
        {
            // Check if a subject with the same name already exists in the given education medium
            var existingSubject = await subjectRepository.FindAsync(
                s => s.Name == name && s.EducationMediumId == educationMediumId
            );

            // Check if the color is already assigned to another subject in the same education medium
            var existingColorSubject = await subjectRepository.FindAsync(
                s => s.Color == color && s.EducationMediumId == educationMediumId
            );

            if (existingSubject != null)
            {
                // Prevent duplicate subject names within the same medium
                throw new BusinessException("A subject with the same name already exists in this medium.");
            }

            if (existingColorSubject != null)
            {
                // Prevent duplicate colors within the same medium
                throw new BusinessException("This color has already been assigned to another subject in this medium.");
            }

            // Create and return a new Subject instance with the provided details
            return new Subject(
                guidGenerator.Create(),
                CurrentTenant.Id,
                name,
                color,
                educationMediumId
            );
        }
        catch (UserFriendlyException ex)
        {
            logger.LogError(ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating subject with name {SubjectName}", name);
            throw new UserFriendlyException("The subject name or color is already assigned to another subject in this medium. Please choose a different name or color.");
        }
    }


	public async Task<Subject> UpdateAsync(
		Guid id,
		string name,
		string color,
		Guid? educationMediumId
	)
	{
		try
		{
            // Fetch the subject by ID, throw if not found
            var existingSubject =
                await subjectRepository.FindAsync(s => s.Id == id)
                ?? throw new UserFriendlyException("Subject not found");

            // Check for duplicate color in the same education medium (excluding current subject)
            var duplicateColorSubject = await subjectRepository.FindAsync(
                s => s.Color == color
                  && s.EducationMediumId == (educationMediumId ?? existingSubject.EducationMediumId)
                  && s.Id != id
            );

            if (duplicateColorSubject != null)
            {
                // Prevent duplicate colors within the same medium
                throw new BusinessException("This color has already been assigned to another subject in this medium.");
            }

            // Update subject name and color
            existingSubject.SetName(name);
            existingSubject.SetColor(color);

            return existingSubject;
		}
		catch (UserFriendlyException ex)
		{
			logger.LogError(ex.Message);
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error updating subject with ID {SubjectId}", id);
			throw new UserFriendlyException("An error occurred while updating the subject.");
		}
	}
}
