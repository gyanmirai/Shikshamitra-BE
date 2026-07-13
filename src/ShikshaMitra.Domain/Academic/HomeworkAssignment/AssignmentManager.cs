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
/// Domain manager for AssignmentEntity
/// Handles business logic and validation
/// </summary>
public class AssignmentManager : DomainService
{
	private readonly IRepository<AssignmentEntity, Guid> _repository;
	private readonly IDivisionRepository _divisionRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IAssignmentTypeRepository _assignmentTypeRepository;

	private string EntityName => GetType().ToEntityNameFromManager();

	public AssignmentManager(
		IRepository<AssignmentEntity, Guid> repository,
		IDivisionRepository divisionRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IAssignmentTypeRepository assignmentTypeRepository
	)
	{
		_repository = repository;
		_divisionRepository = divisionRepository;
		_academicYearRepository = academicYearRepository;
		_assignmentTypeRepository = assignmentTypeRepository;
	}

	/// <summary>
	/// Validate assignment creation/update data
	/// </summary>
	public async Task ValidateAssignmentAsync(
		Guid academicYearId,
		Guid classId,
		List<(Guid DivisionId, DateTime? PublishDate, DateTime DueDate)> divisions
	)
	{
		// 1. Validate Academic Year exists and get dates
		var academicYear = await _academicYearRepository.FindAsync(academicYearId);
		if (academicYear == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:InvalidAcademicYear").WithData(
				"AcademicYearId",
				academicYearId
			);
		}

		// 2. Validate divisions
		if (divisions == null || divisions.Count == 0)
		{
			throw new BusinessException("ShikshaMitra.Assignment:AtLeastOneDivision");
		}

		var divisionIds = divisions.Select(d => d.DivisionId).ToList();
		var divisionEntities = await _divisionRepository.GetListAsync(d =>
			divisionIds.Contains(d.Id)
		);

		// Check all divisions exist
		if (divisionEntities.Count != divisionIds.Count)
		{
			var missingIds = divisionIds.Except(divisionEntities.Select(d => d.Id)).ToList();
			throw new BusinessException("ShikshaMitra.Assignment:InvalidDivision").WithData(
				"DivisionId",
				string.Join(", ", missingIds)
			);
		}

		// 3. Validate all divisions belong to same Standard
		var standardIds = divisionEntities.Select(d => d.StandardId).Distinct().ToList();
		if (standardIds.Count > 1)
		{
			throw new BusinessException("ShikshaMitra.Assignment:DivisionsDifferentStandards");
		}

		if (standardIds.FirstOrDefault() != classId)
		{
			throw new BusinessException("ShikshaMitra.Assignment:DivisionsNotMatchClass");
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
						"ShikshaMitra.Assignment:PublishDateBeforeAcademicYear"
					)
						.WithData("PublishDate", division.PublishDate)
						.WithData("AcademicYearStart", academicYear.StartDate);
				}

				if (division.PublishDate > academicYear.EndDate)
				{
					throw new BusinessException(
						"ShikshaMitra.Assignment:PublishDateAfterAcademicYear"
					)
						.WithData("PublishDate", division.PublishDate)
						.WithData("AcademicYearEnd", academicYear.EndDate);
				}
			}

			// Due Date validation
			if (division.PublishDate.HasValue && division.DueDate < division.PublishDate)
			{
				throw new BusinessException("ShikshaMitra.Assignment:DueDateBeforePublishDate")
					.WithData("PublishDate", division.PublishDate)
					.WithData("DueDate", division.DueDate);
			}

			if (division.DueDate < academicYear.StartDate)
			{
				throw new BusinessException("ShikshaMitra.Assignment:DueDateBeforeAcademicYear")
					.WithData("DueDate", division.DueDate)
					.WithData("AcademicYearStart", academicYear.StartDate);
			}

			if (division.DueDate > academicYear.EndDate)
			{
				throw new BusinessException("ShikshaMitra.Assignment:DueDateAfterAcademicYear")
					.WithData("DueDate", division.DueDate)
					.WithData("AcademicYearEnd", academicYear.EndDate);
			}
		}
	}

	/// <summary>
	/// Validate assignment type exists
	/// </summary>
	public async Task ValidateAssignmentTypeAsync(Guid assignmentTypeId)
	{
		var assignmentType = await _assignmentTypeRepository.FindAsync(assignmentTypeId);
		if (assignmentType == null)
		{
			throw new BusinessException("ShikshaMitra.Assignment:InvalidAssignmentType").WithData(
				"AssignmentTypeId",
				assignmentTypeId
			);
		}

		if (assignmentType.Status == TaskTypeStatus.Inactive)
		{
			throw new BusinessException("ShikshaMitra.Assignment:InactiveAssignmentType").WithData(
				"AssignmentTypeId",
				assignmentTypeId
			);
		}
	}

	/// <summary>
	/// Validate assignment can be updated (not published)
	/// </summary>
	public async Task ValidateCanUpdateAsync(Guid assignmentId)
	{
		var assignment = await _repository.GetAsync(assignmentId, includeDetails: true);

		if (assignment.HasPublishedDivisions)
		{
			throw new BusinessException("ShikshaMitra.Assignment:CannotUpdatePublished").WithData(
				"AssignmentId",
				assignmentId
			);
		}
	}

	/// <summary>
	/// Validate assignment can be published
	/// </summary>
	public async Task<AssignmentEntity> ValidateCanPublishAsync(Guid assignmentId)
	{
		var assignment = await _repository.GetAsync(assignmentId, includeDetails: true);

		if (assignment.AllDivisionsPublished)
		{
			throw new BusinessException("ShikshaMitra.Assignment:AlreadyPublished").WithData(
				"AssignmentId",
				assignmentId
			);
		}

		return assignment;
	}
}
