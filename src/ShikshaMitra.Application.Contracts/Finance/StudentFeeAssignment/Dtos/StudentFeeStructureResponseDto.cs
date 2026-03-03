using System;
using System.Collections.Generic;

namespace ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment.Dtos;

public class StudentFeeStructureResponseDto
{
	public Guid StudentFeeStructureId { get; set; }

	public Guid? StandardFeeStructureId { get; set; }

	public Guid StudentId { get; set; }

	public Guid StandardId { get; set; }

	public string StructureName { get; set; }

	public List<StudentFeeHeadDto> FeeHeads { get; set; } = new List<StudentFeeHeadDto>();
}
