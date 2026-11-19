using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Communication;

public static class CommunicationCampaignDeliveryStatusResolver
{
	public static CommunicationCampaignStatus Resolve(IEnumerable<CommunicationDeliveryStatus> statuses)
	{
		var rows = statuses.ToList();
		var failed = rows.Count(status => status is CommunicationDeliveryStatus.Failed or CommunicationDeliveryStatus.Bounced or CommunicationDeliveryStatus.Skipped);
		if (failed == rows.Count) return CommunicationCampaignStatus.Failed;
		if (failed > 0) return CommunicationCampaignStatus.PartiallyFailed;
		return rows.Contains(CommunicationDeliveryStatus.Queued) ? CommunicationCampaignStatus.Queued : CommunicationCampaignStatus.Sent;
	}
}
