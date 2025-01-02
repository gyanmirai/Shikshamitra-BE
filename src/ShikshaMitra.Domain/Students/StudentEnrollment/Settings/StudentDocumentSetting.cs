using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public class StudentDocumentSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public required string DocumentName { get; set; }
	public required bool isRequired { get; set; }
	public List<DocumentType> DocumentTypes { get; set; } = [];
}