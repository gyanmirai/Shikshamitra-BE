using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Extensions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Domain manager for HomeworkAssignmentEntity
/// Handles business logic and validation
/// </summary>
public class HomeworkManager : DomainService
{
	private readonly IRepository<HomeworkAssignmentEntity, Guid> _repository;
	private readonly IDivisionRepository _divisionRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IHomeworkTypeRepository _homeworkTypeRepository;

	private string EntityName => GetType().ToEntityNameFromManager();

	public HomeworkManager(
		IRepository<HomeworkAssignmentEntity, Guid> repository,
		IDivisionRepository divisionRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IHomeworkTypeRepository homeworkTypeRepository
	)
	{
		_repository = repository;
		_divisionRepository = divisionRepository;
		_academicYearRepository = academicYearRepository;
		_homeworkTypeRepository = homeworkTypeRepository;
	}

	/// <summary>
	/// Validate homework creation/update data
	/// </summary>
	public async Task ValidateHomeworkAsync(
		Guid academicYearId,
		Guid classId,
		List<(Guid DivisionId, DateTime? PublishDate, DateTime DueDate)> divisions
	)
	{
		// 1. Validate Academic Year exists and get dates
		var academicYear = await _academicYearRepository.FindAsync(academicYearId);
		if (academicYear == null)
		{
			throw new BusinessException("ShikshaMitra.Homework:InvalidAcademicYear").WithData(
				"AcademicYearId",
				academicYearId
			);
		}

		// 2. Validate divisions
		if (divisions == null || divisions.Count == 0)
		{
			throw new BusinessException("ShikshaMitra.Homework:AtLeastOneDivision");
		}

		var divisionIds = divisions.Select(d => d.DivisionId).ToList();
		var divisionEntities = await _divisionRepository.GetListAsync(d =>
			divisionIds.Contains(d.Id)
		);

		// Check all divisions exist
		if (divisionEntities.Count != divisionIds.Count)
		{
			var missingIds = divisionIds.Except(divisionEntities.Select(d => d.Id)).ToList();
			throw new BusinessException("ShikshaMitra.Homework:InvalidDivision").WithData(
				"DivisionId",
				string.Join(", ", missingIds)
			);
		}

		// 3. Validate all divisions belong to same Standard
		var standardIds = divisionEntities.Select(d => d.StandardId).Distinct().ToList();
		if (standardIds.Count > 1)
		{
			throw new BusinessException("ShikshaMitra.Homework:DivisionsDifferentStandards");
		}

		if (standardIds.FirstOrDefault() != classId)
		{
			throw new BusinessException("ShikshaMitra.Homework:DivisionsNotMatchClass");
		}

		// 4. Validate dates for each division
		foreach (var division in divisions)
		{
			// Publish Date validation
			if (division.PublishDate.HasValue)
			{
				if (division.PublishDate < academicYear.StartDate)
				{
					throw new BusinessException(
						"ShikshaMitra.Homework:PublishDateBeforeAcademicYear"
					)
						.WithData("PublishDate", division.PublishDate)
						.WithData("AcademicYearStart", academicYear.StartDate);
				}

				if (division.PublishDate > academicYear.EndDate)
				{
					throw new BusinessException(
						"ShikshaMitra.Homework:PublishDateAfterAcademicYear"
					)
						.WithData("PublishDate", division.PublishDate)
						.WithData("AcademicYearEnd", academicYear.EndDate);
				}
			}

			// Due Date validation
			if (division.PublishDate.HasValue && division.DueDate < division.PublishDate)
			{
				throw new BusinessException("ShikshaMitra.Homework:DueDateBeforePublishDate")
					.WithData("PublishDate", division.PublishDate)
					.WithData("DueDate", division.DueDate);
			}

			if (division.DueDate < academicYear.StartDate)
			{
				throw new BusinessException("ShikshaMitra.Homework:DueDateBeforeAcademicYear")
					.WithData("DueDate", division.DueDate)
					.WithData("AcademicYearStart", academicYear.StartDate);
			}

			if (division.DueDate > academicYear.EndDate)
			{
				throw new BusinessException("ShikshaMitra.Homework:DueDateAfterAcademicYear")
					.WithData("DueDate", division.DueDate)
					.WithData("AcademicYearEnd", academicYear.EndDate);
			}
		}
	}

	/// <summary>
	/// Validate homework type exists
	/// </summary>
	public async Task ValidateHomeworkTypeAsync(Guid homeworkTypeId)
	{
		var homeworkType = await _homeworkTypeRepository.FindAsync(homeworkTypeId);
		if (homeworkType == null)
		{
			throw new BusinessException("ShikshaMitra.Homework:InvalidHomeworkType").WithData(
				"HomeworkTypeId",
				homeworkTypeId
			);
		}
	}

	/// <summary>
	/// Validate homework can be updated (not published)
	/// </summary>
	public async Task ValidateCanUpdateAsync(Guid homeworkId)
	{
		var homework = await _repository.GetAsync(homeworkId, includeDetails: true);

		if (homework.HasPublishedDivisions)
		{
			throw new BusinessException("ShikshaMitra.Homework:CannotUpdatePublished").WithData(
				"HomeworkId",
				homeworkId
			);
		}
	}

	/// <summary>
	/// Validate homework can be published
	/// </summary>
	public async Task<HomeworkAssignmentEntity> ValidateCanPublishAsync(Guid homeworkId)
	{
		var homework = await _repository.GetAsync(homeworkId, includeDetails: true);

		if (homework.AllDivisionsPublished)
		{
			throw new BusinessException("ShikshaMitra.Homework:AlreadyPublished").WithData(
				"HomeworkId",
				homeworkId
			);
		}

		return homework;
	}
}
