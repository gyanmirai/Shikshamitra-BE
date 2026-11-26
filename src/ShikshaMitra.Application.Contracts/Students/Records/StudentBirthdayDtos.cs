using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentBirthdayAppService : IApplicationService
{
	Task<StudentBirthdayPageDto> GetListAsync(StudentBirthdayQueryDto input);
	Task<StudentDirectoryOptionsDto> GetOptionsAsync(Guid academicYearId);
}

public class StudentBirthdayQueryDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public string Period { get; set; } = "today";
	public string? StartDate { get; set; }
	public string? EndDate { get; set; }
	public string? Search { get; set; }
	public int SkipCount { get; set; }
	public int MaxResultCount { get; set; } = 25;
}

public class StudentBirthdayPageDto
{
	public DateOnly SchoolDate { get; set; }
	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }
	public List<StudentBirthdayOccurrenceDto> Items { get; set; } = [];
	public long TotalCount { get; set; }
}

public class StudentBirthdayOccurrenceDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = "";
	public string? StandardName { get; set; }
	public string? DivisionName { get; set; }
	public DateOnly OccurrenceDate { get; set; }
	public bool CanOpenProfile { get; set; }
}
