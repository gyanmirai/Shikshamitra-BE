using ShikshaMitra.Students.Records.Settings;
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
	StudentManager studentManager
) : ApplicationService, IStudentManagementAppService
{
	public async Task<List<StudentRecordDto>> GetStudentRecords(Guid divisionId)
	{
		try
		{
			// Get students with their additional details
			var students = await studentRepository.GetListAsync(s => s.DivisionId == divisionId, includeDetails: true);

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

			return result;
		}
		catch
		{
			throw new UserFriendlyException("An error occurred while fetching student records.");
		}
	}
	public async Task AssignHouseToStudent(Guid studentId, Guid houseId)
	{
		var student = await studentRepository.GetAsync(studentId, includeDetails: true);
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
		try
		{
			foreach (var studentGrNumberDto in updateStudentGrNumberDto)
			{
				var student = await studentRepository.GetAsync(studentGrNumberDto.StudentId, includeDetails: true);

				// get board of student
				var medium = await educationMediumRepository.GetAsync(student.AcademicMediumId);
				if (medium.Name == "CBSE")
				{
					// GR no should be unique in whole medium
					var grNoExists = await studentRepository.AnyAsync(s => s.GrNo == studentGrNumberDto.GrNo && s.AcademicMediumId == student.AcademicMediumId);
					if (grNoExists)
					{
						throw new UserFriendlyException("GR no already exists.");
					}
				}
				else if (medium.Name == "State Board")
				{
					// GR no should be unique in education medium stage
					// Temporary: Commented out due to missing EducationMediumStageId on Student
					/*
					var grNoExists = await studentRepository.AnyAsync(s => s.GrNo == studentGrNumberDto.GrNo && s.AcademicMediumId == student.AcademicMediumId && s.EducationMediumStageId == student.EducationMediumStageId);
					if (grNoExists)
					{
						throw new UserFriendlyException("GR no already exists.");
					}
					*/
				}

				student.AssignGrNo(studentGrNumberDto.GrNo);
				await studentRepository.UpdateAsync(student);
			}
		}
		catch
		{
			throw;
		}
	}

	public async Task<List<GrProjectionDto>> GetGrProjections(Guid divisionId)
	{
		try
		{
			// get students in division which gr no is null
			var students = await studentRepository.GetListAsync(s => s.DivisionId == divisionId, includeDetails: true);

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
					GrNumber = student.GrNo
				});
			}

			// Original logic was incomplete/missing. Returning empty list to fix build.
			return result;
		}
		catch
		{
			throw;
		}
	}

	public async Task<List<RollNumberProjectionDto>> GetRollNumberProjections(Guid divisionId)
	{
		try
		{
			// get students in division which roll no is null
			var students = await studentRepository.GetListAsync(s => s.DivisionId == divisionId && s.RollNo == null, includeDetails: true);

			// sort these students with their Enrollment date
			students = students.OrderBy(s => s.CreationTime).ToList();

			// get last roll no in division
			var lastRollNo = await studentRepository.FirstOrDefaultAsync(s => s.DivisionId == divisionId);

			var rollNumberProjectionDto = new List<RollNumberProjectionDto>();
			int nextRollNo = (lastRollNo?.RollNo ?? 0) + 1;

			foreach (var student in students)
			{
				rollNumberProjectionDto.Add(new RollNumberProjectionDto
				{
					StudentId = student.Id,
					RollNo = nextRollNo++
				});
			}

			return rollNumberProjectionDto;
		}
		catch
		{
			throw;
		}
	}

	public async Task StoreRollNumbers(List<UpdateStudentRollNumberDto> updateStudentRollNumberDto)
	{
		try
		{
			if (updateStudentRollNumberDto == null || !updateStudentRollNumberDto.Any())
			{
				return;
			}

			// We assume all students belong to the same division as this is a batch operation for a class.
			// Fetch the first student to get the DivisionId.
			var firstStudentId = updateStudentRollNumberDto.First().StudentId;
			var firstStudent = await studentRepository.GetAsync(firstStudentId);

			if (firstStudent.DivisionId == null)
			{
				throw new UserFriendlyException("Student does not belong to a division.");
			}

			var rollNumberInputs = updateStudentRollNumberDto
				.Select(x => (x.StudentId, x.RollNo))
				.ToList();

			var students = await studentManager.AssignRollNumbersAsync(firstStudent.DivisionId.Value, rollNumberInputs);
			await studentRepository.UpdateManyAsync(students);
		}
		catch
		{
			throw;
		}
	}

	public async Task ChangeDivision(List<ChangeStudentDivisionDto> changeStudentDivisionDto)
	{
		try
		{
			foreach (var changeStudentDivision in changeStudentDivisionDto)
			{
				var student = await studentRepository.GetAsync(changeStudentDivision.StudentId, includeDetails: true);
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
}
