using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalSourcePosition(Guid SourceId, Guid? SourceLinkId, Guid? AcademicYearId,
	FinanceExternalPositionEvidence Evidence, FinanceExternalRecordedSettlementDeclaration RecordedSettlementDeclaration,
	decimal AssessmentOriginal, string? Currency, ChargeStatus? ApprovalStatus,
	FinanceExternalPositionAmounts? Amounts, IReadOnlyList<string> ReviewIssues);
