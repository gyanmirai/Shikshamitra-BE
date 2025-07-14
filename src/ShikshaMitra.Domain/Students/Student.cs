using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class Student : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string? FirstName { get; private set; }
	public string? MiddleName { get; private set; }
	public string? LastName { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid? StandardId { get; private set; }
	public Guid? DivisionId { get; private set; }
	public int? GrNo { get; private set; }
	public int? RollNo { get; private set; }
	public StudentLifeCycleStatusEnum Status { get; private set; }
}
