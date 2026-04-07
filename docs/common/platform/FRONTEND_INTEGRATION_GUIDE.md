# Frontend Integration Guide: User Scope Hierarchy API

## Overview
This guide explains how to consume the optimized User Scope Hierarchy API in your frontend application.

## API Endpoint
```
GET /api/app/user-scope/{userId}/hierarchy?academicYearId={yearId}
```

## Understanding the Response

### Key Principles
1. **When `HasFullAccess = true` at any level, nested collections are empty (`[]`).**
2. **No Summary property** - Use `HasFullAccess` flags to determine scope level.

This is intentional for performance. Your frontend must interpret this correctly.

## Response Structure

```typescript
interface UserScopeHierarchyDto {
    userId: string;
    userName: string;
    email?: string;
    isAdmin: boolean;  // ⭐ If true, user has access to everything
    accessibleHierarchy: AcademicYearScopeNode[];
    fromCache: boolean;
    generatedAt: string;
    // Note: summary property has been REMOVED
}

interface AcademicYearScopeNode {
    academicYearId: string;
    academicYearName: string;
    hasFullBoardAccess: boolean;  // ⭐ If true, boards[] will be empty
    boards: BoardScopeNode[];
}

interface BoardScopeNode {
    boardId: string;
    boardName: string;
    hasAccess: boolean;
    hasFullMediumAccess: boolean;  // ⭐ If true, mediums[] will be empty
    mediums: MediumScopeNode[];
}

interface MediumScopeNode {
    mediumId: string;
    mediumName: string;
    hasAccess: boolean;
    hasFullMediumStageAccess: boolean;  // ⭐ If true, mediumStages[] will be empty
    mediumStages: MediumStageScopeNode[];
}

interface MediumStageScopeNode {
    mediumStageId: string;
    mediumStageName: string;
    hasAccess: boolean;
    hasFullStandardAccess: boolean;  // ⭐ If true, standards[] will be empty
    standards: StandardScopeNode[];
}

interface StandardScopeNode {
    standardId: string;
    standardName: string;
    hasAccess: boolean;
    hasFullDivisionAccess: boolean;  // ⭐ If true, divisions[] will be empty
    divisions: DivisionScopeNode[];
}

interface DivisionScopeNode {
    divisionId: string;
    divisionName: string;
    hasAccess: boolean;
    roomNumber?: string;
}
```

## Migration from Previous Version

### Breaking Changes
❌ **`summary` property removed** - This property no longer exists in the response.

### Before (won't work)
```typescript
// ❌ This code will fail
function showSummary(hierarchy: UserScopeHierarchyDto) {
    return (
        <div>
            <p>{hierarchy.summary.scopeLevel}</p>
            <p>Total Boards: {hierarchy.summary.totalAccessibleBoards}</p>
        </div>
    );
}
```

### After (correct approach)
```typescript
// ✅ Use HasFullAccess flags instead
function showSummary(hierarchy: UserScopeHierarchyDto, masterData: MasterData) {
    if (hierarchy.isAdmin) {
        return <Badge variant="success">Administrator - Full Access</Badge>;
    }
    
    const year = hierarchy.accessibleHierarchy[0];
    if (!year) return <Badge>No Access</Badge>;
    
    if (year.hasFullBoardAccess) {
        const allBoards = masterData.getBoardsForYear(year.academicYearId);
        return <Badge>Full Access - All {allBoards.length} Boards</Badge>;
    }
    
    const accessibleBoards = year.boards.filter(b => b.hasAccess);
    return <Badge>Partial Access - {accessibleBoards.length} Board(s)</Badge>;
}
```

## Implementation Pattern

### 1. Admin User Handling
```typescript
function renderUserScope(hierarchy: UserScopeHierarchyDto) {
    if (hierarchy.isAdmin) {
        return (
            <Alert type="info">
                This user is an administrator with full access to all data.
            </Alert>
        );
    }
    
    return <HierarchyTree hierarchy={hierarchy} />;
}
```

