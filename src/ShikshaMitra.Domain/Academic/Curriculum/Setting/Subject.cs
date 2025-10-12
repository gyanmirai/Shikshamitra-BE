using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum.Setting;

public class Subject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Properties
	public Guid? TenantId { get; private set; }
	[Required]
	public string Name { get; set; }
	[Required]
	public string Color { get; set; }
	public Guid EducationMediumId { get; set; }
	public EducationMedium EducationMedium { get; set; }

	#endregion

	#region Constructors
	private Subject()
	{

	}

	public Subject(Guid id, Guid? tenantId, string name, string color, Guid educationMediumId)
		: base(id)
	{
		TenantId = tenantId;
		SetName(name);
		SetColor(color);
		EducationMediumId = educationMediumId;
	}

	#endregion

	#region Methods
	public void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		Name = name;
	}

	public void SetColor(string color) => Color = color;
	#endregion
}
