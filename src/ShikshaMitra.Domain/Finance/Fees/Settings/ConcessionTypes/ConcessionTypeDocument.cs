using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public class ConcessionTypeDocument : Entity<Guid>
{
    public Guid ConcessionTypeId { get; private set; }
    public string DocumentName { get; private set; }
	public long MaxFileSizeBytes { get; private set; }
	public bool IsRequired { get; private set; }
    public ICollection<ConcessionTypeDocumentFormat> AcceptedFormats { get; private set; } = new List<ConcessionTypeDocumentFormat>();

    private ConcessionTypeDocument()
    {
        // EF materializes these required columns after this ORM constructor.
        DocumentName = null!;

        /* This constructor is for deserialization / ORM purpose */
    }

    public ConcessionTypeDocument(
        Guid id,
        Guid concessionTypeId,
        string documentName,
		long maxFileSizeBytes,
		bool isRequired
    ) : base(id)
    {
        ConcessionTypeId = concessionTypeId;
		DocumentName = ConcessionTypeConsts.NormalizeDisplayName(documentName);
		if (string.IsNullOrWhiteSpace(DocumentName) || DocumentName.Length > ConcessionTypeConsts.MaxDocumentNameLength)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		if (maxFileSizeBytes <= 0 || maxFileSizeBytes > ConcessionTypeConsts.AbsoluteMaxFileSizeBytes)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		MaxFileSizeBytes = maxFileSizeBytes;
		IsRequired = isRequired;
    }
    
	public void AddAcceptedFormat(DocumentType format)
	{
		if (!Enum.IsDefined(format))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		if (!AcceptedFormats.Any(f => f.DocumentType == format))
		{
			AcceptedFormats.Add(new ConcessionTypeDocumentFormat(Guid.NewGuid(), Id, format));
		}
	}
}
