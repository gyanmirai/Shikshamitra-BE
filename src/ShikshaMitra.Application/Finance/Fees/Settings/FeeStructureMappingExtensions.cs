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
        int configuredClasses = 0) =>
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
            ConfiguredClasses = configuredClasses
        };
}
