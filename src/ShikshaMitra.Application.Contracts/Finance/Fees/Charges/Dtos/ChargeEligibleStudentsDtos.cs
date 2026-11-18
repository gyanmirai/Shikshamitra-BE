using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Records;

namespace ShikshaMitra.Finance.Fees.Charges.Dtos;

public class GetChargeEligibleStudentsRequest
{
	[Required] public Guid AcademicYearId { get; set; }
	[Required] public Guid BoardId { get; set; }
	[Required] public Guid EducationMediumId { get; set; }
	[Required, MinLength(1)] public List<Guid> ClassIds { get; set; } = [];
	public List<Guid>? DivisionIds { get; set; }
	public Guid? ChargeCategoryId { get; set; }
	public Guid? DivisionFilterId { get; set; }
	public string? Search { get; set; }
	public GenderEnum? Gender { get; set; }
	[Range(0, int.MaxValue)] public int? RollNoFrom { get; set; }
	[Range(0, int.MaxValue)] public int? RollNoTo { get; set; }
	public bool? Charged { get; set; }
	[Range(1, int.MaxValue)] public int Page { get; set; } = 1;
	[Range(1, 100)] public int PageSize { get; set; } = 25;
}

public class ChargeEligibleStudentsResponse
{
	public List<StudentLookupDto> Items { get; set; } = [];
	public List<Guid> CandidateStudentIds { get; set; } = [];
	public List<Guid> MatchingStudentIds { get; set; } = [];
	public Dictionary<Guid, string> MatchingStudentNames { get; set; } = [];
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
}
