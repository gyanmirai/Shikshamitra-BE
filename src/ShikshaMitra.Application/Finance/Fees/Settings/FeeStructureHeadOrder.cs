using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance.Fees.Settings;

internal static class FeeStructureHeadOrder
{
	private const string OrderProperty = "FeeHeadOrder";

	public static void Save(FeeStructure structure, IEnumerable<Guid> feeHeadIds) =>
		structure.SetProperty(OrderProperty, JsonSerializer.Serialize(feeHeadIds));

	public static IReadOnlyList<Guid> Read(FeeStructure structure)
	{
		try
		{
			var saved = structure.GetProperty<string>(OrderProperty);
			if (!string.IsNullOrEmpty(saved))
				return JsonSerializer.Deserialize<List<Guid>>(saved) ?? [];

			var review = structure.GetProperty<string>("PublicationReview");
			return string.IsNullOrEmpty(review)
				? []
				: JsonSerializer.Deserialize<FeeStructurePreviewDto>(review)?.Coverage?.Select(head => head.FeeHeadId)
					.ToList() ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	public static IEnumerable<FeeStructureDetail> Order(FeeStructure structure)
	{
		var rank = CreateRank(Read(structure));
		return structure.FeeStructureDetails
			.OrderBy(detail => rank.GetValueOrDefault(detail.FeeHeadId, int.MaxValue))
			.ThenBy(detail => detail.CreationTime)
			.ThenBy(detail => detail.Id);
	}

	public static Dictionary<Guid, int> CreateRank(IEnumerable<Guid> feeHeadIds) =>
		feeHeadIds.Distinct().Select((id, index) => (id, index)).ToDictionary(entry => entry.id, entry => entry.index);
}
