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
	private async Task HandleDefaultGradingScaleAsync(Guid academicMediumId, Guid currentScaleId, bool isDefault)
	{
		if (!isDefault)
		{
			return;
		}

		var queryable = await gradingScaleRepository.GetQueryableAsync();
		var otherScales = queryable
			.Where(gs => gs.AcademicMediumId == academicMediumId && gs.Id != currentScaleId && gs.IsDefault)
			.ToList();

		foreach (var scale in otherScales)
		{
			scale.SetIsDefault(false);
			await gradingScaleRepository.UpdateAsync(scale);
		}
	}

	private async Task<int> GetGradingScaleUsageCountAsync(Guid id)
	{
		var examCount = await examRepository.CountAsync(e => e.GradingScaleId == id);
		var examSubjectCount = await examSubjectRepository.CountAsync(es => es.GradeScalingId == id);
		return examCount + examSubjectCount;
	}

	private async Task<int> GetExamTypeUsageCountAsync(Guid id)
	{
		return await examRepository.CountAsync(e => e.ExamTypeId == id);
	}

	public async Task CreateGradingScaleAsync(Guid academicMediumId, CreateGradingScaleDto input)
	{
		var gradingScale = new GradingScale(
			guidGenerator.Create(),
			CurrentTenant.Id,
			input.Name,
			input.TotalMarks,
			academicMediumId,
			input.PassMarks,
			input.Description,
			input.IsDefault,
			SettingStatus.Active
		);

		foreach (var range in input.GradingScale)
		{
			gradingScale.AddGradeRange(
				guidGenerator.Create(),
				range.MinMarks,
				range.MaxMarks,
				range.Grade,
				range.GradePoint,
				range.Remark,
				range.ResultStatus
			);
		}

		await gradingScaleRepository.InsertAsync(gradingScale);
		await HandleDefaultGradingScaleAsync(academicMediumId, gradingScale.Id, input.IsDefault);
	}

	public async Task<GradingScaleListResponseDto> GetAllGradingScalesAsync(Guid academicMediumId, bool includeArchived = false)
	{
		var queryable = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);

		var gradingScales = queryable
			.Where(gs => gs.AcademicMediumId == academicMediumId)
			.WhereIf(!includeArchived, gs => gs.Status != SettingStatus.Archived)
			.OrderByDescending(gs => gs.CreationTime)
			.ToList();

		var gradingScaleDtos = new List<GradingScaleDto>();
		foreach (var gs in gradingScales)
		{
			gradingScaleDtos.Add(await MapGradingScaleAsync(gs));
		}

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

		var gradingScaleDto = await MapGradingScaleAsync(gradingScale);

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
		gradingScale.SetPassMarks(input.PassMarks);
		gradingScale.SetDescription(input.Description);
		gradingScale.SetIsDefault(input.IsDefault);
		gradingScale.SetStatus(input.Status);
		if (input.Status == SettingStatus.Archived)
		{
			gradingScale.SetIsDefault(false);
		}

		var gradeRanges = input.GradingScale
			.Select(range => (guidGenerator.Create(), range.MinMarks, range.MaxMarks, range.Grade, range.GradePoint, range.Remark, range.ResultStatus))
			.ToList();

		gradingScale.UpdateGradeRanges(gradeRanges);

		await gradingScaleRepository.UpdateAsync(gradingScale);
		await HandleDefaultGradingScaleAsync(academicMediumId, gradingScale.Id, input.IsDefault);
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
				.WithData("message", $"Cannot delete grading scale '{gradingScale.Name}' because it is in use: {string.Join(", ", usageParts)}. Please archive it to stop future usage.");
		}

		await gradingScaleRepository.DeleteAsync(gradingScale);
	}

	public async Task<GradingScaleSingleResponseDto> ArchiveGradingScaleAsync(Guid id, Guid academicMediumId)
	{
		var gradingScale = await GetGradingScaleEntityAsync(id, academicMediumId);

		gradingScale.SetStatus(SettingStatus.Archived);
		gradingScale.SetIsDefault(false);
		await gradingScaleRepository.UpdateAsync(gradingScale);

		return new GradingScaleSingleResponseDto(await MapGradingScaleAsync(gradingScale));
	}

	public async Task<GradingScaleSingleResponseDto> RestoreGradingScaleAsync(Guid id, Guid academicMediumId)
	{
		var gradingScale = await GetGradingScaleEntityAsync(id, academicMediumId);

		gradingScale.SetStatus(SettingStatus.Active);
		await gradingScaleRepository.UpdateAsync(gradingScale);

		return new GradingScaleSingleResponseDto(await MapGradingScaleAsync(gradingScale));
	}

	public async Task CreateExamTypeAsync(Guid academicMediumId, CreateExamTypeDto input)
	{
		var examType = await examTypeManager.CreateAsync(
			input.ExamType,
			input.Description,
			academicMediumId,
			input.Category,
			SettingStatus.Active,
			input.DefaultTotalMarks,
			input.DefaultWeightage,
			input.IncludedInResult,
			input.MarksEntryRequired,
			input.GradeScaleRequired,
			input.AllowPracticalOral,
			input.DefaultGradeScaleId
		);
	}

	public async Task<ExamTypeListResponseDto> GetExamTypesAsync(GetExamTypesInput input)
	{
		var queryable = await examTypeRepository.GetQueryableAsync();

		var query = queryable
			.Where(x => x.AcademicMediumId == input.AcademicMediumId)
			.WhereIf(!input.IncludeArchived, x => x.Status != SettingStatus.Archived);

		var totalCount = await AsyncExecuter.CountAsync(query);

		var sortedQuery = !string.IsNullOrWhiteSpace(input.Sorting)
			? query.OrderBy(input.Sorting)
			: query.OrderByDescending(x => x.CreationTime);

		var examTypes = await AsyncExecuter.ToListAsync(
			sortedQuery.PageBy(input)
		);

		var dtos = ObjectMapper.Map<List<ExamType>, List<ExamTypeDto>>(examTypes);
		foreach (var dto in dtos)
		{
			dto.UsageCount = await GetExamTypeUsageCountAsync(dto.Id);
		}

		return new ExamTypeListResponseDto
		{
			Items = dtos,
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

		var dto = ObjectMapper.Map<ExamType, ExamTypeDto>(examType);
		dto.UsageCount = await GetExamTypeUsageCountAsync(dto.Id);

		return new ExamTypeSingleResponseDto
		{
			Item = dto
		};
	}

	public async Task UpdateExamTypeAsync(Guid id, Guid academicMediumId, UpdateExamTypeDto input)
	{
		var examType = await examTypeRepository.FirstOrDefaultAsync(x => x.Id == id && x.AcademicMediumId == academicMediumId);

		if (examType == null)
		{
			throw new UserFriendlyException("Exam type not found");
		}

		await examTypeManager.UpdateAsync(
			examType,
			input.ExamType,
			input.Description,
			input.Category,
			input.Status,
			input.DefaultTotalMarks,
			input.DefaultWeightage,
			input.IncludedInResult,
			input.MarksEntryRequired,
			input.GradeScaleRequired,
			input.AllowPracticalOral,
			input.DefaultGradeScaleId
		);
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
				.WithData("message", $"Cannot delete exam type '{examType.Name}' because it is in use: {string.Join(", ", usageParts)}. Please archive it to stop future usage.");
		}

		await examTypeRepository.DeleteAsync(examType);
	}

	public async Task<ExamTypeSingleResponseDto> ArchiveExamTypeAsync(Guid id, Guid academicMediumId)
	{
		var examType = await GetExamTypeEntityAsync(id, academicMediumId);

		examType.SetStatus(SettingStatus.Archived);
		await examTypeRepository.UpdateAsync(examType);

		return await MapExamTypeSingleResponseAsync(examType);
	}

	public async Task<ExamTypeSingleResponseDto> RestoreExamTypeAsync(Guid id, Guid academicMediumId)
	{
		var examType = await GetExamTypeEntityAsync(id, academicMediumId);

		examType.SetStatus(SettingStatus.Active);
		await examTypeRepository.UpdateAsync(examType);

		return await MapExamTypeSingleResponseAsync(examType);
	}

	private async Task<GradingScale> GetGradingScaleEntityAsync(Guid id, Guid academicMediumId)
	{
		var queryable = await gradingScaleRepository.WithDetailsAsync(gs => gs.GradeRanges);
		var gradingScale = queryable.FirstOrDefault(gs =>
			gs.Id == id && gs.AcademicMediumId == academicMediumId);

		if (gradingScale == null)
		{
			throw new UserFriendlyException("Grading scale not found");
		}

		return gradingScale;
	}

	private async Task<ExamType> GetExamTypeEntityAsync(Guid id, Guid academicMediumId)
	{
		var examType = await examTypeRepository.FirstOrDefaultAsync(x =>
			x.Id == id && x.AcademicMediumId == academicMediumId);

		if (examType == null)
		{
			throw new UserFriendlyException("Exam type not found");
		}

		return examType;
	}

	private async Task<GradingScaleDto> MapGradingScaleAsync(GradingScale gradingScale)
	{
		return new GradingScaleDto
		{
			Id = gradingScale.Id,
			Name = gradingScale.Name,
			TotalMarks = gradingScale.TotalMarks,
			PassMarks = gradingScale.PassMarks,
			Description = gradingScale.Description,
			IsDefault = gradingScale.IsDefault,
			Status = gradingScale.Status,
			UsageCount = await GetGradingScaleUsageCountAsync(gradingScale.Id),
			CreatedAt = gradingScale.CreationTime,
			GradingScale = gradingScale.GradeRanges
				.OrderBy(gr => gr.MinMarks)
				.Select(gr => new GradeRangeDto
				{
					MinMarks = gr.MinMarks,
					MaxMarks = gr.MaxMarks,
					Grade = gr.Grade,
					GradePoint = gr.GradePoint,
					Remark = gr.Remark,
					ResultStatus = gr.ResultStatus
				})
				.ToList()
		};
	}

	private async Task<ExamTypeSingleResponseDto> MapExamTypeSingleResponseAsync(ExamType examType)
	{
		var dto = ObjectMapper.Map<ExamType, ExamTypeDto>(examType);
		dto.UsageCount = await GetExamTypeUsageCountAsync(dto.Id);

		return new ExamTypeSingleResponseDto
		{
			Item = dto
		};
	}
}
