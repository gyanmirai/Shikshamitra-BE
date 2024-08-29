using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class ParentDetails : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	public string Name { get; private set; }
	public DateTime? DateOfBirth { get; private set; }
	public string Occupation { get; private set; }
	public string Qualification { get; private set; }
	public string ExStudent { get; private set; }
	public int? PassOutYear { get; private set; }
	public string Organization { get; private set; }
	public string Designation { get; private set; }
	public string OfficeAddress { get; private set; }
	public string ContactNumber { get; private set; }
	public string OfficeContactNumber { get; private set; }
	public string Email { get; private set; }
	public string AadharNo { get; private set; }
	public decimal? AnnualIncome { get; private set; }

	// Constructor for ORM
	private ParentDetails() { }

	internal ParentDetails(
		Guid id,
		Guid? tenantId,
		string name,
		DateTime? dateOfBirth,
		string occupation,
		string qualification,
		string exStudent,
		int? passOutYear,
		string organization,
		string designation,
		string officeAddress,
		string contactNumber,
		string officeContactNumber,
		string email,
		string aadharNo,
		decimal? annualIncome) : base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		Name = name;
		DateOfBirth = dateOfBirth;
		Occupation = occupation;
		Qualification = qualification;
		ExStudent = exStudent;
		PassOutYear = passOutYear;
		Organization = organization;
		Designation = designation;
		OfficeAddress = officeAddress;
		ContactNumber = contactNumber;
		OfficeContactNumber = officeContactNumber;
		Email = email;
		AadharNo = aadharNo;
		AnnualIncome = annualIncome;
	}


}