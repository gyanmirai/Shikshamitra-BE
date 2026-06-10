using ShikshaMitra.Finance.Charges;
using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.Charges.Dtos;

public class ChargeAudienceDto
{
	public AcademicScopeDto Scope { get; set; } = new();
	public StudentSelectionDto Selection { get; set; } = new();
}

public class AcademicScopeDto
{
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public List<Guid> ClassIds { get; set; } = [];
	public List<Guid>? DivisionIds { get; set; }
}

public class StudentSelectionDto
{
	public StudentSelectionMode Mode { get; set; }
	public List<Guid>? StudentIds { get; set; }
}
