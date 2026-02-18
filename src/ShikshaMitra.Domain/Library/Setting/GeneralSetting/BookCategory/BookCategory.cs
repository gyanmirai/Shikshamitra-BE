using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory
{
	public class BookCategory : AuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; private set; }

		public Guid? AcademicYearId { get; set; }

		public string Name { get; set; }

		public string Description { get; set; }

		protected BookCategory() { }

		public BookCategory(string name, string description, Guid? academicYearId = null, Guid? tenantId = null)
		{
			Name = name;
			Description = description;
			AcademicYearId = academicYearId;
			TenantId = tenantId;
		}
	}
}
