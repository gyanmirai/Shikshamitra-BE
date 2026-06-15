using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using ShikshaMitra.Academic.Curriculum;

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
				throw new BusinessException(CurriculumErrorCodes.SubjectEducationMediumNotFound)
					.WithData("EducationMediumId", educationMediumId);
			}

			// Check for duplicate subject name within the same education medium
			var existingByName = await subjectRepository.FindAsync(s =>
				s.Name == name && s.EducationMediumId == educationMediumId);
			if (existingByName != null)
			{
				throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectName)
					.WithData("Name", name)
					.WithData("message", $"A subject with name '{name}' already exists. Please choose a different name.");
			}

			// Check for duplicate color within the same education medium
			var existingByColor = await subjectRepository.FindAsync(s =>
				s.Color == color && s.EducationMediumId == educationMediumId);
			if (existingByColor != null)
			{
				throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectColor)
					.WithData("Color", color)
					.WithData("ExistingSubjectName", existingByColor.Name)
					.WithData("message", $"The color '{color}' is already used by subject '{existingByColor.Name}'. Please choose a different color.");
			}

			return new Subject(
				guidGenerator.Create(),
				CurrentTenant.Id,
				name,
				color,
				educationMediumId
			);
		}
		catch (BusinessException)
		{
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
				?? throw new BusinessException(CurriculumErrorCodes.SubjectNotFound)
					.WithData("SubjectId", id);

			// Check for duplicate name (excluding current subject)
			var duplicateName = await subjectRepository.FindAsync(s =>
				s.Name == name && s.EducationMediumId == existingSubject.EducationMediumId && s.Id != id);
			if (duplicateName != null)
			{
				throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectName)
					.WithData("Name", name)
					.WithData("message", $"A subject with name '{name}' already exists. Please choose a different name.");
			}

			// Check for duplicate color (excluding current subject)
			var duplicateColor = await subjectRepository.FindAsync(s =>
				s.Color == color && s.EducationMediumId == existingSubject.EducationMediumId && s.Id != id);
			if (duplicateColor != null)
			{
				throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectColor)
					.WithData("Color", color)
					.WithData("ExistingSubjectName", duplicateColor.Name)
					.WithData("message", $"The color '{color}' is already used by subject '{duplicateColor.Name}'. Please choose a different color.");
			}

			existingSubject.SetName(name);
			existingSubject.SetColor(color);

			return existingSubject;
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error updating subject with ID {SubjectId}", id);
			throw new UserFriendlyException("An error occurred while updating the subject.");
		}
	}
}
