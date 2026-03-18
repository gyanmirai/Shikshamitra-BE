using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Entity for employee additional details.
/// Stores supplementary information like BPL card, disability status, language preferences, and memberships.
/// Has a 1:1 relationship with Employee.
/// </summary>
public class EmployeeAdditionalDetails : FullAuditedEntity<Guid>, IMultiTenant
{
	/// <summary>
	/// Tenant ID for multi-tenancy support.
	/// </summary>
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Foreign key to the Employee entity.
	/// </summary>
	public Guid EmployeeId { get; private set; }

	/// <summary>
	/// Below Poverty Line card number.
	/// </summary>
	public string? BplCardNo { get; private set; }

	/// <summary>
	/// Indicates if the employee is a person with disability.
	/// </summary>
	public bool PersonWithDisability { get; private set; }

	/// <summary>
	/// Second language known by employee.
	/// </summary>
	public string? SecondLanguage { get; private set; }

	/// <summary>
	/// Library membership status.
	/// </summary>
	public bool LibraryMember { get; private set; }

	/// <summary>
	/// Transport facility membership status.
	/// </summary>
	public bool TransportMember { get; private set; }

	/// <summary>
	/// Navigation property to the Employee.
	/// </summary>
	public virtual Employee? Employee { get; private set; }

	// For ORM
	private EmployeeAdditionalDetails()
	{
	}

	/// <summary>
	/// Creates a new EmployeeAdditionalDetails entity.
	/// </summary>
	public EmployeeAdditionalDetails(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		string? bplCardNo,
		bool personWithDisability,
		string? secondLanguage,
		bool libraryMember,
		bool transportMember)
		: base(id)
	{
		TenantId = tenantId;
		EmployeeId = employeeId;
		BplCardNo = bplCardNo?.Trim();
		PersonWithDisability = personWithDisability;
		SecondLanguage = secondLanguage?.Trim();
		LibraryMember = libraryMember;
		TransportMember = transportMember;
	}

	/// <summary>
	/// Updates the additional details.
	/// </summary>
	public void Update(
		string? bplCardNo,
		bool personWithDisability,
		string? secondLanguage,
		bool libraryMember,
		bool transportMember)
	{
		BplCardNo = bplCardNo?.Trim();
		PersonWithDisability = personWithDisability;
		SecondLanguage = secondLanguage?.Trim();
		LibraryMember = libraryMember;
		TransportMember = transportMember;
	}

	/// <summary>
	/// Partially updates the additional details. Only non-null values are applied.
	/// </summary>
	public void Patch(
		string? bplCardNo,
		bool? personWithDisability,
		string? secondLanguage,
		bool? libraryMember,
		bool? transportMember,
		bool bplCardNoProvided = false)
	{
		// For nullable string fields, we need an explicit flag to know if null was intentionally passed
		if (bplCardNoProvided)
		{
			BplCardNo = bplCardNo?.Trim();
		}

		if (personWithDisability.HasValue)
		{
			PersonWithDisability = personWithDisability.Value;
		}

		if (secondLanguage != null)
		{
			SecondLanguage = secondLanguage.Trim();
		}

		if (libraryMember.HasValue)
		{
			LibraryMember = libraryMember.Value;
		}

		if (transportMember.HasValue)
		{
			TransportMember = transportMember.Value;
		}
	}
}
