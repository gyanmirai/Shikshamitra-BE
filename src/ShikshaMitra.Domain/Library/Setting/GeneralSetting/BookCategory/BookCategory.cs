using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

public class BookCategory : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string Name { get; set; }
    public string Description { get; set; }
	private BookCategory() { }

	public BookCategory(Guid id, string name, string description, Guid tenantId) : base(id)
	{
		Name = name;
		Description = description;
		TenantId = tenantId;
	}
}
