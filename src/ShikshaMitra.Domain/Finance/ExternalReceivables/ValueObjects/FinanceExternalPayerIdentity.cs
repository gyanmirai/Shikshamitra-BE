using System;
using ShikshaMitra.Library;
using Volo.Abp;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalPayerIdentity
{
	public FinanceExternalPayerKind Kind { get; }
	public Guid PersonId { get; }
	public Guid? StudentId => Kind == FinanceExternalPayerKind.Student ? PersonId : null;
	public Guid? EmployeeId => Kind == FinanceExternalPayerKind.Employee ? PersonId : null;

	public FinanceExternalPayerIdentity(FinanceExternalPayerKind kind, Guid personId)
	{
		if (personId == Guid.Empty || !Enum.IsDefined(kind))
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidPayer);
		Kind = kind;
		PersonId = personId;
	}
}
