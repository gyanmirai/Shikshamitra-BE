using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public class ConcessionTypeDocumentFormat : Entity<Guid>
{
	public Guid ConcessionTypeDocumentId { get; private set; }
	public DocumentType DocumentType { get; private set; }

	private ConcessionTypeDocumentFormat()
	{
	}

	public ConcessionTypeDocumentFormat(Guid id, Guid concessionTypeDocumentId, DocumentType documentType)
	{
		Id = id;
		ConcessionTypeDocumentId = concessionTypeDocumentId;
		DocumentType = documentType;
	}
}
