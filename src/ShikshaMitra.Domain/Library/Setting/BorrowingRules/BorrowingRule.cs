using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Setting.BorrowingRules
{
	public class BorrowingRule : AuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; private set; }

		public Guid AcademicYearId { get; set; }

		/// <summary>
		/// 0 = Student, 1 = Teacher
		/// </summary>
		public int Role { get; set; }

		public int MaxBooksAtTime { get; set; }

		public int ReservationLimit { get; set; }

		protected BorrowingRule() { }

		public BorrowingRule(Guid id, Guid? tenantId, Guid academicYearId, int role, int maxBooksAtTime, int reservationLimit)
			: base(id)
		{
			TenantId = tenantId;
			AcademicYearId = academicYearId;
			Role = role;
			MaxBooksAtTime = maxBooksAtTime;
			ReservationLimit = reservationLimit;
		}
	}
}
