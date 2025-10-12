using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;
public class AssigmentType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }

	public string Name { get; private set; } = default!;

	public Guid EducationMediumId { get; private set; }

	public EducationMedium? EducationMedium { get; set; }

	private AssigmentType() { }

	public AssigmentType(Guid id, Guid? tenantId, string name, Guid educationMediumId)
		: base(id)
	{
		TenantId = tenantId;
		SetName(name);
		EducationMediumId = educationMediumId;
	}

	public void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		Name = name;
	}
}
