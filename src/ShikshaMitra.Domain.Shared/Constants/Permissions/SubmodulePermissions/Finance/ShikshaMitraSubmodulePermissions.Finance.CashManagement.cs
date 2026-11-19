using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
	public static partial class Finance
	{
		public static class Accounts_CashManagement
		{
			private const string Prefix = "Finance.Accounts.CashManagement";
			public const string View = Prefix + ".View";
			public const string Approve = Prefix + ".Approve";
			public const string Deposit = Prefix + ".Deposit";
			public const string Reconcile = Prefix + ".Reconcile";
			public static readonly string[] All = [View, Approve, Deposit, Reconcile];
			public static readonly Dictionary<string, string> DisplayNames = new()
			{
				{ Prefix, "Permission:Finance.Accounts.CashManagement" },
				{ View, "Permission:Finance.Accounts.CashManagement.View" },
				{ Approve, "Permission:Finance.Accounts.CashManagement.Approve" },
				{ Deposit, "Permission:Finance.Accounts.CashManagement.Deposit" },
				{ Reconcile, "Permission:Finance.Accounts.CashManagement.Reconcile" }
			};
		}
	}
}