### 2. Full Board Access Handling
```typescript
function renderAcademicYear(year: AcademicYearScopeNode, masterData: MasterData) {
    if (year.hasFullBoardAccess) {
        // Fetch all boards from master data
        const allBoards = masterData.getBoardsForYear(year.academicYearId);
        
        return (
            <div>
                <h3>{year.academicYearName}</h3>
                <Badge>Full Access - All {allBoards.length} Boards</Badge>
                <ExpandableSection>
                    {allBoards.map(board => (
                        <BoardCard 
                            key={board.id}
                            board={board}
                            isAccessible={true}
                            hasFullAccess={true}
                        />
                    ))}
                </ExpandableSection>
            </div>
        );
    }
    
    // Render provided boards
    return (
        <div>
            <h3>{year.academicYearName}</h3>
            {year.boards.map(board => <BoardCard key={board.boardId} board={board} />)}
        </div>
    );
}
```

### 3. Calculating Counts for Display
```typescript
function calculateAccessCounts(
    hierarchy: UserScopeHierarchyDto, 
    masterData: MasterData
): { boards: number; mediums: number; standards: number; divisions: number } {
    
    if (hierarchy.isAdmin) {
        // Get total counts from master data
        return {
            boards: masterData.getAllBoards().length,
            mediums: masterData.getAllMediums().length,
            standards: masterData.getAllStandards().length,
            divisions: masterData.getAllDivisions().length,
        };
    }
    
    let counts = { boards: 0, mediums: 0, standards: 0, divisions: 0 };
    
    for (const year of hierarchy.accessibleHierarchy) {
        if (year.hasFullBoardAccess) {
            // Count all boards from master data
            const allBoards = masterData.getBoardsForYear(year.academicYearId);
            counts.boards += allBoards.length;
            
            // If full board access, count all mediums, standards, etc.
            allBoards.forEach(board => {
                const mediums = masterData.getMediumsForBoard(board.id);
                counts.mediums += mediums.length;
                // ... continue for all levels
            });
            continue;
        }
        
        for (const board of year.boards) {
            if (!board.hasAccess) continue;
            counts.boards++;
            
            if (board.hasFullMediumAccess) {
                const allMediums = masterData.getMediumsForBoard(board.boardId);
                counts.mediums += allMediums.length;
                // ... continue
                continue;
            }
            
            // ... continue pattern for each level
        }
    }
    
    return counts;
}
```

