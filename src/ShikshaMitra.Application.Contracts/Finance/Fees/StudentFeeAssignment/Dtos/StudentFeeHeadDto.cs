using System;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class StudentFeeHeadDto
{
	public Guid FeeHeadId { get; set; }

	public string FeeHeadName { get; set; }

	public string CategoryName { get; set; }

	public decimal Amount { get; set; }

	public bool IsOptional { get; set; }

	public bool IsOneTime { get; set; }

	public bool IsSelected { get; set; }
}
