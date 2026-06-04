using System;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class EmployeeOrganizationAssignment : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid EducationMediumStageId { get; private set; }

	private EmployeeOrganizationAssignment()
	{
	}

	public EmployeeOrganizationAssignment(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		Guid educationMediumStageId)
		: base(id)
	{
		TenantId = tenantId;
		EmployeeId = Check.NotNull(employeeId, nameof(employeeId));
		EducationMediumStageId = Check.NotNull(educationMediumStageId, nameof(educationMediumStageId));
	}

	public virtual Employee? Employee { get; private set; }
	public virtual EducationMediumStage? EducationMediumStage { get; private set; }
}
