using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum.Settings;

public class SubjectManager(
	IGuidGenerator guidGenerator,
	IRepository<Subject> subjectRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	ILogger<Subject> logger
) : DomainService
{
	public async Task<Subject> CreateSubject(string name, string color, Guid educationMediumId)
	{
		try
		{
			// Validate that the EducationMedium exists
			if (!await educationMediumRepository.AnyAsync(em => em.Id == educationMediumId))
			{
				throw new BusinessException(
					"ShikshaMitra:Subject:EducationMediumNotFound",
					$"Education Medium with ID {educationMediumId} does not exist."
				);
			}

			// Check for duplicate subject name within the same education medium
			var existingSubject = await subjectRepository.FindAsync(s => 
				s.Name == name && s.EducationMediumId == educationMediumId);
			if (existingSubject != null)
			{
				throw new BusinessException(
					"ShikshaMitra:Subject:DuplicateName",
					"Subject with the same name already exists for this education medium."
				);
			}

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
			throw new UserFriendlyException("An error occurred while creating the subject.");
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
			var existingSubject =
				await subjectRepository.FindAsync(s => s.Id == id)
				?? throw new UserFriendlyException("Subject not found");

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
