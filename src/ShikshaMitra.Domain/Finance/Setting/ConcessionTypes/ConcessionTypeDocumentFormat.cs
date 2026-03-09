using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Finance.Setting.ConcessionTypes;

public class ConcessionTypeDocumentFormat : Entity<Guid>
{
	public Guid ConcessionTypeDocumentId { get; set; }
	public DocumentType DocumentType { get; set; }

	public ConcessionTypeDocumentFormat(Guid id, Guid concessionTypeDocumentId, DocumentType documentType)
	{
		Id = id;
		ConcessionTypeDocumentId = concessionTypeDocumentId;
		DocumentType = documentType;
	}
}
