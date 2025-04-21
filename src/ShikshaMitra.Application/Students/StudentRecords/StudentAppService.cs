using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords.Division;
using ShikshaMitra.Students.StudentRecords.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.Students.StudentRecords.BulkUpload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentAppService(
	StudentManager studentManager,
	IStudentRepository studentRepository, 
	IEducationBoardRepository educationBoardRepository,
	IStudentCsvImportService studentCsvImportService) :
	CrudAppService<Student, StudentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDto>(studentRepository),
	IStudentAppService
{

	public async Task<PagedResultDto<StudentDto>> GetFilteredListAsync(PagedAndSortedResultRequestDto input, Guid? educationMediumStageId, Guid? standardId, Guid? divisionId)
	{
		// Create the base query from the repository
		var query = await Repository.GetQueryableAsync();

		// Apply additional filters
		if (educationMediumStageId.HasValue)
		{
			query = query.Where(student => student.EducationMediumStageId == educationMediumStageId);
		}

		if (standardId.HasValue)
		{
			query = query.Where(student => student.StandardId == standardId);
		}

		if (divisionId.HasValue)
		{
			query = query.Where(student => student.DivisionId == divisionId);
		}

		// Get the total count for pagination
		var totalCount = await AsyncExecuter.CountAsync(query);

		// Apply sorting and paging
		query = ApplySorting(query, input);
		query = ApplyPaging(query, input);

		// Execute the query to get the students
		var entities = await AsyncExecuter.ToListAsync(query);

		// Map the entities to DTOs
		var entityDtos = await MapToGetListOutputDtosAsync(entities);

		return new PagedResultDto<StudentDto>(totalCount, entityDtos);
	}
	// Get GR No

	public async Task<int> GetGrNoAsync(Guid educationBoardId, Guid educationMediumId, Guid educationMediumStageId)
	{
		EducationBoard? educationBoard = await educationBoardRepository.GetAsync(educationBoardId);

		if (educationBoard.Type == EducationBoardType.GSEB)
		{
			Student? student = await studentRepository.GetLastStudentbyGrNoForGSEBAsync(educationBoardId, educationMediumId, educationMediumStageId);

			return student?.GrNoRecord.HasValue == true ? student.GrNoRecord.Value + 1 : 1;
		}
		else if (educationBoard.Type == EducationBoardType.CBSE)
		{
			Student? student = await studentRepository.GetLastStudentbyGrNoForCBSEAsync(educationBoardId);

			return student?.GrNoRecord.HasValue == true ? student.GrNoRecord.Value + 1 : 1;
		}
		else
		{
			// if education board is not found then throw an exception
			throw new UserFriendlyException("Education Board not found");
		}
	}

	// Get Student Roll No
	public async Task<int> GetRollNoAsync(Guid divisionId)
	{
		Student? student = await studentRepository.GetLastStudentbyRollNoAsync(divisionId);
		return (student?.RollNo.HasValue == true) ? student.RollNo.Value + 1 : 1;
	}

	public async Task UpdateGrNumbersAsync(CreateUpdateGRNoDto input)
	{
		if (input.StudentNumbers == null || input.StudentNumbers.Count == 0)
		{
			throw new UserFriendlyException("No student data provided.");
		}

		foreach (var item in input.StudentNumbers)
		{
			// Validate using the domain service.
			await studentManager.ValidateGrNumberAsync(item.StudentId, item.GrNo);

			// After validation, fetch and update the student.
			var student = await studentRepository.GetAsync(item.StudentId);
			student.GrNoRecord = item.GrNo;
			await studentRepository.UpdateAsync(student);
		}
	}

	public async Task UpdateRollNumbersAsync(CreateUpdateRollNoDto input)
	{
		if (input.StudentNumbers == null || input.StudentNumbers.Count == 0)
		{
			throw new UserFriendlyException("No student data provided.");
		}
		foreach (var item in input.StudentNumbers)
		{
			// Validate using the domain service.
			await studentManager.ValidateRollNumberAsync(item.StudentId, item.RollNo);
			// After validation, fetch and update the student.
			var student = await studentRepository.GetAsync(item.StudentId);
			student.RollNo = item.RollNo;
			await studentRepository.UpdateAsync(student);
		}
	}


	public async Task UpdateStudentStandardAsync(UpdateStudentStandardDto input)
	{
		if (input.StudentStandardUpdates == null || input.StudentStandardUpdates.Count == 0)
		{
			throw new UserFriendlyException("No student data provided.");
		}
		foreach (var update in input.StudentStandardUpdates)
		{
			// Fetch the student by its ID.
			var student = await studentRepository.GetAsync(update.StudentId);
			student.StandardId = update.StandardId;
			student.DivisionId = null;
			await studentRepository.UpdateAsync(student);
		}
	}

	public async Task UpdateStudentDivisionAsync(UpdateStudentDivisionDto input)
	{
		if (input.StudentDivisionUpdates == null || input.StudentDivisionUpdates.Count == 0)
		{
			throw new UserFriendlyException("No student data provided.");
		}

		foreach (var update in input.StudentDivisionUpdates)
		{
			// Fetch the student by its ID.
			var student = await studentRepository.GetAsync(update.StudentId);

			// Update the student's division and reset the roll number.
			student.DivisionId = update.DivisionId;
			student.RollNo = null;

			await studentRepository.UpdateAsync(student);
		}
	}

	public async Task<CustomFileResult> GenerateReportAsync(StudentReportType reportType)
	{
		List<Student>? enrollments = await studentRepository.GetListAsync(includeDetails: true);

		IDocument? reportDocument = StudentManager.CreateStudentReport(enrollments, reportType);

		var pdfBytes = await Task.Run(() =>
		{
			using MemoryStream ms = new MemoryStream();
			reportDocument.GeneratePdf(ms);
			return ms.ToArray();
		});

		// Build a file name based on the report type and current timestamp.
		string fileName = $"StudentReport_{reportType}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

		return new CustomFileResult
		(
			pdfBytes,
			"application/pdf",
			fileName
		);
	}

	public async Task<StudentDto> UpdateStudentStatusAsync(Guid studentId, StudentStatus newStatus)
	{
		var student = await studentRepository.GetAsync(studentId);
		
		student.UpdateStatus(newStatus);
		
		await studentRepository.UpdateAsync(student);
		
		return ObjectMapper.Map<Student, StudentDto>(student);
	}

	[RemoteService(false)]
	public async Task<CustomFileResult> GenerateCertificate(Guid studentId, StudentCertificateType certificateType)
	{
		Student? student = await studentRepository.GetAsync(studentId, includeDetails: true);
		if (student == null)
		{
			throw new UserFriendlyException($"Student not found: {studentId}");
		}

		IDocument? certificateDocument = StudentManager.CreateCertificate(student, certificateType);
		var pdfBytes = await Task.Run(() =>
		{
			using MemoryStream ms = new MemoryStream();
			certificateDocument.GeneratePdf(ms);
			return ms.ToArray();
		});

		string fileName = $"StudentCertificate_{studentId}_{certificateType}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

		return new CustomFileResult
		(
			pdfBytes,
			"application/pdf",
			fileName
		);
	}

	[RemoteService(false)]
	public async Task<StudentCsvUploadResultDto> ImportStudentsFromCsvAsync(Stream fileStream, string fileName)
	{
		return await studentCsvImportService.ImportStudentsFromCsvAsync(fileStream);
	}
}
