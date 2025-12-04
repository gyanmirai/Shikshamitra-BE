using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentParentDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public string? FullName { get; private set; }
	public DateTimeOffset DateOfBirth { get; private set; }
	public string? Occupation { get; private set; }
	public string? PrimaryContactNo { get; private set; }
	public string? SecondaryContactNo { get; private set; }
	public string? Qualification { get; private set; }
	public int? YearOfPassing { get; private set; }
	public string? Designation { get; private set; }
	public string? Organization { get; private set; }
	public string? AdharNo { get; private set; }
	public ParentTypeEnum ParentType { get; private set; }
	public string? Address1 { get; private set; }
	public string? Address2 { get; private set; }
	public string? Address3 { get; private set; }
	public string? OfficeAddress1 { get; private set; }
	public string? OfficeAddress2 { get; private set; }
	public string? OfficeAddress3 { get; private set; }
}
