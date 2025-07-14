using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentParentDetail : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid StudentId { get; set; }
	public string? FullName { get; set; }
	public DateTimeOffset DateOfBirth { get; set; }
	public string? Occupation { get; set; }
	public string? PrimaryContactNo { get; set; }
	public string? SecondaryContactNo { get; set; }
	public string? Qualification { get; set; }
	public int? YearOfPassing { get; set; }
	public string? Designation { get; set; }
	public string? Organization { get; set; }
	public string? AdharNo { get; set; }
	public ParentTypeEnum ParentType { get; set; }
	public string? Address1 { get; set; }
	public string? Address2 { get; set; }
	public string? Address3 { get; set; }
	public string? OfficeAddress1 { get; set; }
	public string? OfficeAddress2 { get; set; }
	public string? OfficeAddress3 { get; set; }
}
