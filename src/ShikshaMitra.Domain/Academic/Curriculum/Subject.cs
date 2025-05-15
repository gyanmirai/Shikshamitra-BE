using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

public class Subject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    #region Properties    
    public Guid? TenantId { get; private set; }
    public string Name { get; set; }
    public string Color { get; set; }

    [ForeignKey(nameof(EducationMedium))]
    public Guid MediumId { get; set; }

    public virtual EducationMedium EducationMedium { get; set; }
    #endregion

    #region Constructors  
    private Subject()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public Subject(
        Guid id,
        Guid? tenantId,
        string name,
        Guid mediumId,
        string color)
        : base(id)
    {
        TenantId = tenantId;
		SetName(name);
		SetMedium(mediumId);
		SetColor(color);
	}
	#endregion

	#region Methods  
	public void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		Name = name;
	}

	public void SetMedium(Guid mediumId)
	{
		MediumId = mediumId;
	}

	public void SetColor(string? color)
	{
		Color = color;
	}
	#endregion
}
