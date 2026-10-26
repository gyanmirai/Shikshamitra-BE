using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Finance
	{
		public static class SelfService
		{
			public const string View = "Finance.SelfService.View";
			public const string DownloadReceipts = "Finance.SelfService.DownloadReceipts";
			public const string DownloadStatement = "Finance.SelfService.DownloadStatement";
			public const string RequestConcession = "Finance.SelfService.RequestConcession";
			public const string RequestRefund = "Finance.SelfService.RequestRefund";
			public const string RequestAdjustment = "Finance.SelfService.RequestAdjustment";
			public static readonly string[] All = [View, DownloadReceipts, DownloadStatement, RequestConcession, RequestRefund, RequestAdjustment];
			public static readonly Dictionary<string, string> DisplayNames = All.ToDictionary(x => x, x => "Permission:" + x);
		}
	}
}
