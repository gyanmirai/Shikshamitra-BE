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

			//case StudentEnrollmentReportType.AgewiseSummary:
			//	return new AgeWiseSummary(enrollments);

			default:
				throw new NotSupportedException($"Report type '{reportType}' is not supported.");
		}
	}

	public static IDocument CreateStudentReport(IReadOnlyList<Student> enrollments, StudentReportType reportType)
	{
		switch (reportType)
		{
			case StudentReportType.ContactList:
				return new StudentContactList(enrollments);
			case StudentReportType.AdmissionCategoryWiseList:
				return new AdmissionCategoryWiseList(enrollments);
			case StudentReportType.AgeWiseList:
				return new AgewiseList(enrollments);
			case StudentReportType.AgeWiseSummary:
				return new AgeWiseSummary(enrollments);
			case StudentReportType.AllBirthDayWiseList:
				return new AllBirthdayWhishList(enrollments);
			case StudentReportType.BirthDayReport:
				return new BirthdayReport(enrollments);
			case StudentReportType.CastCategoryWiseSummary:
				return new CasteCategoryWiseSummary(enrollments);
			case StudentReportType.FessCategoryWiseSummary:
				return new FeesCategoryWiseSummary(enrollments);
			case StudentReportType.GenderWiseList:
				return new GenderWiseList(enrollments);
			case StudentReportType.GenderWiseStudentStrengthSummary:
				return new GenderwiseStudentStrengthSummary(enrollments);
			case StudentReportType.LeftStudentList:
				return new LeftStudentsList(enrollments);
			case StudentReportType.NewStudentAdmissionReport:
				return new NewStudentAdmissionReport(enrollments[0]);
			case StudentReportType.ReligionWiseList:
				return new ReligionwiseList(enrollments);
			case StudentReportType.ReligionWiseSummary:
				return new ReligionwiseSummary(enrollments);
			case StudentReportType.StudentDeatilInfoarmtion:
				return new StudentDetailInformation(enrollments);
			case StudentReportType.StudentDetailList:
				return new StudentDetailList(enrollments);
			case StudentReportType.StudentList:
				return new StudentList(enrollments);
			case StudentReportType.DivisionWiseStudentList:
				return new StudetListDivisionnWise(enrollments);
			case StudentReportType.StudentProfileReport:
				return new StudentProfileReport(enrollments[0]);
			case StudentReportType.StudentRegister:
				return new StudentRegister(enrollments);
			case StudentReportType.StudentStrengthDivisionWiseSummary:
				return new StudentStrengthDivisionWiseSummary(enrollments);

			default:
				throw new NotSupportedException($"Report type '{reportType}' is not supported.");
		}
	}
}