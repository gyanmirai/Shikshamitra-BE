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
    /// Prunes a Board→Medium→Stage→Standard→Division tree to only include nodes
    /// the given user scope context grants access to.
    /// Returns the original list unmodified when the scope is admin.
    /// </summary>
    internal static IReadOnlyList<EducationBoardDto> PruneTree(
        IReadOnlyList<EducationBoardDto> boards,
        UserScopeContext scope)
    {
        if (scope.IsAdmin)
            return boards;

        // Tenant-wide wildcard (admin already handled above; this catches HasWildcardAcademicYear)
        if (scope.HasTenantWideScope())
            return boards;

        var result = new List<EducationBoardDto>(boards.Count);

        foreach (var board in boards)
        {
            // Full board access = user can see ALL mediums (and lower) under this board
            bool boardFullAccess =
                scope.WildcardBoardAcademicYearIds.Contains(board.AcademicYearId)
                || scope.WildcardMediumBoardIds.Contains(board.Id);

            // Board is visible at all if full access OR user has an explicit assignment for it
            bool boardVisible = boardFullAccess || scope.BoardIds.Contains(board.Id);

            if (!boardVisible)
                continue;

            if (boardFullAccess)
            {
                result.Add(board);
                continue;
            }

            // Prune mediums
            var filteredMediums = new List<EducationMediumDto>(board.EducationMediums?.Count ?? 0);

            foreach (var medium in board.EducationMediums ?? [])
            {
                // Full medium access = user can see ALL stages (and lower) under this medium
                bool mediumFullAccess = scope.WildcardMediumStageMediumIds.Contains(medium.Id);

                bool mediumVisible = mediumFullAccess || scope.MediumIds.Contains(medium.Id);

                if (!mediumVisible)
                    continue;

                if (mediumFullAccess)
                {
                    filteredMediums.Add(medium);
                    continue;
                }

                // Prune stages
                var filteredStages = new List<EducationMediumStageDto>(medium.EducationMediumStages?.Count ?? 0);

                foreach (var stage in medium.EducationMediumStages ?? [])
                {
                    // Full stage access = user can see ALL standards (and lower) under this stage
                    bool stageFullAccess = scope.WildcardStandardMediumStageIds.Contains(stage.Id);

                    bool stageVisible = stageFullAccess || scope.MediumStageIds.Contains(stage.Id);

                    if (!stageVisible)
                        continue;

                    if (stageFullAccess)
                    {
                        filteredStages.Add(stage);
                        continue;
                    }

                    // Prune standards
                    var filteredStandards = new List<StandardDto>(stage.Standards?.Count ?? 0);

                    foreach (var standard in stage.Standards ?? [])
                    {
                        // Full standard access = user can see ALL divisions under this standard
                        bool standardFullAccess = scope.WildcardDivisionStandardIds.Contains(standard.Id);

                        bool standardVisible = standardFullAccess || scope.StandardIds.Contains(standard.Id);

                        if (!standardVisible)
                            continue;

                        if (standardFullAccess)
                        {
                            filteredStandards.Add(standard);
                            continue;
                        }

                        // Prune divisions to only explicitly assigned ones
                        var filteredDivisions = standard.Divisions?
                            .Where(d => scope.DivisionIds.Contains(d.Id))
                            .ToList() ?? [];

                        if (filteredDivisions.Count > 0)
                        {
                            standard.Divisions = filteredDivisions;
                            filteredStandards.Add(standard);
                        }
                    }

                    if (filteredStandards.Count > 0)
                    {
                        stage.Standards = filteredStandards;
                        filteredStages.Add(stage);
                    }
                }

                if (filteredStages.Count > 0)
                {
                    medium.EducationMediumStages = filteredStages;
                    filteredMediums.Add(medium);
                }
            }

            if (filteredMediums.Count > 0)
            {
                board.EducationMediums = filteredMediums;
                result.Add(board);
            }
        }

        return result;
    }
}
