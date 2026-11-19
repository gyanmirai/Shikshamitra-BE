using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.Dtos;

public class ReceiptBookResponse : EntityDto<Guid>
{
	public string Name { get; set; } = null!; // The response mapper populates these members after construction.
	public string Prefix { get; set; } = null!;
	public string Suffix { get; set; } = null!;

	public Guid BoardId { get; set; }
	public string BoardName { get; set; } = null!;
	public Guid MediumId { get; set; }
	public string MediumName { get; set; } = null!;
	public Guid MediumStageId { get; set; }
	public string MediumStageName { get; set; } = null!;

	public long StartingNumber { get; set; }
	public long EndingNumber { get; set; }

	public string Description { get; set; } = null!;
	public bool IsActive { get; set; }
	public DateTime CreatedAt { get; set; }
}
