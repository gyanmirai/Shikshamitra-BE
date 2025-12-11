using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Shared helper class to enrich student enrollment DTOs with related entity names
/// (Standard, Academic Medium, Board, Year) to avoid N+1 database queries.
/// </summary>
public class StudentEnrollmentDtoEnricher : ITransientDependency
{
	private readonly IRepository<EducationMedium, Guid> _academicMediumRepository;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<InquiryPreference, Guid> _inquiryPreferenceRepository;

	public StudentEnrollmentDtoEnricher(
		IRepository<EducationMedium, Guid> academicMediumRepository,
		IRepository<Standard, Guid> standardRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IRepository<Student, Guid> studentRepository)
	{
		_academicMediumRepository = academicMediumRepository;
		_standardRepository = standardRepository;
		_educationBoardRepository = educationBoardRepository;
		_academicYearRepository = academicYearRepository;
		_studentRepository = studentRepository;
	}

	/// <summary>
	/// Enriches a single DTO with related entity names (makes 4 database calls max).
	/// Use this for single-item operations.
	/// </summary>
	public async Task EnrichSingleDtoAsync(StudentInquiryDto dto)
	{
		// Academic Medium Name and Board Name
		if (dto.AcademicMediumId != Guid.Empty)
		{
			var medium = await _academicMediumRepository.FindAsync(dto.AcademicMediumId, includeDetails: true);
			if (medium != null)
			{
				dto.AcademicMediumName = medium.Name;
				
				// Get Academic Board Name through Medium
				dto.AcademicBoardId = medium.EducationBoardId;
				var board = await _educationBoardRepository.FindAsync(medium.EducationBoardId);
				dto.AcademicBoardName = board?.Type.ToString();

				if (board != null)
				{
					dto.AcademicYearId = board.AcademicYearId;

					// Get Academic Year name through board
					var year = await _academicYearRepository.FindAsync(board.AcademicYearId);
					dto.AcademicYearName = year?.Name;
				}
			}
		}

		// Standard Name
		if (dto.StudentId != Guid.Empty)
		{
			// Get the student to read StandardId (mapping didn't include name)
			var student = await _studentRepository.FindAsync(dto.StudentId);
			if (student?.StandardId != null)
			{
				var std = await _standardRepository.FindAsync(student.StandardId.Value);
				dto.Standard = std?.Name ?? dto.Standard;
			}
		}

		// Inquiry Preference
		if (dto.InquiryPreferenceId != null && dto.InquiryPreferenceId != Guid.Empty)
		{
			var inquiryPreference = await _inquiryPreferenceRepository.FindAsync(dto.InquiryPreferenceId!.Value);
			if (inquiryPreference != null) {
				dto.InquiryPreference = inquiryPreference.PreferenceName;
			}
		}
	}

