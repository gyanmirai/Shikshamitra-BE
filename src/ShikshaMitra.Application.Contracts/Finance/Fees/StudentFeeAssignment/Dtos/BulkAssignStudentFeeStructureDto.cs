using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class BulkAssignStudentFeeStructureDto
{
	[Required, MinLength(1)]
	public List<Guid> StudentIds { get; set; } = new();

	[Required]
	public Guid StandardFeeStructureId { get; set; }

	public List<Guid> SelectedOptionalFeeHeadIds { get; set; } = new();

	public List<StudentFeeHeadDetailDto> FeeHeadDetails { get; set; } = new();
}
