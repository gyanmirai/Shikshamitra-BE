using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class BulkAssignStudentFeeStructureDto
{
    public Guid AcademicYearId { get; set; }
    public List<AssignStudentFeeStructureDto> Items { get; set; } = [];

	[Required]
	public List<Guid> StudentIds { get; set; } = new();

	[Required]
	public Guid StandardFeeStructureId { get; set; }

	public List<Guid> SelectedOptionalFeeHeadIds { get; set; } = new();

	public List<StudentFeeHeadDetailDto> FeeHeadDetails { get; set; } = new();

	public DateTime? ChargeStartDate { get; set; }
	public DateTime? ChargeEndDate { get; set; }
	public string ChargePeriodSource { get; set; } = "AcademicYear";
	public Guid? ChargePeriodSourceReferenceId { get; set; }
	public string? ChargePeriodOverrideReason { get; set; }
}