### 4. Complete Example (React)
```typescript
import React, { useEffect, useState } from 'react';
import { useMasterData } from './hooks/useMasterData';

interface Props {
    userId: string;
    academicYearId?: string;
}

export const UserScopeViewer: React.FC<Props> = ({ userId, academicYearId }) => {
    const [hierarchy, setHierarchy] = useState<UserScopeHierarchyDto | null>(null);
    const [loading, setLoading] = useState(true);
    const masterData = useMasterData();

    useEffect(() => {
        fetchHierarchy();
    }, [userId, academicYearId]);

    const fetchHierarchy = async () => {
        setLoading(true);
        const response = await fetch(
            `/api/app/user-scope/${userId}/hierarchy?academicYearId=${academicYearId || ''}`
        );
        const data = await response.json();
        setHierarchy(data);
        setLoading(false);
    };

    if (loading) return <Spinner />;
    if (!hierarchy) return <Error />;

    // Admin user - show full access message
    if (hierarchy.isAdmin) {
        return (
            <div className="admin-scope">
                <Alert type="success">
                    <strong>{hierarchy.userName}</strong> is an administrator with full access.
                </Alert>
            </div>
        );
    }

    return (
        <div className="user-scope">
            <h2>{hierarchy.userName}'s Access</h2>
            <ScopeBadge hierarchy={hierarchy} masterData={masterData} />
            {hierarchy.accessibleHierarchy.map(year => (
                <AcademicYearCard 
                    key={year.academicYearId}
                    year={year}
                    masterData={masterData}
                />
            ))}
        </div>
    );
};

// Helper component to show scope badge
const ScopeBadge: React.FC<{hierarchy: UserScopeHierarchyDto, masterData: MasterData}> = 
    ({ hierarchy, masterData }) => {
    
    const year = hierarchy.accessibleHierarchy[0];
    if (!year) return <Badge variant="secondary">No Access</Badge>;
    
    if (year.hasFullBoardAccess) {
        const boardCount = masterData.getBoardsForYear(year.academicYearId).length;
        return <Badge variant="success">Full Access - All {boardCount} Boards</Badge>;
    }
    
    const accessibleBoards = year.boards.filter(b => b.hasAccess);
    if (accessibleBoards.length === 1 && accessibleBoards[0].hasFullMediumAccess) {
        return (
            <Badge variant="info">
                Full Access - {accessibleBoards[0].boardName}
            </Badge>
        );
    }
    
    return <Badge variant="warning">Partial Access - {accessibleBoards.length} Board(s)</Badge>;
};

const AcademicYearCard: React.FC<{year: AcademicYearScopeNode, masterData: MasterData}> = 
    ({ year, masterData }) => {
    
    if (year.hasFullBoardAccess) {
        const allBoards = masterData.getBoardsForYear(year.academicYearId);
        return (
            <div className="year-card">
                <h3>{year.academicYearName}</h3>
                <Badge variant="success">Full Access - All {allBoards.length} Boards</Badge>
                <Collapsible title="View All Boards">
                    {allBoards.map(board => (
                        <div key={board.id} className="board-item">
                            ✓ {board.name} <Badge>Full Access</Badge>
                        </div>
                    ))}
                </Collapsible>
            </div>
        );
    }

    return (
        <div className="year-card">
            <h3>{year.academicYearName}</h3>
            {year.boards.map(board => (
                <BoardCard 
                    key={board.boardId}
                    board={board}
                    masterData={masterData}
                />
            ))}
        </div>
    );
};

// Continue pattern for Board, Medium, Stage, Standard, Division cards...
```

## Best Practices

### ✅ DO:
- Check `HasFullAccess` flags before processing nested collections
- Fetch master data when `HasFullAccess = true`
- Display "Full Access" badges when appropriate
- Calculate your own counts from master data when needed
- Disable checkboxes/selections when full access is granted
- Cache master data for performance

### ❌ DON'T:
- Rely on nested collections when `HasFullAccess = true`
- Try to access the `summary` property (it doesn't exist anymore)
- Make assumptions about hierarchy depth
- Ignore the `isAdmin` flag

## Edge Cases to Handle

### 1. Empty Hierarchy
```typescript
if (hierarchy.accessibleHierarchy.length === 0) {
    return <NoAccessMessage />;
}
```

### 2. Mixed Access Levels
User might have:
- Full access to Board A
- Partial access to Board B (specific mediums only)

```typescript
// Your code should handle both cases in the same loop
year.boards.forEach(board => {
    if (board.hasFullMediumAccess) {
        // Handle full access
    } else {
        // Handle partial access
    }
});
```

## Testing Checklist

- [ ] Admin user shows "Full Access" message
- [ ] Full board access shows all boards from master data
- [ ] Full medium access shows all mediums from master data
- [ ] Partial access shows specific entities only
- [ ] Mixed access (full + partial) renders correctly
- [ ] Empty access shows "No Access" message
- [ ] No code references the removed `summary` property
- [ ] Counts are calculated correctly when needed
- [ ] Performance is acceptable for large hierarchies
- [ ] Loading states are handled
- [ ] Error states are handled

## Questions?

**Q: Where did the summary go?**
A: The `summary` property was removed because it became misleading with the optimization. Use `HasFullAccess` flags and master data to determine and display scope information.

**Q: How do I show counts now?**
A: Calculate them from master data using the `HasFullAccess` flags. See the `calculateAccessCounts` example above.

**Q: If the nested collection is empty but `HasFullAccess` is false, is this a bug?**
A: Yes, this would be a data issue. Contact the backend team.

---

**Last Updated:** 2024
**API Version:** v1
**Breaking Changes:** Yes - `summary` property removed
