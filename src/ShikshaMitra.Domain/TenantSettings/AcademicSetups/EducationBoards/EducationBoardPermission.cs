using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

public class EducationBoardPermission : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? UserId { get; private set; }
	public Guid EducationBoardId { get; private set; }
	
	private EducationBoardPermission()
	{
		/* This constructor is for deserialization / ORM purpose */
	}
	
	internal EducationBoardPermission(
		Guid id,
		Guid? tenantId,
		Guid? userId,
		Guid educationBoardId)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		UserId = Check.NotNull(userId, nameof(userId));
		EducationBoardId = Check.NotNull(educationBoardId, nameof(educationBoardId));
	}
}