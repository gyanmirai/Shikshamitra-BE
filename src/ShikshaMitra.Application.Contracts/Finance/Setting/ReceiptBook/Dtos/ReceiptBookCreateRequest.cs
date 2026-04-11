using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ReceiptBook.Dtos;

public class ReceiptBookCreateRequest
{
    [Required]
    public Guid BoardId { get; set; }

    [Required]
    public Guid MediumId { get; set; }

    [Required]
    public Guid MediumStageId { get; set; }

    [Required]
    public string Name { get; set; }

    public string Prefix { get; set; }
    public string Suffix { get; set; }

    public long StartingNumber { get; set; }
    public long EndingNumber { get; set; }

    public string Description { get; set; }
}
