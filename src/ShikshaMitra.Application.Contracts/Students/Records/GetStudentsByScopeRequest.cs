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
	public Guid? ChargeCategoryId { get; set; }
	public string? Search { get; set; }
	public GenderEnum? Gender { get; set; }
	[Range(0, int.MaxValue)] public int? RollNoFrom { get; set; }
	[Range(0, int.MaxValue)] public int? RollNoTo { get; set; }
	public bool? HasFeeStructureAssignment { get; set; }
	/// <summary>Pending or approved charge in the selected category and academic year.</summary>
	public bool? AlreadyCharged { get; set; }
	[Range(0, int.MaxValue)] public int SkipCount { get; set; }
	[Range(1, 1000)] public int? MaxResultCount { get; set; }
}
