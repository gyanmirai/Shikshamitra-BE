using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class FeeHeadCreateRequest
{
    [Required]
    public Guid FeeCategoryId { get; set; }

    [Required]
    public string Name { get; set; }

    public FeeHeadModule Module { get; set; }

    public bool IsConcessionApplied { get; set; }

    public string Description { get; set; }
}
