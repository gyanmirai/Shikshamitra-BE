using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

/// <summary>
/// A fee head as submitted inside the nested feeHeads[] collection of a fee category create/update request.
/// Id is null for a new fee head and set to an existing Guid to update that fee head in place.
/// </summary>
public class FeeHeadItemRequest
{
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; }

    public FeeHeadModule Module { get; set; }

    public bool IsConcessionApplied { get; set; }

    public string Description { get; set; }
}
