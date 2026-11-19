using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;

namespace ShikshaMitra.Students.Records;

public partial class StudentDirectoryQueryService
{
	private async Task<IQueryable<StudentDirectoryFilterRow>> GetColumnFilterRowsAsync(IQueryable<Student> students, StudentDirectoryRequest input)
	{
		var rows = students.Select(student => new StudentDirectoryFilterRow
		{
			StudentId = student.Id,
			Name = ((student.FirstName ?? "") + (student.MiddleName != null && student.MiddleName != "" ? " " + student.MiddleName : "") +
				(student.LastName != null && student.LastName != "" ? " " + student.LastName : "")).Trim(),
			GrNumber = student.GrNumber != null && student.GrNumber != "" ? student.GrNumber : student.GrNo.HasValue ? student.GrNo.Value.ToString() : "",
			ClassDivision = (student.Standard != null ? student.Standard.Name : "") +
				(student.Division != null ? (student.Standard != null ? " / " : "") + student.Division.Name : ""),
			RollNo = student.RollNo,
			Status = student.Status
		});
		if (!input.AcademicYearId.HasValue || input.IncludeActiveStudentList && input.Population == "Active" ||
			!input.ColumnFilters.Any(filter => filter.Column is "classDivision" or "rollNo")) return rows;

		// Use the same scoped selected-year enrollment projection as the displayed row, including
		// former students whose current placement/roll differs from the selected academic year.
		var placements = (await enrollmentScope.GetDirectoryProjectionAsync()).Where(p => p.Enrollment.AcademicYearId == input.AcademicYearId);
		var divisionQuery = (await divisions.GetQueryableAsync()).Where(division => division.TenantId == CurrentTenant.Id);
		var enrolled = from row in rows
			join placement in placements on row.StudentId equals placement.Enrollment.StudentId
			join division in divisionQuery on placement.Enrollment.DivisionId equals (System.Guid?)division.Id into divisionRows
			from division in divisionRows.DefaultIfEmpty()
			select new StudentDirectoryFilterRow
			{
				StudentId = row.StudentId, Name = row.Name, GrNumber = row.GrNumber, Status = row.Status,
				ClassDivision = placement.StandardName + (division != null ? " / " + division.Name : ""),
				RollNo = placement.Enrollment.RollNumber
			};
		var fallback = rows.Where(row => !placements.Any(placement => placement.Enrollment.StudentId == row.StudentId));
		return enrolled.Concat(fallback);
	}
}
