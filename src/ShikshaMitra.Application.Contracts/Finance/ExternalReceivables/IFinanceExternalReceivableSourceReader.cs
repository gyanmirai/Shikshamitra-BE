using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalReceivableSourceFacts(
	FinanceExternalSourceReference Source,
	Guid TenantId,
	Guid AcademicYearId,
	FinanceExternalPayerDto Payer,
	decimal Amount,
	string Currency,
	DateOnly AssessmentDate,
	DateOnly DueDate,
	bool SchoolDatesConfirmed,
	string Reason,
	string EvidenceReference)
{
	public bool AcademicYearConfirmed { get; init; }
	public string AcademicYearEvidenceJson { get; init; } = string.Empty;
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public FinanceExternalSourceReasonOrigin? ReasonOrigin { get; init; }
}

public interface IFinanceExternalReceivableSourceReader
{
	Task<FinanceExternalReceivableSourceFacts> ReadAuthorizedAsync(FinanceExternalSourceReference source);
}

public interface IFinanceExternalReceivableSourceBinding
{
	Task BindAsync(FinanceExternalSourceReference source, Guid sourceLinkId);
}
