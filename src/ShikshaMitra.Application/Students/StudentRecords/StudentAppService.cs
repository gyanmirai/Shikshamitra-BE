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
using System.Linq.Expressions;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Common.Excel;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentAppService(
	StudentManager studentManager,
	IStudentRepository studentRepository,
	IEducationBoardRepository educationBoardRepository,
	IPdfService pdfService,
    IExcelService excelService,
	IStudentCsvImportService studentCsvImportService) :
	CrudAppService<Student, StudentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDto>(studentRepository),
	IStudentAppService
{
	public async Task<PagedResultDto<StudentDto>> GetFilteredListAsync(
	PagedAndSortedResultRequestDto input,
	Guid? educationMediumStageId,
	Guid? standardId,
	Guid? divisionId,
	bool skipPagination = false)
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

		// Apply sorting
		query = ApplySorting(query, input);

		if (!skipPagination)
		{
			query = ApplyPaging(query, input);
		}

		// Execute the query to get the students
		var entities = await AsyncExecuter.ToListAsync(query);

		// Map the entities to DTOs
		var entityDtos = await MapToGetListOutputDtosAsync(entities);

		// Get student group information
		await EnrichStudentsWithGroupInfoAsync(entityDtos);

		return new PagedResultDto<StudentDto>(totalCount, entityDtos);
	}

	public async Task<CustomFileResult> GetFilteredListReport(
	PagedAndSortedResultRequestDto input,
	Guid? educationMediumStageId,
	Guid? standardId,
	Guid? divisionId,
	ReportFormatType format)
	{
		var studentdtos = await GetFilteredListAsync(input, educationMediumStageId, standardId, divisionId, skipPagination: true);
		var fileName = $"StudentReport_{DateTime.UtcNow:ddMMyyyyHHmmss}";
		if (format == ReportFormatType.Pdf)
		{
			var pdfBytes = await pdfService.GenerateReport(studentdtos.Items, fileName);
			  return new CustomFileResult(
				pdfBytes,
				"application/pdf",
				fileName + ".pdf"
			);
		}
		else
		{
			var excelBytes = excelService.GenerateExcelFromData(studentdtos.Items, "Student Report", "Student Report");
			return new CustomFileResult(
			   excelBytes,
			   "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			   fileName + ".xlsx"
		   );

		}

	}

	/// <summary>
	/// Enriches student DTOs with group membership information
	/// </summary>
	private async Task EnrichStudentsWithGroupInfoAsync(List<StudentDto> students)
	{
		if (students == null || !students.Any())
			return;

		// Get all student IDs
		var studentIds = students.Select(s => s.Id).ToList();

		// Get all student group memberships for these students in a single query
		var studentGroupMemberships = await LazyServiceProvider
			.LazyGetRequiredService<IRepository<StudentGroupMember, Guid>>()
			.GetListAsync(m => studentIds.Contains(m.StudentId));

		// Create a dictionary that maps student IDs to their group membership
		var membershipByStudentId = studentGroupMemberships.ToDictionary(
			m => m.StudentId,
			m => m);

		// Get all group IDs from memberships
		var groupIds = studentGroupMemberships.Select(m => m.StudentGroupId).Distinct().ToList();

		// Get all student groups in a single query
		var studentGroups = groupIds.Any()
			? await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentGroup, Guid>>()
				.GetListAsync(g => groupIds.Contains(g.Id))
			: new List<StudentGroup>();

		// Create a dictionary for quick group lookups
		var groupsDict = studentGroups.ToDictionary(g => g.Id, g => g);

		// Assign group membership to student DTOs
		foreach (var student in students)
		{
			if (membershipByStudentId.TryGetValue(student.Id, out var membership))
			{
				student.GroupMembership = new StudentGroupMembershipInfo
				{
					GroupId = membership.StudentGroupId,
					GroupName = groupsDict.ContainsKey(membership.StudentGroupId)
						? groupsDict[membership.StudentGroupId].Name
						: "Unknown Group",
					IsLeader = membership.IsLeader,
					IsDivisionLeader = membership.IsDivisionLeader
				};
			}
			else
			{
				// Set null or a default empty object to indicate no group membership
				student.GroupMembership = null;
			}
		}
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

	public async Task<CustomFileResult> GenerateReportAsync(StudentReportType reportType, StudentReportDto reportDto)
	{
		Expression<Func<Student, bool>> filter = student => true;

		// Apply filters if specified in the reportDto
		if (reportDto.AcademicYearId.HasValue)
		{
			filter = filter.And(s => s.AcademicYearId == reportDto.AcademicYearId.Value);
		}

		if (reportDto.EducationBoardId.HasValue)
		{
			filter = filter.And(s => s.EducationBoardId == reportDto.EducationBoardId.Value);
		}

		if (reportDto.EducationMediumId.HasValue)
		{
			filter = filter.And(s => s.EducationMediumId == reportDto.EducationMediumId.Value);
		}

		if (reportDto.EducaitonMediumStageId.HasValue)
		{
			filter = filter.And(s => s.EducationMediumStageId == reportDto.EducaitonMediumStageId.Value);
		}

		if (reportDto.StandardId.HasValue)
		{
			filter = filter.And(s => s.StandardId == reportDto.StandardId.Value);
		}

		if (reportDto.DivisionId.HasValue)
		{
			filter = filter.And(s => s.DivisionId == reportDto.DivisionId.Value);
		}

		// Get filtered list of students
		List<Student>? enrollments = await studentRepository.GetListAsync(filter, includeDetails: true);

		IDocument? reportDocument = await studentManager.CreateStudentReport(enrollments, reportType);

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
	public async Task<StudentCsvUploadResultDto> ImportStudentsFromCsvAsync(byte[] csvContent, string fileName)
	{
		using var memoryStream = new MemoryStream(csvContent);
		return await studentCsvImportService.ImportStudentsFromCsvAsync(memoryStream);
	}
}
