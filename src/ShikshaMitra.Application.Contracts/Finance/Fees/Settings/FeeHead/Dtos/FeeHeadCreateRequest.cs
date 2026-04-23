using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Settings.FeeHead.Dtos;

public class FeeHeadCreateRequest
{
    [Required]
    public Guid FeeCategoryId { get; set; }

    [Required]
    public string Name { get; set; }

    public string Description { get; set; }
}
