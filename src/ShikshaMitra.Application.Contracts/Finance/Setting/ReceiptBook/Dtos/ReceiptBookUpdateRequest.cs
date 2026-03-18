using System;

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ReceiptBook.Dtos;

public class ReceiptBookUpdateRequest
{
    public Guid BoardId { get; set; }
    public Guid MediumId { get; set; }
    public Guid MediumStageId { get; set; }

    public string Name { get; set; }
    public string Prefix { get; set; }
    public string Suffix { get; set; }

    public long StartingNumber { get; set; }
    public long EndingNumber { get; set; }

    public string Description { get; set; }
}
