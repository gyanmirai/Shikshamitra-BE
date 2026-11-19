using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Setup;

public static class FinanceSetupKeys
{
	public const string Invalid = "ShikshaMitra:FinanceSetup:Invalid";
	public const string InvalidStoredState = "ShikshaMitra:FinanceSetup:InvalidStoredState";
	public static readonly IReadOnlyDictionary<string, string> SettingStages = new Dictionary<string, string>(StringComparer.Ordinal) {
		["income-categories"] = "accounts", ["expense-categories"] = "accounts", ["bank-accounts"] = "accounts",
		["fee-categories"] = "fees", ["payment-terms"] = "fees", ["charge-categories"] = "fees", ["concession-types"] = "fees",
		["collection-policy"] = "policies", ["refund-policy"] = "policies", ["concession-policy"] = "policies", ["proration-policy"] = "policies",
		["fee-structures"] = "structures"
	};
	public static bool IsStage(string? key) => key is "accounts" or "fees" or "policies" or "structures" or "assignment" or "operations" or "review";
}
