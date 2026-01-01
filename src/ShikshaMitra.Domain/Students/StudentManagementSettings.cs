using ShikshaMitra.Students.Records;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class StudentManagementSettings : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid DivisionId { get; set; }
	public ArrangementTypeEnum? ArrangementTye { get; set; }
	public ArrangementByEnum? ArrangementBy { get; set; }
}
