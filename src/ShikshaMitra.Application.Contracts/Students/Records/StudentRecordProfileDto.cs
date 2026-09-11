using System;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Records;

public class StudentRecordProfileDto : EntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public int? GrNo { get; set; }
	public string? GrNumber { get; set; }
	public int? RollNo { get; set; }
	public Guid? StandardId { get; set; }
	public string? StandardName { get; set; }
	public Guid? DivisionId { get; set; }
	public string? DivisionName { get; set; }
	public Guid AcademicMediumId { get; set; }
	public string? AcademicMediumName { get; set; }
	public StudentLifeCycleStatusEnum Status { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public GenderEnum? Gender { get; set; }
}
