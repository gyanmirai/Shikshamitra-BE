using System;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

/// <summary>
/// Entity representing communication/address details of a student (1:1 relationship with Student aggregate)
/// Stores local, permanent, and emergency address information
/// </summary>
public class StudentCommunicationDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Local/Current address where student currently resides
	/// </summary>
	public AddressDetails? LocalAddress { get; private set; }

	/// <summary>
	/// Permanent address as per official documents
	/// </summary>
	public AddressDetails? PermanentAddress { get; private set; }

	/// <summary>
	/// Emergency contact address
	/// </summary>
	public AddressDetails? EmergencyAddress { get; private set; }

	/// <summary>
	/// Indicates if permanent address is same as local address
	/// </summary>
	public bool IsPermanentSameAsLocal { get; private set; }

	/// <summary>
	/// Indicates if emergency address is same as local address
	/// </summary>
	public bool IsEmergencySameAsLocal { get; private set; }

	/// <summary>
	/// Emergency contact person name
	/// </summary>
	public string? EmergencyContactName { get; private set; }

	/// <summary>
	/// Emergency contact person phone number
	/// </summary>
	public string? EmergencyContactPhone { get; private set; }

	/// <summary>
	/// Relationship with emergency contact person
	/// </summary>
	public string? EmergencyContactRelation { get; private set; }

	private StudentCommunicationDetail()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public StudentCommunicationDetail(
		Guid id,
		Guid? tenantId)
		: base(id)
	{
		TenantId = tenantId;
	}

	/// <summary>
	/// Updates all communication information at once
	/// </summary>
	public void UpdateCommunicationInformation(
		AddressDetails? localAddress,
		AddressDetails? permanentAddress,
		AddressDetails? emergencyAddress,
		bool isPermanentSameAsLocal,
		bool isEmergencySameAsLocal,
		string? emergencyContactName,
		string? emergencyContactPhone,
		string? emergencyContactRelation)
	{
		LocalAddress = localAddress;

		// If permanent address is same as local, copy local address
		PermanentAddress = isPermanentSameAsLocal ? localAddress : permanentAddress;
		IsPermanentSameAsLocal = isPermanentSameAsLocal;

		// If emergency address is same as local, copy local address
		EmergencyAddress = isEmergencySameAsLocal ? localAddress : emergencyAddress;
		IsEmergencySameAsLocal = isEmergencySameAsLocal;

		EmergencyContactName = emergencyContactName;
		EmergencyContactPhone = emergencyContactPhone;
		EmergencyContactRelation = emergencyContactRelation;
	}
}
