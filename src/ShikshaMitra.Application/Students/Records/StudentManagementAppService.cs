using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentManagementAppService(
	IStudentRepository studentRepository,
	IRepository<HouseSettings, Guid> houseSettingsRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<StudentManagementSettings, Guid> studentManagementSettingsRepository,
	IRepository<AcademicTerm, Guid> academicTermRepository,
	IRepository<StudentRollNumberAssignment, Guid> rollNumberAssignmentRepository,
	IUserScopeAccessService userScopeAccessService,
	StudentScopeAccessService studentScopeAccessService
) : ApplicationService, IStudentManagementAppService
{
	public async Task<(List<StudentRecordDto> StudentRecords, ArrangementTypeEnum? ArrangementType, ArrangementByEnum? ArrangementBy)> GetStudentRecords(Guid divisionId)
	{
		try
		{
			await studentScopeAccessService.EnsureCanAccessDivisionAsync(divisionId, nameof(GetStudentRecords));

			// Get students with their additional details
			var query = (await studentRepository.WithDetailsAsync())
				.Where(s => s.DivisionId == divisionId && s.Status == StudentLifeCycleStatusEnum.Enrolled);

			query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);
			var students = await AsyncExecuter.ToListAsync(query);

			// Get arrangement settings for the division
			var settings = await studentManagementSettingsRepository.FirstOrDefaultAsync(s => s.DivisionId == divisionId);

			// Get all unique house IDs
			var houseIds = students
				.Where(s => s.AdditionalDetail?.HouseId != null)
				.Select(s => s.AdditionalDetail!.HouseId!.Value)
				.Distinct()
				.ToList();

			// Fetch all houses at once
			var houses = houseIds.Any()
				? await houseSettingsRepository.GetListAsync(h => houseIds.Contains(h.Id))
				: new List<HouseSettings>();

			var houseDictionary = houses.ToDictionary(h => h.Id);

			var result = new List<StudentRecordDto>();
			foreach (var student in students)
			{
				HouseSettings? house = null;
				if (student.AdditionalDetail?.HouseId != null)
				{
					houseDictionary.TryGetValue(student.AdditionalDetail.HouseId.Value, out house);
				}

				result.Add(new StudentRecordDto
				{
					StudentId = student.Id,
					StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}",
					GrNo = student.GrNo,
					RollNo = student.RollNo,
					HouseSettingId = student.AdditionalDetail?.HouseId,
					HouseSetting = house != null
						? new HouseSettingsDto
						{
							Name = house.Name,
							Color = house.Color,
							LogoUrl = house.LogoUrl,
							Description = house.Description,
						}
						: null,
					IsDivisionHouseLeader = student.AdditionalDetail?.IsDivisionHouseLeader,
					IsGlobalHouseLeader = student.AdditionalDetail?.IsGlobalHouseLeader
				});
			}

			return (result, settings?.ArrangementTye, settings?.ArrangementBy);
		}
		catch
		{
			throw new UserFriendlyException("An error occurred while fetching student records.");
		}
	}
	public async Task AssignHouseToStudent(Guid studentId, Guid houseId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await EnsureCanAccessStudentAsync(student, nameof(AssignHouseToStudent));

		var house = await houseSettingsRepository.GetAsync(houseId);
		await studentScopeAccessService.EnsureCanAccessMediumAsync(house.AcademicMediumId, nameof(AssignHouseToStudent));
		if (house.AcademicMediumId != student.AcademicMediumId)
		{
			throw new UserFriendlyException("House does not belong to the student's academic medium.");
		}

		// assign house to student only if he is not assigned with other house
		if (student.AdditionalDetail?.HouseId != null)
		{
			throw new UserFriendlyException("Student is already assigned to a house.");
		}
		student.AssignHouse(houseId);
		await studentRepository.UpdateAsync(student);
	}

	public async Task AssignHouseGlobalLeaderAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await EnsureCanAccessStudentAsync(student, nameof(AssignHouseGlobalLeaderAsync));
		// Global leader can be assigned to only one student in whole medium
		var globalLeader = await studentRepository.FirstOrDefaultAsync(s => s.AcademicMediumId == student.AcademicMediumId && s.AdditionalDetail != null && s.AdditionalDetail!.IsGlobalHouseLeader == true);
		if (globalLeader != null)
		{
			throw new UserFriendlyException("Global leader can be assigned to only one student in whole medium.");
		}
		student.AssignGlobalLeader();
		await studentRepository.UpdateAsync(student);
	}

	public async Task AssignHouseDivisionLeaderAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await EnsureCanAccessStudentAsync(student, nameof(AssignHouseDivisionLeaderAsync));
		// Division leader can be assigned to only one student in student's division
		var divisionLeader = await studentRepository.FirstOrDefaultAsync(s => s.DivisionId == student.DivisionId && s.AdditionalDetail != null && s.AdditionalDetail!.IsDivisionHouseLeader == true);
		if (divisionLeader != null)
		{
			throw new UserFriendlyException("Division leader can be assigned to only one student in student's division.");
		}
		student.AssignDivisionLeader();
		await studentRepository.UpdateAsync(student);
	}

	public async Task DeassignHouseFromStudent(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await EnsureCanAccessStudentAsync(student, nameof(DeassignHouseFromStudent));
		// If student is assigned with a house
		if (student.AdditionalDetail?.HouseId == null)
		{
			throw new UserFriendlyException("Student is not assigned with a house.");
		}
		student.DeassignHouse();
		await studentRepository.UpdateAsync(student);
	}

	public async Task DeassignHouseGlobalLeaderAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await EnsureCanAccessStudentAsync(student, nameof(DeassignHouseGlobalLeaderAsync));
		// If student is not assigned with a house or he is not global leader
		if (student.AdditionalDetail?.HouseId == null || student.AdditionalDetail!.IsGlobalHouseLeader == false)
		{
			throw new UserFriendlyException("Student is not assigned with a house or student is not global leader.");
		}
		student.DeassignGlobalLeader();
		await studentRepository.UpdateAsync(student);
	}

	public async Task DeassignHouseDivisionLeaderAsync(Guid studentId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
		await EnsureCanAccessStudentAsync(student, nameof(DeassignHouseDivisionLeaderAsync));
		// If student is not assigned with a house or he is not division leader
		if (student.AdditionalDetail?.HouseId == null || student.AdditionalDetail!.IsDivisionHouseLeader == false)
		{
			throw new UserFriendlyException("Student is not assigned with a house or student is not division leader.");
		}
		student.DeassignDivisionLeader();
		await studentRepository.UpdateAsync(student);
	}

	public async Task StoreGrNumbers(List<UpdateStudentGrNumberDto> updateStudentGrNumberDto)
	{
		if (updateStudentGrNumberDto == null || updateStudentGrNumberDto.Count == 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "At least one student GR number is required.");
		}

		var duplicateStudentIds = updateStudentGrNumberDto
			.GroupBy(x => x.StudentId)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();
		if (duplicateStudentIds.Count != 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "Duplicate student entries are not allowed.")
				.WithData("StudentIds", string.Join(", ", duplicateStudentIds));
		}

		var duplicateGrNumbers = updateStudentGrNumberDto
			.Where(x => x.GrNo.HasValue)
			.GroupBy(x => x.GrNo!.Value)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.OrderBy(x => x)
			.ToList();
		if (duplicateGrNumbers.Count != 0)
		{
			var message = $"GR Number {string.Join(", ", duplicateGrNumbers)} is already assigned to another student.";
			throw new BusinessException(StudentErrorCodes.DuplicateGrNo, message)
				.WithData("GrNumbers", string.Join(", ", duplicateGrNumbers));
		}

		var invalidGrNumbers = updateStudentGrNumberDto
			.Where(x => x.GrNo.HasValue && x.GrNo.Value <= 0)
			.Select(x => x.GrNo!.Value)
			.Distinct()
			.OrderBy(x => x)
			.ToList();
		if (invalidGrNumbers.Count != 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "GR numbers must be greater than zero.")
				.WithData("GrNumbers", string.Join(", ", invalidGrNumbers));
		}

		var requestedStudentIds = updateStudentGrNumberDto.Select(x => x.StudentId).ToList();
		var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var query = studentQuery.Where(s => requestedStudentIds.Contains(s.Id));
		query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);
		var students = await AsyncExecuter.ToListAsync(query);
		if (students.Count != requestedStudentIds.Count)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "One or more students were not found or are outside the current user scope.");
		}

		var requestedGrNumbers = updateStudentGrNumberDto
			.Where(x => x.GrNo.HasValue)
			.Select(x => x.GrNo!.Value)
			.ToList();
		if (requestedGrNumbers.Count != 0)
		{
			var requestedStudentByGrNo = updateStudentGrNumberDto
				.Where(x => x.GrNo.HasValue)
				.ToDictionary(x => x.GrNo!.Value, x => x.StudentId);
			var existingAssignments = await AsyncExecuter.ToListAsync(
				studentQuery
					.Where(s => s.GrNo.HasValue && requestedGrNumbers.Contains(s.GrNo.Value))
					.Select(s => new { s.Id, GrNo = s.GrNo!.Value }));
			var conflictingGrNumbers = existingAssignments
				.Where(existing =>
					requestedStudentByGrNo.TryGetValue(existing.GrNo, out var requestedStudentId) &&
					requestedStudentId != existing.Id)
				.Select(existing => existing.GrNo)
				.Distinct()
				.OrderBy(x => x)
				.ToList();
			if (conflictingGrNumbers.Count != 0)
			{
				var message = $"GR Number {string.Join(", ", conflictingGrNumbers)} is already assigned to another student.";
				throw new BusinessException(StudentErrorCodes.DuplicateGrNo, message)
					.WithData("GrNumbers", string.Join(", ", conflictingGrNumbers));
			}
		}

		var studentsById = students.ToDictionary(s => s.Id);
		foreach (var studentGrNumberDto in updateStudentGrNumberDto)
		{
			var student = studentsById[studentGrNumberDto.StudentId];
			await EnsureCanAccessStudentAsync(student, nameof(StoreGrNumbers));

			student.AssignGrNo(studentGrNumberDto.GrNo);
			await studentRepository.UpdateAsync(student, autoSave: false);
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();
	}

	public async Task<List<GrProjectionDto>> GetGrProjections(Guid divisionId)
	{
		try
		{
			await studentScopeAccessService.EnsureCanAccessDivisionAsync(divisionId, nameof(GetGrProjections));

			var query = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
				.Where(s => s.DivisionId == divisionId);

			query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);
			var students = await AsyncExecuter.ToListAsync(
				query.Select(s => new
				{
					s.Id,
					s.FirstName,
					s.MiddleName,
					s.LastName,
					s.GrNo,
					s.CreationTime
				}));

			// Find board from divisionId
			var division = await divisionRepository.FindAsync(s => s.Id == divisionId, includeDetails: true) ?? throw new UserFriendlyException("Division not found.");
			var standard = await standardRepository.FindAsync(s => s.Id == division.StandardId) ?? throw new UserFriendlyException("Standard not found.");
			var educationMediumStage = await educationMediumStageRepository.FindAsync(s => s.Id == standard.EducationMediumStageId, includeDetails: true) ?? throw new UserFriendlyException("Education medium stage not found.");
			var educationMedium = await educationMediumRepository.FindAsync(s => s.Id == educationMediumStage.EducationMediumId, includeDetails: true) ?? throw new UserFriendlyException("Education medium not found.");
			var educationBoard = await educationBoardRepository.FindAsync(s => s.Id == educationMedium.EducationBoardId) ?? throw new UserFriendlyException("Education board not found.");
			var board = educationBoard.Type;

			var projectedGrNumbers = new Dictionary<Guid, int>();
			if (board == EducationBoardType.CBSE)
			{
				var studentsWithoutGrNo = students.Where(s => s.GrNo == null).OrderBy(s => s.CreationTime);

				var queryable = await studentRepository.GetQueryableWithoutAutoIncludesAsync();

				var lastGrNoStudent = await AsyncExecuter.FirstOrDefaultAsync(
					queryable
						.Where(s => s.AcademicBoardId == educationBoard.Id && s.GrNo != null)
						.OrderByDescending(s => s.GrNo)
						.Select(s => s.GrNo)) ?? 0;

				foreach (var student in studentsWithoutGrNo)
				{
					projectedGrNumbers[student.Id] = ++lastGrNoStudent;
				}
			}
			else if (board == EducationBoardType.GSEB)
			{
				var studentsWithoutGrNo = students.Where(s => s.GrNo == null).OrderBy(s => s.CreationTime);

				var queryable = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
				var lastGrNoStudent = await AsyncExecuter.FirstOrDefaultAsync(
					queryable
						.Where(s => s.AcademicMediumStageId == educationMediumStage.Id && s.GrNo != null)
						.OrderByDescending(s => s.GrNo)
						.Select(s => s.GrNo)) ?? 0;

				foreach (var student in studentsWithoutGrNo)
				{
					projectedGrNumbers[student.Id] = ++lastGrNoStudent;
				}
			}
			else
			{
				throw new UserFriendlyException("Board not supported for GR number projection.");
			}

			var result = new List<GrProjectionDto>();

			// prepare projection dto
			foreach (var student in students)
			{
				int? grNumber = student.GrNo;
				if (grNumber == null && projectedGrNumbers.TryGetValue(student.Id, out var projectedGrNo))
				{
					grNumber = projectedGrNo;
				}

				result.Add(new GrProjectionDto
				{
					StudentId = student.Id,
					GrNumber = grNumber,
					StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}"
				});
			}

			return result;
		}
		catch
		{
			throw;
		}
	}

	public async Task<List<AcademicTermDto>> GetAcademicTermsAsync(Guid academicYearId)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetAcademicTermsAsync));
		await EnsureDefaultAcademicTermAsync(academicYearId);

		var terms = await AsyncExecuter.ToListAsync(
			(await academicTermRepository.GetQueryableAsync())
				.Where(term => term.AcademicYearId == academicYearId && term.IsActive)
				.OrderBy(term => term.Sequence)
				.ThenBy(term => term.Name));

		return terms.Select(MapAcademicTerm).ToList();
	}

	public async Task<AcademicTermDto> CreateAcademicTermAsync(CreateAcademicTermDto input)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(input.AcademicYearId, nameof(CreateAcademicTermAsync));

		if (input.Sequence <= 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "Academic term sequence must be greater than zero.");
		}

		var normalizedName = NormalizeTermName(input.Name);
		var existingTerms = await AsyncExecuter.ToListAsync(
			(await academicTermRepository.GetQueryableAsync())
				.Where(term => term.AcademicYearId == input.AcademicYearId));
		if (existingTerms.Any(term =>
			    term.Sequence == input.Sequence ||
			    NormalizeTermName(term.Name) == normalizedName))
		{
			throw new BusinessException(StudentErrorCodes.AcademicTermAlreadyExists)
				.WithData("AcademicYearId", input.AcademicYearId)
				.WithData("Name", input.Name)
				.WithData("Sequence", input.Sequence);
		}

		if (input.IsCurrent)
		{
			foreach (var existingCurrent in existingTerms.Where(term => term.IsCurrent))
			{
				existingCurrent.ClearCurrent();
				await academicTermRepository.UpdateAsync(existingCurrent, autoSave: false);
			}
		}

		var term = new AcademicTerm(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			input.Name,
			input.Sequence,
			input.IsCurrent);
		await academicTermRepository.InsertAsync(term, autoSave: true);

		return MapAcademicTerm(term);
	}

	public async Task<AcademicTermDto> SetCurrentAcademicTermAsync(Guid academicTermId)
	{
		var term = await academicTermRepository.GetAsync(academicTermId);
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(term.AcademicYearId, nameof(SetCurrentAcademicTermAsync));

		var terms = await AsyncExecuter.ToListAsync(
			(await academicTermRepository.GetQueryableAsync())
				.Where(existing => existing.AcademicYearId == term.AcademicYearId));
		foreach (var existing in terms)
		{
			if (existing.Id == academicTermId)
			{
				existing.MarkCurrent();
			}
			else
			{
				existing.ClearCurrent();
			}

			await academicTermRepository.UpdateAsync(existing, autoSave: false);
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();
		return MapAcademicTerm(term);
	}

	public async Task<RollNumberProjectionResponseDto> GetRollNumberProjections(Guid divisionId, Guid? academicTermId, ArrangementTypeEnum? arrangementType, ArrangementByEnum? arrangementBy)
	{
		try
		{
			await studentScopeAccessService.EnsureCanAccessDivisionAsync(divisionId, nameof(GetRollNumberProjections));
			var academicTerm = await ResolveAcademicTermAsync(divisionId, academicTermId, nameof(GetRollNumberProjections));
			var settings = await studentManagementSettingsRepository.FirstOrDefaultAsync(s => s.DivisionId == divisionId);
			var effectiveArrangementType = arrangementType ?? settings?.ArrangementTye ?? ArrangementTypeEnum.Mix;
			var effectiveArrangementBy = arrangementBy ?? settings?.ArrangementBy ?? ArrangementByEnum.FirstName;

			// Keep roll projection lean; avoid loading the full student aggregate graph.
			var query = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
				.Where(s => s.DivisionId == divisionId && s.Status == StudentLifeCycleStatusEnum.Enrolled);

			query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);
			query = ApplyRollArrangement(query, effectiveArrangementType, effectiveArrangementBy);
			var students = await AsyncExecuter.ToListAsync(query);

			var studentIds = students.Select(s => s.Id).ToList();
			var assignments = await AsyncExecuter.ToListAsync(
				(await rollNumberAssignmentRepository.GetQueryableAsync()).Where(assignment =>
					assignment.AcademicTermId == academicTerm.Id &&
					studentIds.Contains(assignment.StudentId)));
			var assignmentsByStudentId = assignments.ToDictionary(assignment => assignment.StudentId);
			var nextRollNo = (assignments
				.Where(assignment => assignment.DivisionId == divisionId && assignment.RollNo.HasValue)
				.Select(assignment => assignment.RollNo!.Value)
				.DefaultIfEmpty(0)
				.Max()) + 1;

			var rollNumberProjectionDto = new List<RollNumberProjectionDto>();
			foreach (var student in students)
			{
				var assignedRollNo = assignmentsByStudentId.GetValueOrDefault(student.Id)?.RollNo;
				rollNumberProjectionDto.Add(new RollNumberProjectionDto
				{
					StudentId = student.Id,
					AcademicTermId = academicTerm.Id,
					RollNo = assignedRollNo ?? nextRollNo++,
					StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}",
					GrNo = student.GrNo
				});
			}

			return new RollNumberProjectionResponseDto
			{
				TotalCount = rollNumberProjectionDto.Count,
				PageSize = rollNumberProjectionDto.Count,
				TotalPages = rollNumberProjectionDto.Count == 0 ? 0 : 1,
				ArrangementType = effectiveArrangementType,
				ArrangementBy = effectiveArrangementBy,
				Items = rollNumberProjectionDto
			};
		}
		catch
		{
			throw;
		}
	}

	public async Task<RollNumberSaveResultDto> StoreRollNumbers(Guid divisionId, Guid? academicTermId, List<UpdateStudentRollNumberDto> updateStudentRollNumberDto, ArrangementTypeEnum? arrangementType, ArrangementByEnum? arrangementBy)
	{
		await studentScopeAccessService.EnsureCanAccessDivisionAsync(divisionId, nameof(StoreRollNumbers));
		var academicTerm = await ResolveAcademicTermAsync(divisionId, academicTermId, nameof(StoreRollNumbers));

		if (updateStudentRollNumberDto == null || updateStudentRollNumberDto.Count == 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "At least one student roll number is required.");
		}

		var duplicateStudentIds = updateStudentRollNumberDto
			.GroupBy(x => x.StudentId)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();
		if (duplicateStudentIds.Count != 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "Duplicate student entries are not allowed.")
				.WithData("StudentIds", string.Join(", ", duplicateStudentIds));
		}

		var duplicateRollNumbers = updateStudentRollNumberDto
			.Where(x => x.RollNo.HasValue)
			.GroupBy(x => x.RollNo!.Value)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.OrderBy(x => x)
			.ToList();
		if (duplicateRollNumbers.Count != 0)
		{
			throw new BusinessException(StudentErrorCodes.DuplicateRollNumber)
				.WithData("RollNumbers", string.Join(", ", duplicateRollNumbers));
		}

		var invalidRollNumbers = updateStudentRollNumberDto
			.Where(x => x.RollNo.HasValue && x.RollNo.Value <= 0)
			.Select(x => x.RollNo!.Value)
			.Distinct()
			.OrderBy(x => x)
			.ToList();
		if (invalidRollNumbers.Count != 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "Roll numbers must be greater than zero.")
				.WithData("RollNumbers", string.Join(", ", invalidRollNumbers));
		}

		var requestedStudentIds = updateStudentRollNumberDto.Select(x => x.StudentId).ToList();
		var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var enrolledStudentQuery = studentQuery.Where(s => s.Status == StudentLifeCycleStatusEnum.Enrolled);
		var query = enrolledStudentQuery.Where(s => requestedStudentIds.Contains(s.Id));
		query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);
		var students = await AsyncExecuter.ToListAsync(query);
		if (students.Count != requestedStudentIds.Count)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "One or more students were not found, are not enrolled, or are outside the current user scope.");
		}

		var studentsById = students.ToDictionary(s => s.Id);
		var outOfDivisionStudents = students
			.Where(s => s.DivisionId != divisionId)
			.Select(s => s.Id)
			.ToList();
		if (outOfDivisionStudents.Count != 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "Every student must belong to the selected division.")
				.WithData("StudentIds", string.Join(", ", outOfDivisionStudents));
		}

		var divisionStudentCount = await AsyncExecuter.CountAsync(
			await studentScopeAccessService.ApplyCurrentUserScopeAsync(
				enrolledStudentQuery.Where(s => s.DivisionId == divisionId)));
		var rollNumbersAboveStudentCount = updateStudentRollNumberDto
			.Where(x => x.RollNo.HasValue && x.RollNo.Value > divisionStudentCount)
			.Select(x => x.RollNo!.Value)
			.Distinct()
			.OrderBy(x => x)
			.ToList();
		if (rollNumbersAboveStudentCount.Count != 0)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", $"Roll numbers cannot exceed total students ({divisionStudentCount}).")
				.WithData("MaxRollNo", divisionStudentCount)
				.WithData("RollNumbers", string.Join(", ", rollNumbersAboveStudentCount));
		}

		var requestedRollNumbers = updateStudentRollNumberDto
			.Where(x => x.RollNo.HasValue)
			.Select(x => x.RollNo!.Value)
			.ToList();
		if (requestedRollNumbers.Count != 0)
		{
			var existingConflicts = await AsyncExecuter.ToListAsync(
				(await rollNumberAssignmentRepository.GetQueryableAsync()).Where(assignment =>
					assignment.AcademicTermId == academicTerm.Id &&
					assignment.DivisionId == divisionId &&
					assignment.RollNo.HasValue &&
					requestedRollNumbers.Contains(assignment.RollNo.Value) &&
					!requestedStudentIds.Contains(assignment.StudentId)));
			if (existingConflicts.Count != 0)
			{
				throw new BusinessException(StudentErrorCodes.DuplicateRollNumber)
					.WithData("RollNumbers", string.Join(", ", existingConflicts
						.Select(s => s.RollNo)
						.Where(x => x.HasValue)
						.Select(x => x!.Value)
						.Distinct()
						.OrderBy(x => x)));
			}
		}

		var result = new RollNumberSaveResultDto
		{
			AcademicTermId = academicTerm.Id,
			TotalRequested = updateStudentRollNumberDto.Count
		};

		var existingAssignments = await AsyncExecuter.ToListAsync(
			(await rollNumberAssignmentRepository.GetQueryableAsync()).Where(assignment =>
				assignment.AcademicTermId == academicTerm.Id &&
				requestedStudentIds.Contains(assignment.StudentId)));
		var assignmentsByStudentId = existingAssignments.ToDictionary(assignment => assignment.StudentId);

		foreach (var studentRollNumberDto in updateStudentRollNumberDto)
		{
			var student = studentsById[studentRollNumberDto.StudentId];
			assignmentsByStudentId.TryGetValue(student.Id, out var assignment);
			var oldRollNo = assignment?.RollNo;
			var newRollNo = studentRollNumberDto.RollNo;
			var status = oldRollNo == newRollNo
				? "Skipped"
				: newRollNo.HasValue
					? "Applied"
					: "Cleared";

			if (assignment == null)
			{
				assignment = new StudentRollNumberAssignment(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					student.Id,
					divisionId,
					academicTerm.Id,
					newRollNo);
				await rollNumberAssignmentRepository.InsertAsync(assignment, autoSave: false);
			}
			else
			{
				assignment.Update(divisionId, newRollNo);
				await rollNumberAssignmentRepository.UpdateAsync(assignment, autoSave: false);
			}

			if (academicTerm.IsCurrent)
			{
				student.AssignRollNo(newRollNo);
				await studentRepository.UpdateAsync(student, autoSave: false);
			}

			result.Results.Add(new RollNumberSaveItemResultDto
			{
				StudentId = student.Id,
				OldRollNo = oldRollNo,
				NewRollNo = newRollNo,
				Status = status
			});
		}

		await UnitOfWorkManager.Current!.SaveChangesAsync();

		result.AppliedCount = result.Results.Count(x => x.Status == "Applied");
		result.ClearedCount = result.Results.Count(x => x.Status == "Cleared");
		result.SkippedCount = result.Results.Count(x => x.Status == "Skipped");
		result.ConflictCount = 0;

		if (arrangementType != null && arrangementBy != null)
		{
			var settings = await studentManagementSettingsRepository.FirstOrDefaultAsync(s => s.DivisionId == divisionId);
			if (settings == null)
			{
				settings = new StudentManagementSettings
				{
					DivisionId = divisionId,
					ArrangementTye = arrangementType,
					ArrangementBy = arrangementBy
				};
				await studentManagementSettingsRepository.InsertAsync(settings);
			}
			else
			{
				settings.ArrangementTye = arrangementType;
				settings.ArrangementBy = arrangementBy;
				await studentManagementSettingsRepository.UpdateAsync(settings);
			}
		}

		return result;
	}

	private async Task<AcademicTerm> ResolveAcademicTermAsync(Guid divisionId, Guid? academicTermId, string operation)
	{
		var divisionScope = await BuildDivisionScopeAsync(divisionId);
		if (!divisionScope.AcademicYearId.HasValue)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "Selected division is not linked to an academic year.");
		}

		if (!academicTermId.HasValue)
		{
			return await EnsureDefaultAcademicTermAsync(divisionScope.AcademicYearId.Value);
		}

		var academicTerm = await academicTermRepository.GetAsync(academicTermId.Value);
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicTerm.AcademicYearId, operation);
		if (!academicTerm.IsActive)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "Selected academic term is inactive.");
		}

		if (academicTerm.AcademicYearId != divisionScope.AcademicYearId.Value)
		{
			throw new BusinessException(StudentErrorCodes.RollNumberBatchInvalid)
				.WithData("Reason", "Selected academic term does not belong to the division academic year.")
				.WithData("AcademicTermId", academicTerm.Id)
				.WithData("DivisionId", divisionId);
		}

		return academicTerm;
	}

	private async Task<AcademicTerm> EnsureDefaultAcademicTermAsync(Guid academicYearId)
	{
		var terms = await AsyncExecuter.ToListAsync(
			(await academicTermRepository.GetQueryableAsync())
				.Where(term => term.AcademicYearId == academicYearId));

		var currentTerm = terms.FirstOrDefault(term => term.IsCurrent && term.IsActive);
		if (currentTerm != null)
		{
			return currentTerm;
		}

		var firstActiveTerm = terms
			.Where(term => term.IsActive)
			.OrderBy(term => term.Sequence)
			.ThenBy(term => term.Name)
			.FirstOrDefault();
		if (firstActiveTerm != null)
		{
			firstActiveTerm.MarkCurrent();
			await academicTermRepository.UpdateAsync(firstActiveTerm, autoSave: true);
			return firstActiveTerm;
		}

		var defaultTerm = new AcademicTerm(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			"Semester 1",
			1,
			isCurrent: true);
		await academicTermRepository.InsertAsync(defaultTerm, autoSave: true);
		return defaultTerm;
	}

	private static AcademicTermDto MapAcademicTerm(AcademicTerm term) => new()
	{
		Id = term.Id,
		AcademicYearId = term.AcademicYearId,
		Name = term.Name,
		Sequence = term.Sequence,
		IsCurrent = term.IsCurrent,
		IsActive = term.IsActive
	};

	private static IQueryable<Student> ApplyRollArrangement(
		IQueryable<Student> students,
		ArrangementTypeEnum arrangementType,
		ArrangementByEnum arrangementBy)
	{
		IOrderedQueryable<Student> ordered = arrangementType switch
		{
			ArrangementTypeEnum.BoysFirst => students.OrderBy(s => s.AdditionalDetail != null && s.AdditionalDetail.Gender == GenderEnum.Male ? 0 : 1),
			ArrangementTypeEnum.GirlsFirst => students.OrderBy(s => s.AdditionalDetail != null && s.AdditionalDetail.Gender == GenderEnum.Female ? 0 : 1),
			_ => students.OrderBy(_ => 0)
		};

		ordered = arrangementBy == ArrangementByEnum.LastName
			? ordered.ThenBy(s => s.LastName).ThenBy(s => s.FirstName)
			: ordered.ThenBy(s => s.FirstName).ThenBy(s => s.LastName);

		return ordered.ThenBy(s => s.Id);
	}

	private static string NormalizeTermName(string name)
		=> string.Join(" ", (name ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
			.ToUpperInvariant();

	public async Task ChangeDivision(List<ChangeStudentDivisionDto> changeStudentDivisionDto)
	{
		try
		{
			foreach (var changeStudentDivision in changeStudentDivisionDto)
			{
				var student = await studentRepository.GetAsync(changeStudentDivision.StudentId, includeDetails: true);
				await EnsureCanAccessStudentAsync(student, nameof(ChangeDivision));
				await EnsureCanAccessDivisionAsync(changeStudentDivision.DivisionId, nameof(ChangeDivision));
				student.AssignDivision(changeStudentDivision.DivisionId);
				student.AssignRollNo(null);
				await studentRepository.UpdateAsync(student);
			}
		}
		catch
		{
			throw;
		}
	}

	private async Task EnsureCanAccessStudentAsync(Student student, string operation)
	{
		await userScopeAccessService.EnsureCurrentUserCanAccessAsync(student, operation: operation);
	}

	private async Task EnsureCanAccessDivisionAsync(Guid divisionId, string operation)
	{
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var divisionScope = await BuildDivisionScopeAsync(divisionId);
		userScopeAccessService.EnsureCanAccess(divisionScope, scope, operation: operation);
	}

	private async Task<AcademicOrganizationScopeRow> BuildDivisionScopeAsync(Guid divisionId)
	{
		var division = await divisionRepository.GetAsync(divisionId);
		var standard = await standardRepository.GetAsync(division.StandardId);
		var stage = await educationMediumStageRepository.GetAsync(standard.EducationMediumStageId);
		var medium = await educationMediumRepository.GetAsync(stage.EducationMediumId);
		var board = await educationBoardRepository.GetAsync(medium.EducationBoardId);

		return new AcademicOrganizationScopeRow(
			board.AcademicYearId,
			board.Id,
			medium.Id,
			stage.Id,
			standard.Id,
			division.Id);
	}

	private sealed record AcademicOrganizationScopeRow(
		Guid? AcademicYearId,
		Guid? EducationBoardId,
		Guid? EducationMediumId,
		Guid? EducationMediumStageId,
		Guid? StandardId,
		Guid? DivisionId) : IUserScopedEntity;
}
