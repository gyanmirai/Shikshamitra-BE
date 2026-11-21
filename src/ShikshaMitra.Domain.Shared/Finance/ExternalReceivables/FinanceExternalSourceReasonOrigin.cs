using System;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalSourceReasonOrigin(
	int Schema,
	Guid CategoryId,
	string? RawRemarks,
	bool UsesCategoryReason);
