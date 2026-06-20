using System;

using System.Collections.Generic;

namespace ShikshaMitra.Students.Records;

public class RollNumberProjectionDto
{
    public Guid StudentId { get; set; }
    public Guid AcademicTermId { get; set; }
    public string StudentName { get; set; } = string.Empty;
	public int? RollNo { get; set; }
	public int? GrNo { get; set; }
}

public class RollNumberProjectionResponseDto
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; } = 1;
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public ArrangementTypeEnum ArrangementType { get; set; }
	public ArrangementByEnum ArrangementBy { get; set; }
	public List<RollNumberProjectionDto> Items { get; set; } = [];
}
