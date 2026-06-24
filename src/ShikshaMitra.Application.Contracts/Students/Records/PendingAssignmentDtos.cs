using System;
using System.Collections.Generic;
using ShikshaMitra.Students;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Records;

public class GetPendingAssignmentStudentsInput : PagedResultRequestDto
{
	public GetPendingAssignmentStudentsInput()
	{
		MaxResultCount = 500;
	}

	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public DateTimeOffset? AdmissionDateFrom { get; set; }
	public DateTimeOffset? AdmissionDateTo { get; set; }
}

public class PendingAssignmentResponseDto
{
	public int TotalCount { get; set; }
	public IReadOnlyList<PendingAssignmentStudentDto> Items { get; set; } = [];
}

public class PendingAssignmentStudentDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public GenderEnum? Gender { get; set; }
	public string? ContactNo { get; set; }
	public string? Address { get; set; }
	public string? Remarks { get; set; }
	public string? ApplicationNo { get; set; }
	public string? AdmissionNo { get; set; }
	public DateTimeOffset AdmissionDate { get; set; }
	public string? FormNo { get; set; }
	public int? GrNo { get; set; }
	public int? RollNo { get; set; }
	public string? AcademicYearName { get; set; }
	public string? AcademicMediumName { get; set; }
	public string? EducationMediumStageName { get; set; }
	public string? StandardName { get; set; }
	public string? DivisionName { get; set; }
}
