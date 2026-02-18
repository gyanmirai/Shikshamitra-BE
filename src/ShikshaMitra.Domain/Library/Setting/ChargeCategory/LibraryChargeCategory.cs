using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Setting.ChargeCategory
{
	public class LibraryChargeCategory : AuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; private set; }

		public Guid AcademicYearId { get; set; }

		public string ChargeType { get; set; }

		public decimal Amount { get; set; }

		public bool IsFixed { get; set; }

		public string Description { get; set; }

		protected LibraryChargeCategory() { }

		public LibraryChargeCategory(
			Guid id,
			Guid? tenantId,
			Guid academicYearId,
			string chargeType,
			decimal amount,
			bool isFixed,
			string description)
			: base(id)
		{
			TenantId = tenantId;
			AcademicYearId = academicYearId;
			ChargeType = chargeType;
			Amount = amount;
			IsFixed = isFixed;
			Description = description;
		}
	}
}
