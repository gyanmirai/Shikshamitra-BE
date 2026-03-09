using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Finance.Setting.ConcessionTypes;

public class ConcessionTypeDocumentFormat : ValueObject
{
	public Guid ConcessionTypeDocumentId { get; set; }
	public DocumentType DocumentType { get; set; }

	public ConcessionTypeDocumentFormat(Guid concessionTypeDocumentId, DocumentType documentType)
	{
		ConcessionTypeDocumentId = concessionTypeDocumentId;
		DocumentType = documentType;
	}

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return new object[] {
			ConcessionTypeDocumentId,
			DocumentType

		};
	}
}
