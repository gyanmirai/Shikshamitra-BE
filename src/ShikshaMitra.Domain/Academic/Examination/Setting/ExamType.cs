using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.Setting;
public class ExamType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	[Required]
	public string Name { get; set; }
	public string? Description { get; private set; }
    public Guid EducationMediumId { get; set; }
	public EducationMedium EducationMedium { get; set; }


	private ExamType() { }

	public ExamType(Guid id, Guid? tenantId, string name, Guid educationMediumId, string? description = null)
		  : base(id)
	{
		TenantId = tenantId;
		SetName(name);
		SetDescription(description);
		EducationMediumId = educationMediumId;
	}

	public void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		Name = name;
	}

	public void SetDescription(string? description)
	{
		Description = description?.Trim();
	}

}
