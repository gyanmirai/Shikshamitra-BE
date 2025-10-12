using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Examination.Setting;

[RemoteService(IsEnabled = false)]
public class GradeScaleAppService(
	IRepository<GradeScale, Guid> gradeScaleRepository,
	IRepository<GradeRange, Guid> gradeRangeRepository,
	ILogger<SubjectAppService> logger
	) : ApplicationService, IGradeScaleAppService
{
    public async Task<GradeScaleDto> CreateGradeScaleAsync(CreateGradeScaleDto createGradeScaleDto)
    {
        try
        {
            // Check for duplicate GradeScale title for the same EducationMedium
            var exists = await gradeScaleRepository.AnyAsync(
                x => x.Title.ToLower() == createGradeScaleDto.Title.Trim().ToLower() &&
                x.EducationMediumId == createGradeScaleDto.EducationMediumId
            );

            if (exists)
            {
                throw new UserFriendlyException(
                    $"Grade scale '{createGradeScaleDto.Title}' already exists for this Education Medium. Please choose a different title."
                );
            }

            // Create the GradeScale entity
            var gradeScale = new GradeScale(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                createGradeScaleDto.Title.Trim(),
                createGradeScaleDto.TotalMarks,
                createGradeScaleDto.EducationMediumId
            );

            //Add GradeRanges (from client)
            if (createGradeScaleDto.GradeRanges != null && createGradeScaleDto.GradeRanges.Count > 0)
            {
                foreach (var rangeDto in createGradeScaleDto.GradeRanges)
                {
                    // Validate individual ranges
                    if (rangeDto.MinMarks < 0 || rangeDto.MaxMarks < 0)
                    {
                        throw new UserFriendlyException("Marks cannot be negative.");
                    }

                    if (rangeDto.MinMarks >= rangeDto.MaxMarks)
                    {
                        throw new UserFriendlyException($"Invalid grade range: {rangeDto.MinMarks} - {rangeDto.MaxMarks}");
                    }

                    // Create GradeRange entity
                    var range = new GradeRange(
                        GuidGenerator.Create(),
                        gradeScale.Id, // Set parent GradeScaleId
                        rangeDto.MinMarks,
                        rangeDto.MaxMarks,
                        rangeDto.Grade
                    );

                    // Add to GradeScale
                    gradeScale.AddGradeRange(range);
                }
            }

            //Insert into repository
            await gradeScaleRepository.InsertAsync(gradeScale, autoSave: true);

            //Log creation
            Logger.LogInformation("GradeScale created successfully: {Title}", createGradeScaleDto.Title);

            //map to DTO and return
            return ObjectMapper.Map<GradeScale, GradeScaleDto>(gradeScale);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while creating GradeScale: {Message}", ex.Message);
            throw new UserFriendlyException("An unexpected error occurred while creating the Grade Scale.");
        }
    }

	public async Task<bool> DeleteGradeScaleAsync(Guid id)
	{
		try
		{
			//Validate Id
			if (id == Guid.Empty)
			{
				throw new UserFriendlyException("Invalid GradeScale Id.");
			}

            //Retrieve the GradeScale with nested GradeRanges
            var gradeScale = await gradeScaleRepository.GetAsync(
                x => x.Id == id,
                includeDetails: true
            );

			if (gradeScale == null)
			{
				throw new UserFriendlyException($"Grade scale with Id '{id}' does not exist.");
			}

			// Delete the entity
			await gradeScaleRepository.DeleteAsync(gradeScale, autoSave: true);

			//Log success
			Logger.LogInformation("GradeScale deleted successfully: {Title}", gradeScale.Title);
			return true;

		}
		catch (UserFriendlyException)
		{
			// Re-throw friendly exceptions as-is (they will be shown nicely to client)
			throw;
		}
		catch (Exception ex)
		{
			// Log unexpected exceptions
			Logger.LogError(ex, "Error occurred while deleting GradeScale with Id: {Id}", id);

			// Wrap in a friendly message for the client
			throw new UserFriendlyException("An unexpected error occurred while deleting the grade scale. Please try again later.");
		}
	}

	public async Task<GradeScaleDto> GetGradeScaleByIdAsync(Guid id)
	{
		try
		{
			if (id == Guid.Empty)
				throw new UserFriendlyException("Invalid GradeScale Id.");

			var queryable = await gradeScaleRepository.GetQueryableAsync();

			// Include GradeRanges explicitly
			var gradeScale = await queryable
				.Where(x => x.Id == id)
				.Include(x => x.GradeRanges) 
				.FirstOrDefaultAsync();

			if (gradeScale == null)
				throw new UserFriendlyException($"Grade scale with Id '{id}' does not exist.");

			return ObjectMapper.Map<GradeScale, GradeScaleDto>(gradeScale);
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "An error occurred while retrieving GradeScale with Id: {Id}", id);
			throw new UserFriendlyException("An unexpected error occurred while retrieving the Grade Scale.");
		}
	}

	public async Task<List<GradeScaleDto>> GetGradeScaleListAsync(Guid educationMediumId)
	{
		try
		{
			if (educationMediumId == Guid.Empty)
				throw new UserFriendlyException("Invalid EducationMedium Id.");

			// Get queryable from repository
			var queryable = await gradeScaleRepository.GetQueryableAsync();

			// Include GradeRanges explicitly
			var gradeScalesWithRanges = await queryable
				.Where(x => x.EducationMediumId == educationMediumId)
				.Include(x => x.GradeRanges) 
				.ToListAsync();

			// Map to DTOs
			return gradeScalesWithRanges
				.Select(gs => ObjectMapper.Map<GradeScale, GradeScaleDto>(gs))
				.ToList();
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "An error occurred while retrieving GradeScales for EducationMediumId: {EducationMediumId}", educationMediumId);
			throw new UserFriendlyException("An unexpected error occurred while retrieving the Grade Scales.");
		}
	}
	public async Task<GradeScaleDto> UpdateGradeScaleAsync(Guid id, UpdateGradeScaleDto updateDto)
	{
		if (id == Guid.Empty)
			throw new UserFriendlyException("Invalid GradeScale Id.");

		var gradeScale = await gradeScaleRepository.GetAsync(
			x => x.Id == id,
			includeDetails: true
		);

		if (gradeScale is null)
			throw new UserFriendlyException($"Grade scale with Id '{id}' does not exist.");

		
	    gradeScale.SetTitle(updateDto.Title.Trim());
		gradeScale.SetTotalMarks(updateDto.TotalMarks);

		var idsToDelete = (await gradeRangeRepository.GetListAsync(x => x.GradeScaleId == id))
	             .Select(x => x.Id)
	             .ToList();

		await gradeRangeRepository.DeleteManyAsync(idsToDelete);

		foreach (var rangeDto in updateDto.GradeRanges)
		{

			// Validate individual ranges
			if (rangeDto.MinMarks < 0 || rangeDto.MaxMarks < 0)
			{
				throw new UserFriendlyException("Marks cannot be negative.");
			}

			if (rangeDto.MinMarks >= rangeDto.MaxMarks)
			{
				throw new UserFriendlyException($"Invalid grade range: {rangeDto.MinMarks} - {rangeDto.MaxMarks}");
			}

			// Create GradeRange entity
			var range = new GradeRange(
				GuidGenerator.Create(),
				id,
				rangeDto.MinMarks,
				rangeDto.MaxMarks,
				rangeDto.Grade
			);

			await gradeRangeRepository.InsertAsync(range, autoSave: true);
		}

		await gradeScaleRepository.UpdateAsync(gradeScale, autoSave: true);

		return ObjectMapper.Map<GradeScale, GradeScaleDto>(gradeScale);
	}





}

