using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Members
{
	public class Member : AuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; set; }
        public string Name { get; set; }
		public string Role { get; set; }
		public int ReferenceId { get; set; }

		private Member() { }
		public Member(
			Guid id,
			string name,
			string role,
			int referenceId,
			Guid? tenantId)
			: base(id)
		{
			Name = name;
			Role = role;
			ReferenceId = referenceId;
			TenantId = tenantId;
		}
	}
}
