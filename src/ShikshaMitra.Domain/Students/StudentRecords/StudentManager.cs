using QuestPDF.Infrastructure;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords.Reports;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentManager(IStudentRepository studentRepository) : DomainService
{
	public async Task ValidateGrNumberAsync(Guid studentId, int grNo)
	{
		var student = await studentRepository.GetAsync(studentId) ?? throw new UserFriendlyException($"Student not found: {studentId}");

		// Validate that no other student in the same board, medium, and stage has the same GR number.
		bool exists = await studentRepository.AnyAsync(s =>
			s.Id != student.Id &&
			s.GrNoRecord == grNo &&
			s.EducationBoardId == student.EducationBoardId &&
			s.EducationMediumId == student.EducationMediumId &&
			s.EducationMediumStageId == student.EducationMediumStageId);

		if (exists)
		{
			throw new UserFriendlyException($"GR number {grNo} already exists.");
		}
	}

	public async Task ValidateRollNumberAsync(Guid studentId, int rollNo)
	{
		var student = await studentRepository.GetAsync(studentId) ?? throw new UserFriendlyException($"Student not found: {studentId}");

		// Validate that no other student in the same division has the same Roll number.
		bool exists = await studentRepository.AnyAsync(s =>
			s.Id != student.Id &&
			s.RollNo == rollNo &&
			s.DivisionId == student.DivisionId);

		if (exists)
		{
			throw new UserFriendlyException($"Roll number {rollNo} already exists.");
		}
	}

	public static IDocument CreateReport(IReadOnlyList<StudentEnrollment> enrollments, StudentEnrollmentReportType reportType)
	{
		switch (reportType)
		{
			case StudentEnrollmentReportType.EnrollmentByGender:
				return new EnrollmentByGenderReport(enrollments);

			case StudentEnrollmentReportType.EnrollmentByAge:
				return new EnrollmentByAgeReport(enrollments);

			default:
				throw new NotSupportedException($"Report type '{reportType}' is not supported.");
		}
	}

	public static IDocument CreateStudentReport(IReadOnlyList<Student> enrollments, StudentReportType reportType)
	{
		switch (reportType)
		{

			default:
				throw new NotSupportedException($"Report type '{reportType}' is not supported.");
		}
	}
}