	/// <summary>
	/// Enriches a list of DTOs with related entity names using batch operations
	/// to avoid N+1 queries. Use this for list operations.
	/// </summary>
	public async Task EnrichDtoListAsync(List<StudentInquiryDto> dtos, List<Student> students)
	{
		if (dtos.Count == 0) return;

		// Collect all unique IDs
		var mediumIds = dtos.Select(d => d.AcademicMediumId).Where(id => id != Guid.Empty).Distinct().ToList();
		var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();

		// Batch fetch all entities sequentially to avoid DbContext concurrency issues
		var mediums = mediumIds.Any()
			? await _academicMediumRepository.GetListAsync(m => mediumIds.Contains(m.Id))
			: new List<EducationMedium>();

		var standards = standardIds.Any()
			? await _standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
			: new List<Standard>();

		// Get all board IDs from mediums
		var boardIds = mediums.Select(m => m.EducationBoardId).Distinct().ToList();
		var boards = boardIds.Any()
			? await _educationBoardRepository.GetListAsync(b => boardIds.Contains(b.Id))
			: new List<EducationBoard>();

		// Get all year IDs from boards
		var yearIds = boards.Select(b => b.AcademicYearId).Distinct().ToList();
		var years = yearIds.Any()
			? await _academicYearRepository.GetListAsync(y => yearIds.Contains(y.Id))
			: new List<AcademicYear>();

		// Create lookup dictionaries for fast access
		var mediumDict = mediums.ToDictionary(m => m.Id);
		var standardDict = standards.ToDictionary(s => s.Id);
		var boardDict = boards.ToDictionary(b => b.Id);
		var yearDict = years.ToDictionary(y => y.Id);
		var studentDict = students.ToDictionary(s => s.Id);

		// Enrich each DTO using lookups (no database calls in loop)
		foreach (var dto in dtos)
		{
			// Medium and related data
			if (mediumDict.TryGetValue(dto.AcademicMediumId, out var medium))
			{
				dto.AcademicMediumName = medium.Name;
				dto.AcademicBoardId = medium.EducationBoardId;

				if (boardDict.TryGetValue(medium.EducationBoardId, out var board))
				{
					dto.AcademicBoardName = board.Type.ToString();
					dto.AcademicYearId = board.AcademicYearId;

					if (yearDict.TryGetValue(board.AcademicYearId, out var year))
					{
						dto.AcademicYearName = year.Name;
					}
				}
			}

			// Standard name
			if (studentDict.TryGetValue(dto.StudentId, out var student) && student.StandardId.HasValue)
			{
				if (standardDict.TryGetValue(student.StandardId.Value, out var standard))
				{
					dto.Standard = standard.Name;
				}
			}
		}
	}

	/// <summary>
	/// Enriches a list of StudentRegistrationFormDto with related entity names using batch operations.
	/// </summary>
	public async Task EnrichRegistrationFormDtoListAsync(List<StudentRegistrationFormDto> dtos, List<Student> students)
	{
		if (dtos.Count == 0) return;

		// Collect all unique IDs
		var mediumIds = dtos.Select(d => d.AcademicMediumId).Where(id => id != Guid.Empty).Distinct().ToList();
		var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();

		// Batch fetch all entities
		var mediums = mediumIds.Any()
			? await _academicMediumRepository.GetListAsync(m => mediumIds.Contains(m.Id))
			: new List<EducationMedium>();

		var standards = standardIds.Any()
			? await _standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
			: new List<Standard>();

		// Get all board IDs from mediums
		var boardIds = mediums.Select(m => m.EducationBoardId).Distinct().ToList();
		var boards = boardIds.Any()
			? await _educationBoardRepository.GetListAsync(b => boardIds.Contains(b.Id))
			: new List<EducationBoard>();

		// Get all year IDs from boards
		var yearIds = boards.Select(b => b.AcademicYearId).Distinct().ToList();
		var years = yearIds.Any()
			? await _academicYearRepository.GetListAsync(y => yearIds.Contains(y.Id))
			: new List<AcademicYear>();

		// Create lookup dictionaries
		var mediumDict = mediums.ToDictionary(m => m.Id);
		var standardDict = standards.ToDictionary(s => s.Id);
		var boardDict = boards.ToDictionary(b => b.Id);
		var yearDict = years.ToDictionary(y => y.Id);
		var studentDict = students.ToDictionary(s => s.Id);

		// Enrich each DTO
		foreach (var dto in dtos)
		{
			// Medium and related data
			if (mediumDict.TryGetValue(dto.AcademicMediumId, out var medium))
			{
				dto.AcademicMediumName = medium.Name;
				dto.AcademicBoardId = medium.EducationBoardId;

				if (boardDict.TryGetValue(medium.EducationBoardId, out var board))
				{
					dto.AcademicBoardName = board.Type.ToString();
					dto.AcademicYearId = board.AcademicYearId;

					if (yearDict.TryGetValue(board.AcademicYearId, out var year))
					{
						dto.AcademicYearName = year.Name;
					}
				}
			}

			// Standard name
			if (studentDict.TryGetValue(dto.StudentId, out var student) && student.StandardId.HasValue)
			{
				if (standardDict.TryGetValue(student.StandardId.Value, out var standard))
				{
					dto.Standard = standard.Name;
				}
			}
		}
	}

