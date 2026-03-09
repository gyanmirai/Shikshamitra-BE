using ShikshaMitra.Common;
using ShikshaMitra.Finance.Setting.ConcessionTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.Setting.ConcessionType;

public class ConcessionTypeDocument : Entity<Guid>
{
    public Guid ConcessionTypeId { get; private set; }
    public string DocumentName { get; private set; }
    public long DocumentSize { get; private set; }
    public ICollection<ConcessionTypeDocumentFormat> AcceptedFormats { get; private set; } = new List<ConcessionTypeDocumentFormat>();

    private ConcessionTypeDocument()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public ConcessionTypeDocument(
        Guid id,
        Guid concessionTypeId,
        string documentName,
        long documentSize
    ) : base(id)
    {
        ConcessionTypeId = concessionTypeId;
        DocumentName = documentName;
        DocumentSize = documentSize;
    }
    
    public void AddAcceptedFormat(DocumentType format)
	{
		if (!AcceptedFormats.Any(f => f.DocumentType == format))
		{
			AcceptedFormats.Add(new ConcessionTypeDocumentFormat(Id, format));
		}
	}
}
