using ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure.Dtos;
using ShikshaMitra.Finance.Setting.FeeStructures;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.Setting;

internal static class FeeStructureMappingExtensions
{
    internal static FeeStructureDetailsResponse ToDetailsResponse(this FeeStructureMetadata metadata) =>
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
            ReceiptBookId = metadata.ReceiptBookId!.Value,
            ReceiptBookName = metadata.ReceiptBook?.Name,
            Status = metadata.Status
        };

    internal static List<FeeStructureDetailsResponse> ToDetailsResponseList(
        this IEnumerable<FeeStructureMetadata> items) =>
        items.Select(x => x.ToDetailsResponse()).ToList();

    internal static StandardFeeStructureResponse ToResponse(this StandardFeeStructureQueryResult result) =>
        new()
        {
            StandardId = result.Standard.Id,
            StandardName = result.Standard.Name,
            StandardFeeStructures = result.Structures.Select(fs => new StandardFeeStructureResponseItem
            {
                Id = fs.Id,
                Name = fs.Name,
                FeeHeads = fs.FeeStructureDetails.Select(d =>
                {
                    result.FeeHeadDict.TryGetValue(d.FeeHeadId, out var fh);
                    var categoryName = fh != null && result.CategoryDict.TryGetValue(fh.FeeCategoryId, out var cn)
                        ? cn
                        : string.Empty;
                    return new FeeHeadAssignmentResponse
                    {
                        FeeHeadId = d.FeeHeadId,
                        FeeHeadName = fh?.Name ?? string.Empty,
                        CategoryName = categoryName,
                        Amount = (decimal)d.Amount,
                        IsOneTime = d.IsOneTime,
                        IsOptional = d.IsOptional
                    };
                }).ToList()
            }).ToList()
        };
}