	/// <summary>
	/// Enriches a list of StudentRegistrationDto with related entity names using batch operations.
	/// </summary>
	public async Task EnrichRegistrationDtoListAsync(List<StudentRegistrationDto> dtos, List<Student> students)
	{
		if (dtos.Count == 0) return;

		// Collect all unique IDs
		var mediumIds = dtos.Select(d => d.AcademicMediumId).Where(id => id != Guid.Empty).Distinct().ToList();
		var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();

		// Batch fetch all entities
		var mediums = mediumIds.Any()
			? await _academicMediumRepository.GetListAsync(m => mediumIds.Contains(m.Id))
			: new List<EducationMedium>();

		var standards = standardIds.Any()
			? await _standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
			: new List<Standard>();

		// Get all board IDs from mediums
		var boardIds = mediums.Select(m => m.EducationBoardId).Distinct().ToList();
		var boards = boardIds.Any()
			? await _educationBoardRepository.GetListAsync(b => boardIds.Contains(b.Id))
			: new List<EducationBoard>();

		// Get all year IDs from boards
		var yearIds = boards.Select(b => b.AcademicYearId).Distinct().ToList();
		var years = yearIds.Any()
			? await _academicYearRepository.GetListAsync(y => yearIds.Contains(y.Id))
			: new List<AcademicYear>();

		// Create lookup dictionaries
		var mediumDict = mediums.ToDictionary(m => m.Id);
		var standardDict = standards.ToDictionary(s => s.Id);
		var boardDict = boards.ToDictionary(b => b.Id);
		var yearDict = years.ToDictionary(y => y.Id);
		var studentDict = students.ToDictionary(s => s.Id);

		// Enrich each DTO
		foreach (var dto in dtos)
		{
			// Medium and related data
			if (mediumDict.TryGetValue(dto.AcademicMediumId, out var medium))
			{
				dto.AcademicMediumName = medium.Name;
				dto.AcademicBoardId = medium.EducationBoardId;

				if (boardDict.TryGetValue(medium.EducationBoardId, out var board))
				{
					dto.AcademicBoardName = board.Type.ToString();
					dto.AcademicYearId = board.AcademicYearId;

					if (yearDict.TryGetValue(board.AcademicYearId, out var year))
					{
						dto.AcademicYearName = year.Name;
					}
				}
			}

			// Standard name
			if (studentDict.TryGetValue(dto.StudentId, out var student) && student.StandardId.HasValue)
			{
				if (standardDict.TryGetValue(student.StandardId.Value, out var standard))
				{
					dto.Standard = standard.Name;
				}
			}
		}
	}

	/// <summary>
	/// Enriches a single StudentRegistrationDto with related entity names.
	/// Use this for single-item operations.
	/// </summary>
	public async Task EnrichRegistrationDtoAsync(StudentRegistrationDto dto, Student student)
	{
		// Academic Medium Name and Board Name
		if (dto.AcademicMediumId != Guid.Empty)
		{
			var medium = await _academicMediumRepository.FindAsync(dto.AcademicMediumId);
			if (medium != null)
			{
				dto.AcademicMediumName = medium.Name;
				
				// Get Academic Board Name through Medium
				dto.AcademicBoardId = medium.EducationBoardId;
				var board = await _educationBoardRepository.FindAsync(medium.EducationBoardId);
				dto.AcademicBoardName = board?.Type.ToString();

				if (board != null)
				{
					dto.AcademicYearId = board.AcademicYearId;

					// Get Academic Year name through board
					var year = await _academicYearRepository.FindAsync(board.AcademicYearId);
					dto.AcademicYearName = year?.Name;
				}
			}
		}

		// Standard Name
		if (student.StandardId.HasValue)
		{
			var standard = await _standardRepository.FindAsync(student.StandardId.Value);
			dto.Standard = standard?.Name;
		}
	}
}
