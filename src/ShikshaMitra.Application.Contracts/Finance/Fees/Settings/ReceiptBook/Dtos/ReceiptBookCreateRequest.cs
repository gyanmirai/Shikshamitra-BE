using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class ReceiptBookCreateRequest
{
    [Required]
    public Guid BoardId { get; set; }

    [Required]
    public Guid MediumId { get; set; }

    [Required]
    public Guid MediumStageId { get; set; }

    [Required]
    public string Name { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

    public string Prefix { get; set; } = null!;
    public string Suffix { get; set; } = null!;

    public long StartingNumber { get; set; }
    public long EndingNumber { get; set; }

    public string Description { get; set; } = null!;
}
