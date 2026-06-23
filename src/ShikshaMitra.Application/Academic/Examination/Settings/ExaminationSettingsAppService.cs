using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

using ShikshaMitra.Academic.Examination.Exams;

namespace ShikshaMitra.Academic.Examination.Settings;

[Authorize]
[RemoteService(false)]
public class ExaminationSettingsAppService(
	IRepository<GradingScale, Guid> gradingScaleRepository,
	IRepository<ExamType, Guid> examTypeRepository,
	ExamTypeManager examTypeManager,
	IGuidGenerator guidGenerator,
	IRepository<Exam, Guid> examRepository,
	IRepository<ExamSubject, Guid> examSubjectRepository
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

		// Block deletion if the grading scale is in use
		var usageParts = new System.Collections.Generic.List<string>();

		var examCount = await examRepository.CountAsync(e => e.GradingScaleId == id);
		if (examCount > 0)
			usageParts.Add($"{examCount} exam(s)");

		var examSubjectCount = await examSubjectRepository.CountAsync(es => es.GradeScalingId == id);
		if (examSubjectCount > 0)
			usageParts.Add($"{examSubjectCount} exam subject(s)");

		if (usageParts.Count > 0)
		{
			throw new BusinessException(ExamErrorCodes.GradingScaleIsInUse)
				.WithData("message", $"Cannot delete grading scale '{gradingScale.Name}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
		}

		await gradingScaleRepository.DeleteAsync(gradingScale);
	}

	public async Task CreateExamTypeAsync(Guid academicMediumId, CreateExamTypeDto input)
	{
		var examType = await examTypeManager.CreateAsync(input.ExamType, input.Description, academicMediumId);
	}

	public async Task<ExamTypeListResponseDto> GetExamTypesAsync(GetExamTypesInput input)
	{
		var queryable = await examTypeRepository.GetQueryableAsync();

		var query = queryable
			.Where(x => x.AcademicMediumId == input.AcademicMediumId);

		var totalCount = await AsyncExecuter.CountAsync(query);

		var sortedQuery = !string.IsNullOrWhiteSpace(input.Sorting)
			? query.OrderBy(input.Sorting)
			: query.OrderByDescending(x => x.CreationTime);

		var examTypes = await AsyncExecuter.ToListAsync(
			sortedQuery.PageBy(input)
		);

		return new ExamTypeListResponseDto
		{
			Items = ObjectMapper.Map<List<ExamType>, List<ExamTypeDto>>(examTypes),
			PageNumber = input.SkipCount / input.MaxResultCount + 1,
			PageSize = input.MaxResultCount,
			TotalPages = (int) Math.Ceiling(totalCount / (double) input.MaxResultCount),
			TotalCount = totalCount
		};
	}

	public async Task<ExamTypeSingleResponseDto> GetExamTypeByIdAsync(Guid id, Guid academicMediumId)
	{
		var examType = await examTypeRepository.FirstOrDefaultAsync(x => x.Id == id && x.AcademicMediumId == academicMediumId);

		if (examType == null)
		{
			throw new UserFriendlyException("Exam type not found");
		}

		return new ExamTypeSingleResponseDto
		{
			Item = ObjectMapper.Map<ExamType, ExamTypeDto>(examType)
		};
	}

	public async Task UpdateExamTypeAsync(Guid id, Guid academicMediumId, UpdateExamTypeDto input)
	{
		var examType = await examTypeRepository.FirstOrDefaultAsync(x => x.Id == id && x.AcademicMediumId == academicMediumId);

		if (examType == null)
		{
			throw new UserFriendlyException("Exam type not found");
		}

		await examTypeManager.UpdateAsync(examType, input.ExamType, input.Description);
	}

	public async Task DeleteExamTypeAsync(Guid id, Guid academicMediumId)
	{
		var examType = await examTypeRepository.FirstOrDefaultAsync(x => x.Id == id && x.AcademicMediumId == academicMediumId);

		if (examType == null)
		{
			throw new UserFriendlyException("Exam type not found");
		}

		// Block deletion if the exam type is in use
		var usageParts = new System.Collections.Generic.List<string>();

		var examCount = await examRepository.CountAsync(e => e.ExamTypeId == id);
		if (examCount > 0)
			usageParts.Add($"{examCount} exam(s)");

		if (usageParts.Count > 0)
		{
			throw new BusinessException(ExamErrorCodes.ExamTypeIsInUse)
				.WithData("message", $"Cannot delete exam type '{examType.Name}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
		}

		await examTypeRepository.DeleteAsync(examType);
	}
}
