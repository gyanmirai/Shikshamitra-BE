using System;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class ReceiptBookUpdateRequest
{
    public Guid BoardId { get; set; }
    public Guid MediumId { get; set; }
    public Guid MediumStageId { get; set; }

    public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.
    public string Prefix { get; set; } = null!;
    public string Suffix { get; set; } = null!;

    public long StartingNumber { get; set; }
    public long EndingNumber { get; set; }

    public string Description { get; set; } = null!;
}
