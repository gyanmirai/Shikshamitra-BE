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
	IRepository<Student, Guid> studentRepository,
	IRepository<HouseSettings, Guid> houseSettingsRepository,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<StudentManagementSettings, Guid> studentManagementSettingsRepository,
	IUserScopeAccessService userScopeAccessService
) : ApplicationService, IStudentManagementAppService
{
	public async Task<(List<StudentRecordDto> StudentRecords, ArrangementTypeEnum? ArrangementType, ArrangementByEnum? ArrangementBy)> GetStudentRecords(Guid divisionId)
	{
		try
		{
			// Get students with their additional details
			var query = (await studentRepository.WithDetailsAsync())
				.Where(s => s.DivisionId == divisionId && s.Status == StudentLifeCycleStatusEnum.Enrolled);

			query = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
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
		foreach (var studentGrNumberDto in updateStudentGrNumberDto)
		{
			var student = await studentRepository.GetAsync(studentGrNumberDto.StudentId, includeDetails: true);
			await EnsureCanAccessStudentAsync(student, nameof(StoreGrNumbers));

			student.AssignGrNo(studentGrNumberDto.GrNo);
			await studentRepository.UpdateAsync(student);
		}
	}

	public async Task<List<GrProjectionDto>> GetGrProjections(Guid divisionId)
	{
		try
		{
			// get students in division which gr no is null
			var query = (await studentRepository.WithDetailsAsync())
				.Where(s => s.DivisionId == divisionId);

			query = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
			var students = await AsyncExecuter.ToListAsync(query);

			// Find board from divisionId
			var division = await divisionRepository.FindAsync(s => s.Id == divisionId, includeDetails: true) ?? throw new UserFriendlyException("Division not found.");
			var standard = await standardRepository.FindAsync(s => s.Id == division.StandardId);
			var educationMediumStageId = standard?.EducationMediumStageId;
			var educationMediumStage = await educationMediumStageRepository.GetAsync(s => s.Id == educationMediumStageId, includeDetails: true);
			var educationMedium = await educationMediumRepository.GetAsync(s => s.Id == educationMediumStage.EducationMediumId, includeDetails: true);
			var educationBoard = await educationBoardRepository.FindAsync(s => s.Id == educationMedium.EducationBoardId);
			var board = educationBoard.Type;

			if (board == EducationBoardType.CBSE)
			{
				// get students in medium which gr no is null
				var studentsWithoutGrNo = students.Where(s => s.GrNo == null).OrderBy(s => s.CreationTime);

				var queryable = await studentRepository.GetQueryableAsync();

				var lastGrNoStudent = queryable
					.Where(s => s.AcademicBoardId == educationBoard.Id && s.GrNo != null)
					.OrderByDescending(s => s.GrNo)
					.FirstOrDefault()?.GrNo ?? 0;

				foreach (var student in studentsWithoutGrNo)
				{
					student.AssignGrNo(++lastGrNoStudent);
				}
			}
			else if (board == EducationBoardType.GSEB)
			{
				var studentsWithoutGrNo = students.Where(s => s.GrNo == null).OrderBy(s => s.CreationTime);

				var queryable = await studentRepository.GetQueryableAsync();
				var lastGrNoStudent = queryable.
					Where(s => s.AcademicMediumStageId == educationMediumStage.Id && s.GrNo != null)
					.OrderByDescending(s => s.GrNo)
					.FirstOrDefault()?.GrNo ?? 0;

				foreach (var student in studentsWithoutGrNo)
				{
					student.AssignGrNo(++lastGrNoStudent);
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
				result.Add(new GrProjectionDto
				{
					StudentId = student.Id,
					GrNumber = student.GrNo,
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

	public async Task<List<RollNumberProjectionDto>> GetRollNumberProjections(Guid divisionId, ArrangementTypeEnum arrangementType, ArrangementByEnum arrangementBy)
	{
		try
		{
			// get students in division which roll no is null
			var query = (await studentRepository.WithDetailsAsync())
				.Where(s => s.DivisionId == divisionId && s.RollNo == null);

			query = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
			var students = await AsyncExecuter.ToListAsync(query);

			// arrange by first name or last name within the above arrangement
			if (arrangementBy == ArrangementByEnum.FirstName)
			{
				students = students
					.OrderBy(s => s.FirstName).ToList();
			}
			else if (arrangementBy == ArrangementByEnum.LastName)
			{
				students = students
					.OrderBy(s => s.LastName).ToList();
			}
			else
			{
				students = students
					.OrderBy(s => s.FirstName).ToList();
			}

			// arrange students based on arrangement type and arrangement by
			if (arrangementType == ArrangementTypeEnum.BoysFirst)
			{
				students = students
					.OrderBy(s => s.AdditionalDetail?.Gender == GenderEnum.Male ? 0 : 1).ToList();
			}
			else if (arrangementType == ArrangementTypeEnum.GirlsFirst)
			{
				students = students
					.OrderBy(s => s.AdditionalDetail?.Gender == GenderEnum.Female ? 0 : 1).ToList();
			}
			else
			{
				// Mix - no specific arrangement
			}

			// get last roll no in division
			var lastRollNo = (await studentRepository
				.GetQueryableAsync())
				.Where(s => s.DivisionId == divisionId && s.RollNo != null)
				.OrderByDescending(s => s.RollNo)
				.Select(s => new { s.RollNo })
				.FirstOrDefault();

			var rollNumberProjectionDto = new List<RollNumberProjectionDto>();
			var nextRollNo = (lastRollNo?.RollNo ?? 0) + 1;

			foreach (var student in students)
			{
				rollNumberProjectionDto.Add(new RollNumberProjectionDto
				{
					StudentId = student.Id,
					RollNo = nextRollNo++,
					StudentName = $"{student.FirstName} {student.MiddleName} {student.LastName}",
					GrNo = student.GrNo
				});
			}

			return rollNumberProjectionDto;
		}
		catch
		{
			throw;
		}
	}

	public async Task StoreRollNumbers(Guid divisionId, List<UpdateStudentRollNumberDto> updateStudentRollNumberDto, ArrangementTypeEnum? arrangementType, ArrangementByEnum? arrangementBy)
	{
		foreach (var studentRollNumberDto in updateStudentRollNumberDto)
		{
			var student = await studentRepository.GetAsync(studentRollNumberDto.StudentId, includeDetails: true);
			await EnsureCanAccessStudentAsync(student, nameof(StoreRollNumbers));

			// check that roll no is not assigned to any other student in same division
			var existingStudentWithRollNo = await studentRepository.FirstOrDefaultAsync(s => s.DivisionId == student.DivisionId && s.RollNo == studentRollNumberDto.RollNo && s.Id != student.Id);
			if (existingStudentWithRollNo != null)
			{
				throw new UserFriendlyException($"Roll number {studentRollNumberDto.RollNo} is already assigned to another student in the same division.");
			}

			student.AssignRollNo(studentRollNumberDto.RollNo);
			await studentRepository.UpdateAsync(student);

			// Store settings for arrangement type and arrangement by if provided for division
			if (arrangementType != null && arrangementBy != null)
			{
				var settings = new StudentManagementSettings
				{
					DivisionId = divisionId,
					ArrangementTye = arrangementType,
					ArrangementBy = arrangementBy
				};
				await studentManagementSettingsRepository.InsertAsync(settings);
			}
		}
	}

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
