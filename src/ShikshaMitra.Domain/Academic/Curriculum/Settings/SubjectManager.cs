using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using ShikshaMitra.Academic.Curriculum;

namespace ShikshaMitra.Academic.Curriculum.Settings;

public class SubjectManager(
	IGuidGenerator guidGenerator,
	IRepository<Subject> subjectRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IAsyncQueryableExecuter asyncExecuter,
	ILogger<Subject> logger
) : DomainService
{
	public async Task<List<Subject>> CreateSubjectsAsync(IReadOnlyList<SubjectCreation> inputs)
	{
		if (inputs.Count == 0) return [];
		var mediumIds = inputs.Select(input => input.EducationMediumId).Distinct().ToArray();
		var media = await educationMediumRepository.GetQueryableAsync();
		var existingMediumIds = (await asyncExecuter.ToListAsync(media
			.Where(medium => Enumerable.Contains(mediumIds, medium.Id)).Select(medium => medium.Id))).ToHashSet();
		var names = inputs.Select(input => input.Name.Trim().ToUpperInvariant()).Distinct().ToArray();
		var subjects = await subjectRepository.GetQueryableAsync();
		var existing = await asyncExecuter.ToListAsync(subjects.Where(subject =>
			Enumerable.Contains(mediumIds, subject.EducationMediumId) && Enumerable.Contains(names, subject.Name.ToUpper()))
			.Select(subject => new { subject.EducationMediumId, Name = subject.Name.ToUpper() }));
		var existingNames = existing.Select(subject => (subject.EducationMediumId, subject.Name)).ToHashSet();
		var result = new List<Subject>(inputs.Count);
		foreach (var input in inputs)
		{
			if (!existingMediumIds.Contains(input.EducationMediumId))
				throw new BusinessException(CurriculumErrorCodes.SubjectEducationMediumNotFound)
					.WithData("EducationMediumId", input.EducationMediumId);
			if (existingNames.Contains((input.EducationMediumId, input.Name.Trim().ToUpperInvariant())))
				throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectName)
					.WithData("Name", input.Name)
					.WithData("message", $"A subject with name '{input.Name}' already exists. Please choose a different name.");
			result.Add(new Subject(guidGenerator.Create(), CurrentTenant.Id, input.Name, input.Color,
				input.EducationMediumId, input.DisplayName, input.Category, input.Icon, input.Description, input.Status, input.SortOrder));
			existingNames.Add((input.EducationMediumId, input.Name.ToUpperInvariant()));
		}
		return result;
	}

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
				s.Name.ToUpper() == normalizedName && s.EducationMediumId == educationMediumId);
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
				&& s.Id != id);
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
