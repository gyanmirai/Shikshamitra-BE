using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class AssignStudentFeeStructureDto
{
    public Guid AcademicYearId { get; set; }
    public string? PreviewToken { get; set; }
    public string? OverrideReason { get; set; }
    public List<AssignmentServicePeriodDto> ServicePeriods { get; set; } = [];

	[Required]
	public Guid StudentId { get; set; }

	[Required]
	public Guid StandardFeeStructureId { get; set; }

	public List<Guid> SelectedOptionalFeeHeadIds { get; set; } = new List<Guid>();

	public List<StudentFeeHeadDetailDto> FeeHeadDetails { get; set; } = new();

	public DateTime? ChargeStartDate { get; set; }
	public DateTime? ChargeEndDate { get; set; }
	public string ChargePeriodSource { get; set; } = "AcademicYear";
	public Guid? ChargePeriodSourceReferenceId { get; set; }
	public string? ChargePeriodOverrideReason { get; set; }
}

