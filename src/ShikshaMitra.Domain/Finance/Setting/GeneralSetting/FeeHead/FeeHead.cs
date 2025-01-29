using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;


public class FeeHead : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string FeeHeadName { get; private set; }

	protected FeeHead() { } // For ORM

	internal FeeHead(Guid id, Guid? tenantId, string feeHeadName) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		FeeHeadName = feeHeadName;
	}
}