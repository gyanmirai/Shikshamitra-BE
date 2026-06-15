using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.TenantSettings.EducationBoards;

internal static class EducationBoardScopeFilter
{
    /// <summary>
    /// Prunes a board, medium, stage, standard, and division tree to only include
    /// nodes the given user scope context grants access to.
    /// </summary>
    internal static IReadOnlyList<EducationBoardDto> PruneTree(
        IReadOnlyList<EducationBoardDto> boards,
        UserScopeContext scope)
    {
        if (scope.IsAdmin)
            return boards;

        // Tenant-wide wildcard (admin already handled above; this catches HasWildcardAcademicYear).
        if (scope.HasTenantWideScope())
            return boards;

        var result = new List<EducationBoardDto>(boards.Count);

        foreach (var board in boards)
        {
            var boardFullAccess =
                scope.WildcardBoardAcademicYearIds.Contains(board.AcademicYearId)
                || scope.WildcardMediumBoardIds.Contains(board.Id);

            if (boardFullAccess)
            {
                result.Add(CloneBoard(board));
                continue;
            }

            // A child-level assignment can omit parent ids, so keep traversing
            // and only retain boards where at least one medium survives.
            var filteredMediums = new List<EducationMediumDto>(board.EducationMediums?.Count ?? 0);

            foreach (var medium in board.EducationMediums ?? [])
            {
                var mediumFullAccess = scope.WildcardMediumStageMediumIds.Contains(medium.Id);

                if (mediumFullAccess)
                {
                    filteredMediums.Add(CloneMedium(medium));
                    continue;
                }

                var filteredStages = new List<EducationMediumStageDto>(
                    medium.EducationMediumStages?.Count ?? 0);

                foreach (var stage in medium.EducationMediumStages ?? [])
                {
                    var stageFullAccess = scope.WildcardStandardMediumStageIds.Contains(stage.Id);

                    if (stageFullAccess)
                    {
                        filteredStages.Add(CloneStage(stage));
                        continue;
                    }

                    var filteredStandards = new List<StandardDto>(stage.Standards?.Count ?? 0);

                    foreach (var standard in stage.Standards ?? [])
                    {
                        var standardFullAccess = scope.WildcardDivisionStandardIds.Contains(standard.Id);
                        var standardVisible =
                            standardFullAccess
                            || scope.StandardIds.Contains(standard.Id)
                            || (scope.DivisionIds.Count > 0 &&
                                (standard.Divisions?.Any(d => scope.DivisionIds.Contains(d.Id)) ?? false));

                        if (!standardVisible)
                            continue;

                        if (standardFullAccess)
                        {
                            filteredStandards.Add(CloneStandard(standard));
                            continue;
                        }

                        var filteredDivisions = standard.Divisions?
                            .Where(d => scope.DivisionIds.Contains(d.Id))
                            .Select(CloneDivision)
                            .ToList() ?? [];

                        if (filteredDivisions.Count > 0)
                        {
                            filteredStandards.Add(CloneStandard(standard, filteredDivisions));
                        }
                    }

                    if (filteredStandards.Count > 0)
                    {
                        filteredStages.Add(CloneStage(stage, filteredStandards));
                    }
                }

                if (filteredStages.Count > 0)
                {
                    filteredMediums.Add(CloneMedium(medium, filteredStages));
                }
            }

            if (filteredMediums.Count > 0)
            {
                result.Add(CloneBoard(board, filteredMediums));
            }
        }

        return result;
    }

    private static EducationBoardDto CloneBoard(
        EducationBoardDto board,
        List<EducationMediumDto>? educationMediums = null) =>
        new()
        {
            Id = board.Id,
            Type = board.Type,
            AcademicYearId = board.AcademicYearId,
            EducationMediums = educationMediums
                ?? board.EducationMediums?.Select(medium => CloneMedium(medium)).ToList()
                ?? [],
            CreationTime = board.CreationTime,
            CreatorId = board.CreatorId,
            LastModificationTime = board.LastModificationTime,
            LastModifierId = board.LastModifierId
        };

    private static EducationMediumDto CloneMedium(
        EducationMediumDto medium,
        List<EducationMediumStageDto>? stages = null) =>
        new()
        {
            Id = medium.Id,
            Name = medium.Name,
            EducationBoardId = medium.EducationBoardId,
            EducationMediumStages = stages
                ?? medium.EducationMediumStages?.Select(stage => CloneStage(stage)).ToList()
                ?? [],
            CreationTime = medium.CreationTime,
            CreatorId = medium.CreatorId,
            LastModificationTime = medium.LastModificationTime,
            LastModifierId = medium.LastModifierId
        };

    private static EducationMediumStageDto CloneStage(
        EducationMediumStageDto stage,
        List<StandardDto>? standards = null) =>
        new()
        {
            Id = stage.Id,
            EducationMediumId = stage.EducationMediumId,
            Name = stage.Name,
            BannerPicture = stage.BannerPicture,
            Standards = standards
                ?? stage.Standards?.Select(standard => CloneStandard(standard)).ToList()
                ?? [],
            CreationTime = stage.CreationTime,
            CreatorId = stage.CreatorId,
            LastModificationTime = stage.LastModificationTime,
            LastModifierId = stage.LastModifierId
        };

    private static StandardDto CloneStandard(
        StandardDto standard,
        List<DivisionDto>? divisions = null) =>
        new()
        {
            Id = standard.Id,
            Name = standard.Name,
            AgeLowerLimit = standard.AgeLowerLimit,
            Divisions = divisions
                ?? standard.Divisions?.Select(division => CloneDivision(division)).ToList()
                ?? [],
            EducationMediumStageId = standard.EducationMediumStageId,
            ShiftId = standard.ShiftId,
            Shift = standard.Shift,
            CreationTime = standard.CreationTime,
            CreatorId = standard.CreatorId,
            LastModificationTime = standard.LastModificationTime,
            LastModifierId = standard.LastModifierId
        };

    private static DivisionDto CloneDivision(DivisionDto division) =>
        new()
        {
            Id = division.Id,
            Name = division.Name,
            HeadCount = division.HeadCount,
            Strength = division.Strength,
            RoomNumber = division.RoomNumber,
            RoomId = division.RoomId,
            StandardId = division.StandardId,
            CreationTime = division.CreationTime,
            CreatorId = division.CreatorId,
            LastModificationTime = division.LastModificationTime,
            LastModifierId = division.LastModifierId
        };
}
