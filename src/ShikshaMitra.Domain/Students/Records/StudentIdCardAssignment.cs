using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentIdCardAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid TemplateId { get; private set; }

	protected StudentIdCardAssignment() { }

	public StudentIdCardAssignment(Guid id, Guid tenantId, Guid academicYearId, Guid standardId, Guid templateId) : base(id)
	{
		TenantId = Check.NotDefaultOrNull<Guid>(tenantId, nameof(tenantId));
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		StandardId = Check.NotDefaultOrNull<Guid>(standardId, nameof(standardId));
		Assign(templateId);
	}

	public void Assign(Guid templateId) => TemplateId = Check.NotDefaultOrNull<Guid>(templateId, nameof(templateId));
}
