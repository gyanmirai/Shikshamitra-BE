using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Examination.Settings;

[Authorize]
[RemoteService(false)]
public class ExaminationSettingsAppService(
	IRepository<GradingScale, Guid> gradingScaleRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IExaminationSettingsAppService
{
    public async Task CreateGradingScaleAsync(Guid academicMediumId, CreateGradingScaleDto input)
    {
        var gradingScale = new GradingScale(
			guidGenerator.Create(),
            CurrentTenant.Id,
            input.Name,
            input.TotalMarks,
            academicMediumId
        );

        foreach (var range in input.GradingScale)
        {
            gradingScale.AddGradeRange(
				guidGenerator.Create(),
                range.MinMarks,
                range.MaxMarks,
                range.Grade
            );
        }

        await gradingScaleRepository.InsertAsync(gradingScale);
    }

    public async Task<GradingScaleListResponseDto> GetAllGradingScalesAsync(Guid academicMediumId)
    {
        var queryable = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
        
        var gradingScales = queryable
            .Where(gs => gs.AcademicMediumId == academicMediumId)
            .OrderByDescending(gs => gs.CreationTime)
            .ToList();

        var gradingScaleDtos = gradingScales.Select(gs => new GradingScaleDto
        {
            Id = gs.Id,
            Name = gs.Name,
            TotalMarks = gs.TotalMarks,
            CreatedAt = gs.CreationTime,
            GradingScale = gs.GradeRanges
                .OrderBy(gr => gr.MinMarks)
                .Select(gr => new GradeRangeDto
                {
                    MinMarks = gr.MinMarks,
                    MaxMarks = gr.MaxMarks,
                    Grade = gr.Grade
                })
                .ToList()
        }).ToList();

        return new GradingScaleListResponseDto(gradingScaleDtos);
    }

    public async Task<GradingScaleSingleResponseDto> GetGradingScaleByIdAsync(Guid id, Guid academicMediumId)
    {
        var queryable = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
        
        var gradingScale = queryable
            .FirstOrDefault(gs => gs.Id == id && gs.AcademicMediumId == academicMediumId);

        if (gradingScale == null)
        {
            throw new UserFriendlyException("Grading scale not found");
        }

        var gradingScaleDto = new GradingScaleDto
        {
            Id = gradingScale.Id,
            Name = gradingScale.Name,
            TotalMarks = gradingScale.TotalMarks,
            CreatedAt = gradingScale.CreationTime,
            GradingScale = gradingScale.GradeRanges
                .OrderBy(gr => gr.MinMarks)
                .Select(gr => new GradeRangeDto
                {
                    MinMarks = gr.MinMarks,
                    MaxMarks = gr.MaxMarks,
                    Grade = gr.Grade
                })
                .ToList()
        };

        return new GradingScaleSingleResponseDto(gradingScaleDto);
    }

    public async Task UpdateGradingScaleAsync(Guid id, Guid academicMediumId, UpdateGradingScaleDto input)
    {
        var queryable = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
        
        var gradingScale = queryable
            .FirstOrDefault(gs => gs.Id == id && gs.AcademicMediumId == academicMediumId);

        if (gradingScale == null)
        {
            throw new UserFriendlyException("Grading scale not found");
        }

        gradingScale.SetName(input.Name);
        gradingScale.SetTotalMarks(input.TotalMarks);

        var gradeRanges = input.GradingScale
            .Select(range => (guidGenerator.Create(), range.MinMarks, range.MaxMarks, range.Grade))
            .ToList();

        gradingScale.UpdateGradeRanges(gradeRanges);

        await gradingScaleRepository.UpdateAsync(gradingScale);
    }

    public async Task DeleteGradingScaleAsync(Guid id, Guid academicMediumId)
    {
        var gradingScale = await gradingScaleRepository
            .FirstOrDefaultAsync(gs => gs.Id == id && gs.AcademicMediumId == academicMediumId);

        if (gradingScale == null)
        {
            throw new UserFriendlyException("Grading scale not found");
        }

        await gradingScaleRepository.DeleteAsync(gradingScale);
    }
}
