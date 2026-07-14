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
	public async Task<Subject> CreateSubject(
		string name,
		string color,
		Guid educationMediumId,
		string? displayName = null,
		string? category = null,
		string? icon = null,
		string? description = null,
		SubjectMasterStatus status = SubjectMasterStatus.Active,
		int? sortOrder = null
	)
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
			var normalizedName = name.Trim().ToUpperInvariant();
			var existingByName = await subjectRepository.FindAsync(s =>
				s.Name.ToUpper() == normalizedName && s.EducationMediumId == educationMediumId && s.Status != SubjectMasterStatus.Archived);
			if (existingByName != null)
			{
				throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectName)
					.WithData("Name", name)
					.WithData("message", $"A subject with name '{name}' already exists. Please choose a different name.");
			}



			return new Subject(
				guidGenerator.Create(),
				CurrentTenant.Id,
				name,
				color,
				educationMediumId,
				displayName,
				category,
				icon,
				description,
				status,
				sortOrder
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
		Guid? educationMediumId,
		string? displayName = null,
		string? category = null,
		string? icon = null,
		string? description = null,
		SubjectMasterStatus status = SubjectMasterStatus.Active,
		int? sortOrder = null
	)
	{
		try
		{
			var existingSubject =
				await subjectRepository.FindAsync(s => s.Id == id)
				?? throw new BusinessException(CurriculumErrorCodes.SubjectNotFound)
					.WithData("SubjectId", id);

			// Check for duplicate name (excluding current subject)
			var normalizedName = name.Trim().ToUpperInvariant();
			var duplicateName = await subjectRepository.FindAsync(s =>
				s.Name.ToUpper() == normalizedName
				&& s.EducationMediumId == existingSubject.EducationMediumId
				&& s.Id != id
				&& s.Status != SubjectMasterStatus.Archived);
			if (duplicateName != null)
			{
				throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectName)
					.WithData("Name", name)
					.WithData("message", $"A subject with name '{name}' already exists. Please choose a different name.");
			}



			existingSubject.SetName(name);
			existingSubject.SetColor(color);
			existingSubject.SetMasterDetails(displayName, category, icon, description, status, sortOrder);

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
