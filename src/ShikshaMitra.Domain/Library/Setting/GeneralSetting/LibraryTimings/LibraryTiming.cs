using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings
{
	public class LibraryTiming : AuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; private set; }

		public Guid? AcademicYearId { get; set; }

		public string Day { get; set; }

		public string StartTime { get; set; }

		public string EndTime { get; set; }

		/// <summary>
		/// 0 = Open, 1 = Closed, 2 = Holiday
		/// </summary>
		public int Status { get; set; }

		protected LibraryTiming()
		{
			// EF materializes these required columns after this ORM constructor.
			Day = null!;
			EndTime = null!;
			StartTime = null!;
		}

		public LibraryTiming(Guid id, string day, string startTime, string endTime, int status, Guid? academicYearId = null, Guid? tenantId = null)
			: base(id)
		{
			Day = day;
			StartTime = startTime;
			EndTime = endTime;
			Status = status;
			AcademicYearId = academicYearId;
			TenantId = tenantId;
		}
	}
}
