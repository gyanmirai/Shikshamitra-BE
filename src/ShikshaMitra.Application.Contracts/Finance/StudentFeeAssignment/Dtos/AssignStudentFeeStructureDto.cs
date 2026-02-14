using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment.Dtos;

public class AssignStudentFeeStructureDto
{
	[Required]
	public Guid StudentId { get; set; }

	[Required]
	public Guid StandardFeeStructureId { get; set; }

	public List<Guid> SelectedOptionalFeeHeadIds { get; set; } = new List<Guid>();
}
