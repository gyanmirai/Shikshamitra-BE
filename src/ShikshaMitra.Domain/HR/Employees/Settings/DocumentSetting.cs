using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees.Settings;

public class DocumentSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public required string DocumentName { get; set; }
	public required bool IsRequired { get; set; }
	public List<DocumentType> DocumentTypes { get; set; } = [];
}