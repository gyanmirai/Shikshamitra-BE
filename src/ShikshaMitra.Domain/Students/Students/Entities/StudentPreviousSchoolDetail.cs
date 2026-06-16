using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

/// <summary>
/// Entity representing previous school details of a student (1:1 relationship with Student aggregate)
/// Stores information about student's previous academic institution
/// </summary>
public class StudentPreviousSchoolDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Name of the previous school
	/// </summary>
	public string? SchoolName { get; private set; }

	/// <summary>
	/// School address
	/// </summary>
	public string? SchoolAddress { get; private set; }

	/// <summary>
	/// Education board of previous school
	/// </summary>
	public string? Board { get; private set; }

	/// <summary>
	/// Medium of instruction at previous school
	/// </summary>
	public string? Medium { get; private set; }

	/// <summary>
	/// Last class/standard attended
	/// </summary>
	public string? LastClassAttended { get; private set; }

	/// <summary>
	/// Previous school's index number
	/// </summary>
	public string? SchoolIndexNumber { get; private set; }

	/// <summary>
	/// Transfer Certificate (TC) number
	/// </summary>
	public string? TCNumber { get; private set; }

	/// <summary>
	/// Date of leaving previous school
	/// </summary>
	public DateTimeOffset? DateOfLeaving { get; private set; }

	/// <summary>
	/// Percentage/marks obtained in last class
	/// </summary>
	public decimal? PercentageObtained { get; private set; }

	/// <summary>
	/// Academic year of last attendance
	/// </summary>
	public string? AcademicYear { get; private set; }

	/// <summary>
	/// Reason for leaving previous school
	/// </summary>
	public string? ReasonForLeaving { get; private set; }

	/// <summary>
	/// Any additional remarks
	/// </summary>
	public string? Remarks { get; private set; }

	private StudentPreviousSchoolDetail()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentPreviousSchoolDetail(
		Guid id,
		Guid? tenantId)
		: base(id)
	{
		TenantId = tenantId;
	}

	/// <summary>
	/// Updates all previous school details information at once
	/// </summary>
	public void UpdatePreviousSchoolDetails(
		string? schoolName,
		string? schoolAddress,
		string? board,
		string? medium,
		string? lastClassAttended,
		string? schoolIndexNumber,
		string? tcNumber,
		DateTimeOffset? dateOfLeaving,
		decimal? percentageObtained,
		string? academicYear,
		string? reasonForLeaving,
		string? remarks)
	{
		SchoolName = schoolName;
		SchoolAddress = schoolAddress;
		Board = board;
		Medium = medium;
		LastClassAttended = lastClassAttended;
		SchoolIndexNumber = schoolIndexNumber;
		TCNumber = tcNumber;
		DateOfLeaving = dateOfLeaving;
		PercentageObtained = percentageObtained;
		AcademicYear = academicYear;
		ReasonForLeaving = reasonForLeaving;
		Remarks = remarks;
	}
}
