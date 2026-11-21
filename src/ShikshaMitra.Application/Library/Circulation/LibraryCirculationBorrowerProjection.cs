using System;
using System.Linq.Expressions;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;

namespace ShikshaMitra.Library.Circulation;

internal sealed record CirculationStudentRow(Guid Id, string? FirstName, string? MiddleName, string? LastName,
	Guid? AcademicYearId, Guid? StandardId, Guid? DivisionId, StudentLifeCycleStatusEnum Status, bool IsLibraryMember);

internal sealed record CirculationEmployeeRow(Guid Id, string? FirstName, string? MiddleName, string? LastName,
	string? EmployeeCode, EmployeeStatusType? EmployeeStatusType, bool IsLibraryMember);

internal static class LibraryCirculationBorrowerProjection
{
	public static readonly Expression<Func<Student, CirculationStudentRow>> Student = row => new(
		row.Id, row.FirstName, row.MiddleName, row.LastName, row.AcademicYearId, row.StandardId, row.DivisionId,
		row.Status, row.AdditionalDetail != null && row.AdditionalDetail.IsLibraryMember == true);

	public static readonly Expression<Func<Employee, CirculationEmployeeRow>> Employee = row => new(
		row.Id, row.FirstName, row.MiddleName, row.LastName, row.EmployeeCode, row.EmployeeStatusType,
		row.AdditionalDetails != null && row.AdditionalDetails.LibraryMember == true);
}
