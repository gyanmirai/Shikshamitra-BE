using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

public class FeeStructurePatchRequest
{
	public Guid RequestId { get; set; }
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
	// Presence-aware allow-listed command; null means clear, omitted means preserve.
	public JsonElement Changes { get; set; }
}
