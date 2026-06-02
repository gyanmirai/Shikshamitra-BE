using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Settings;

internal static class FeeStructureMappingExtensions
{
    internal static FeeStructureDetailsResponse ToDetailsResponse(
        this FeeStructureMetadata metadata,
        int totalClasses = 0,
        int totalFeeHeads = 0) =>
        new()
        {
            Id = metadata.Id,
            Name = metadata.Name,
            AcademicYearId = metadata.AcademicYearId!.Value,
            AcademicYearName = metadata.AcademicYear?.Name,
            BoardId = metadata.AcademicBoardId!.Value,
            BoardName = metadata.AcademicBoard?.Type.ToString(),
            MediumId = metadata.AcademicMediumId!.Value,
            MediumName = metadata.AcademicMedium?.Name,
            ReceiptBookId = metadata.ReceiptBookId ?? Guid.Empty,
            ReceiptBookName = metadata.ReceiptBook?.Name,
            TotalClasses = totalClasses,
            TotalFeeHeads = totalFeeHeads,
        };

    internal static List<FeeStructureDetailsResponse> ToDetailsResponseList(
        this IEnumerable<FeeStructureMetadata> items,
        Dictionary<Guid, (int TotalClasses, int TotalFeeHeads)> counts = null) =>
        items.Select(x =>
        {
            var c = counts != null && counts.TryGetValue(x.Id, out var found)
                ? found
                : (TotalClasses: 0, TotalFeeHeads: 0);
            return x.ToDetailsResponse(c.TotalClasses, c.TotalFeeHeads);
        }).ToList();
}
