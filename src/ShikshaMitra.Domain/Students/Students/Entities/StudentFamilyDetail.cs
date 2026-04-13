using System;
using System.Collections.Generic;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

/// <summary>
/// Entity representing family details of a student (1:1 relationship with Student aggregate)
/// Stores information about father, mother, guardian, and sibling students
/// </summary>
public class StudentFamilyDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	// Father Details
	public ParentGuardianDetails? FatherDetails { get; private set; }

	// Mother Details
	public ParentGuardianDetails? MotherDetails { get; private set; }

	// Guardian Details (if different from parents)
	public ParentGuardianDetails? GuardianDetails { get; private set; }

	// Sibling Details - stores student IDs of siblings studying in the same school
	public List<Guid> SiblingStudentIds { get; private set; }

	private StudentFamilyDetail()
	{
		/* This constructor is for deserialization / ORM purpose */
		SiblingStudentIds = new List<Guid>();
	}

	public StudentFamilyDetail(
		Guid id,
		Guid? tenantId)
		: base(id)
	{
		TenantId = tenantId;
		SiblingStudentIds = new List<Guid>();
	}

	/// <summary>
	/// Updates father details
	/// </summary>
	public void UpdateFatherDetails(ParentGuardianDetails? fatherDetails)
	{
		FatherDetails = fatherDetails;
	}

	/// <summary>
	/// Updates mother details
	/// </summary>
	public void UpdateMotherDetails(ParentGuardianDetails? motherDetails)
	{
		MotherDetails = motherDetails;
	}

	/// <summary>
	/// Updates guardian details
	/// </summary>
	public void UpdateGuardianDetails(ParentGuardianDetails? guardianDetails)
	{
		GuardianDetails = guardianDetails;
	}

	/// <summary>
	/// Updates sibling student IDs
	/// </summary>
	public void UpdateSiblingStudentIds(List<Guid> siblingStudentIds)
	{
		SiblingStudentIds = siblingStudentIds ?? new List<Guid>();
	}

	/// <summary>
	/// Updates all family information at once
	/// </summary>
	public void UpdateFamilyInformation(
		ParentGuardianDetails? fatherDetails,
		ParentGuardianDetails? motherDetails,
		ParentGuardianDetails? guardianDetails,
		List<Guid>? siblingStudentIds)
	{
		FatherDetails = fatherDetails;
		MotherDetails = motherDetails;
		GuardianDetails = guardianDetails;
		SiblingStudentIds = siblingStudentIds ?? new List<Guid>();
	}
}
