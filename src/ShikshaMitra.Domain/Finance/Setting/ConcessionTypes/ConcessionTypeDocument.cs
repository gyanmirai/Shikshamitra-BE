using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.Setting.ConcessionType;

public class ConcessionTypeDocument : Entity<Guid>
{
    public Guid ConcessionTypeId { get; private set; }
    public string DocumentName { get; private set; }
    public long DocumentSize { get; private set; }
    
    // Storing accepted formats as a comma-separated string or handling via another table?
    // Specification shows "acceptedFormat": [0, 1, 2]. 
    // For simplicity in SQL, a comma-separated string of ints is easiest for a small list, 
    // or we could make another 1:N but that seems overkill.
    // Let's store as string for now and map in AppService.
    public string AcceptedFormats { get; private set; } 

    private ConcessionTypeDocument()
    {
        /* This constructor is for deserialization / ORM purpose */
    }

    public ConcessionTypeDocument(
        Guid id,
        Guid concessionTypeId,
        string documentName,
        long documentSize,
        string acceptedFormats
    ) : base(id)
    {
        ConcessionTypeId = concessionTypeId;
        DocumentName = documentName;
        DocumentSize = documentSize;
        AcceptedFormats = acceptedFormats;
    }
    
    public void SetAcceptedFormats(string formats)
    {
        AcceptedFormats = formats;
    }
}
