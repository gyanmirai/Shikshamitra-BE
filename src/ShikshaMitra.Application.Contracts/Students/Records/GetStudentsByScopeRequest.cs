using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Records;

public class GetStudentsByScopeRequest
{
	[Required]
	public Guid AcademicYearId { get; set; }

	[Required]
	public Guid BoardId { get; set; }

	[Required]
	public Guid EducationMediumId { get; set; }

	[Required]
	public List<Guid> ClassIds { get; set; } = [];

	public List<Guid>? DivisionIds { get; set; }

	/// <summary>
	/// Limits results to students with a fee-structure assignment for <see cref="AcademicYearId"/>.
	/// </summary>
	public bool OnlyWithFeeStructureAssignment { get; set; }
}
