using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentContactDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string PrimaryContactNo { get; private set; }
	public string? SecondaryContactNo { get; private set; }
	public string? Email { get; private set; }
	public string? Address1 { get; private set; }
	public string? Address2 { get; private set; }
	public string? Address3 { get; private set; }
	public string? City { get; private set; }
	public string? State { get; private set; }
	public string? Country { get; private set; }
	public string? Pincode { get; private set; }
	public Student? Student { get; private set; }

	protected StudentContactDetail() { }

	public StudentContactDetail(
		Guid id,
		Guid? tenantId,
		string primaryContactNo,
		string? secondaryContactNo = null,
		string? email = null,
		string? address1 = null,
		string? address2 = null,
		string? address3 = null,
		string? city = null,
		string? state = null,
		string? country = null,
		string? pincode = null
	)
		: base(id)
	{
		TenantId = tenantId;
		PrimaryContactNo = primaryContactNo;
		SecondaryContactNo = secondaryContactNo;
		Email = email;
		Address1 = address1;
		Address2 = address2;
		Address3 = address3;
		City = city;
		State = state;
		Country = country;
		Pincode = pincode;
	}

	public void UpdateContactInfo(
	string primaryContactNo,
	string? secondaryContactNo,
	string? email,
	string? address1,
	string? address2,
	string? address3,
	string? city,
	string? state,
	string? country,
	string? pinCode)
	{
		PrimaryContactNo = primaryContactNo;
		SecondaryContactNo = secondaryContactNo;
		Email = email;
		Address1 = address1;
		Address2 = address2;
		Address3 = address3;
		City = city;
		State = state;
		Country = country;
		Pincode = pinCode;
	}
}
