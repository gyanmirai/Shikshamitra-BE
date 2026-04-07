# Migration Guide: Summary Property Removal

## Overview
The `Summary` property has been removed from `UserScopeHierarchyDto` as part of the performance optimization. This guide helps frontend teams migrate their code.

## What Changed

### Before (API v1.0)
```json
{
  "userId": "...",
  "userName": "Teacher",
  "isAdmin": false,
  "accessibleHierarchy": [...],
  "summary": {
    "scopeLevel": "Single Board - Multiple Mediums",
    "totalAccessibleBoards": 1,
    "totalAccessibleMediums": 3,
    "totalAccessibleMediumStages": 5,
    "totalAccessibleStandards": 12,
    "totalAccessibleDivisions": 24
  },
  "fromCache": false,
  "generatedAt": "2024-01-15T10:30:00Z"
}
```

### After (API v1.1 - Current)
```json
{
  "userId": "...",
  "userName": "Teacher",
  "isAdmin": false,
  "accessibleHierarchy": [...],
  "fromCache": false,
  "generatedAt": "2024-01-15T10:30:00Z"
}
```

## Why Was It Removed?

1. **Misleading Counts**: With the optimization, when `HasFullAccess = true`, nested collections are empty, making counts zero.
2. **Unnecessary Overhead**: Frontend can calculate counts from master data more accurately.
3. **Redundant Information**: `HasFullAccess` flags provide better information about scope level.

## Migration Steps

### Step 1: Remove Direct Summary References

❌ **Before:**
```typescript
function UserScopeDisplay({ hierarchy }: Props) {
    return (
        <div>
            <h3>{hierarchy.summary.scopeLevel}</h3>
            <p>You have access to {hierarchy.summary.totalAccessibleBoards} boards</p>
        </div>
    );
}
```

✅ **After:**
```typescript
function UserScopeDisplay({ hierarchy, masterData }: Props) {
    const scopeInfo = getScopeInfo(hierarchy, masterData);
    return (
        <div>
            <h3>{scopeInfo.level}</h3>
            <p>You have access to {scopeInfo.boardCount} boards</p>
        </div>
    );
}
```

### Step 2: Implement Scope Level Determination

```typescript
function getScopeLevel(hierarchy: UserScopeHierarchyDto): string {
    if (hierarchy.isAdmin) {
        return "Administrator - Full Access";
    }
    
    if (hierarchy.accessibleHierarchy.length === 0) {
        return "No Access";
    }
    
    const year = hierarchy.accessibleHierarchy[0];
    
    if (year.hasFullBoardAccess) {
        return "Full Access - All Boards";
    }
    
    const accessibleBoards = year.boards.filter(b => b.hasAccess);
    
    if (accessibleBoards.length === 0) {
        return "No Access";
    }
    
    if (accessibleBoards.length === 1) {
        const board = accessibleBoards[0];
        if (board.hasFullMediumAccess) {
            return `Full Access - ${board.boardName}`;
        }
        
        const accessibleMediums = board.mediums.filter(m => m.hasAccess);
        if (accessibleMediums.length === 1) {
            const medium = accessibleMediums[0];
            if (medium.hasFullMediumStageAccess) {
                return `Full Access - ${board.boardName} (${medium.mediumName})`;
            }
            return `${board.boardName} - ${medium.mediumName}`;
        }
        
        return `${board.boardName} - Multiple Mediums`;
    }
    
    return `Multiple Boards (${accessibleBoards.length})`;
}
```

### Step 3: Implement Count Calculation (If Needed)

```typescript
interface AccessCounts {
    boards: number;
    mediums: number;
    mediumStages: number;
    standards: number;
    divisions: number;
}

function calculateAccessCounts(
    hierarchy: UserScopeHierarchyDto,
    masterData: MasterDataService
): AccessCounts {
    
    const counts: AccessCounts = {
        boards: 0,
        mediums: 0,
        mediumStages: 0,
        standards: 0,
        divisions: 0
    };
    
    if (hierarchy.isAdmin) {
        // Admin has access to everything
        return {
            boards: masterData.getAllBoards().length,
            mediums: masterData.getAllMediums().length,
            mediumStages: masterData.getAllMediumStages().length,
            standards: masterData.getAllStandards().length,
            divisions: masterData.getAllDivisions().length
        };
    }
    
    for (const year of hierarchy.accessibleHierarchy) {
        if (year.hasFullBoardAccess) {
            // Count all boards for this year from master data
            const allBoards = masterData.getBoardsForYear(year.academicYearId);
            counts.boards += allBoards.length;
            
            // Count all nested entities
            for (const board of allBoards) {
                const mediums = masterData.getMediumsForBoard(board.id);
                counts.mediums += mediums.length;
                
                for (const medium of mediums) {
                    const stages = masterData.getMediumStagesForMedium(medium.id);
                    counts.mediumStages += stages.length;
                    
                    for (const stage of stages) {
                        const standards = masterData.getStandardsForStage(stage.id);
                        counts.standards += standards.length;
                        
                        for (const standard of standards) {
                            const divisions = masterData.getDivisionsForStandard(standard.id);
                            counts.divisions += divisions.length;
                        }
                    }
                }
            }
            continue;
        }
        
        for (const board of year.boards) {
            if (!board.hasAccess) continue;
            counts.boards++;
            
            if (board.hasFullMediumAccess) {
                const allMediums = masterData.getMediumsForBoard(board.boardId);
                counts.mediums += allMediums.length;
                // ... continue pattern for nested levels
                continue;
            }
            
            for (const medium of board.mediums) {
                if (!medium.hasAccess) continue;
                counts.mediums++;
                
                if (medium.hasFullMediumStageAccess) {
                    const allStages = masterData.getMediumStagesForMedium(medium.mediumId);
                    counts.mediumStages += allStages.length;
                    // ... continue pattern
                    continue;
                }
                
                // ... continue pattern for all levels
            }
        }
    }
    
    return counts;
}
```

### Step 4: Update UI Components

```typescript
// Example: Statistics Card Component

// ❌ BEFORE
const StatisticsCard: React.FC<{ hierarchy: UserScopeHierarchyDto }> = ({ hierarchy }) => {
    return (
        <Card>
            <CardHeader>Access Summary</CardHeader>
            <CardBody>
                <div className="stats">
                    <Stat label="Boards" value={hierarchy.summary.totalAccessibleBoards} />
                    <Stat label="Mediums" value={hierarchy.summary.totalAccessibleMediums} />
                    <Stat label="Standards" value={hierarchy.summary.totalAccessibleStandards} />
                    <Stat label="Divisions" value={hierarchy.summary.totalAccessibleDivisions} />
                </div>
                <Badge>{hierarchy.summary.scopeLevel}</Badge>
            </CardBody>
        </Card>
    );
};

// ✅ AFTER
const StatisticsCard: React.FC<{ 
    hierarchy: UserScopeHierarchyDto; 
    masterData: MasterDataService;
}> = ({ hierarchy, masterData }) => {
    const counts = calculateAccessCounts(hierarchy, masterData);
    const scopeLevel = getScopeLevel(hierarchy);
    
    return (
        <Card>
            <CardHeader>Access Summary</CardHeader>
            <CardBody>
                <div className="stats">
                    <Stat label="Boards" value={counts.boards} />
                    <Stat label="Mediums" value={counts.mediums} />
                    <Stat label="Standards" value={counts.standards} />
                    <Stat label="Divisions" value={counts.divisions} />
                </div>
                <Badge>{scopeLevel}</Badge>
            </CardBody>
        </Card>
    );
};
```

## Common Migration Patterns

### Pattern 1: Simple Scope Level Display

```typescript
// ❌ Before
<h3>{hierarchy.summary.scopeLevel}</h3>

// ✅ After
<h3>{getScopeLevel(hierarchy)}</h3>
```

### Pattern 2: Conditional Rendering Based on Scope

```typescript
// ❌ Before
{hierarchy.summary.totalAccessibleBoards > 1 && (
    <MultiBoard component />
)}

// ✅ After
{hierarchy.accessibleHierarchy[0]?.boards.filter(b => b.hasAccess).length > 1 && (
    <MultiBoardComponent />
)}

// Or with helper:
{getAccessibleBoardCount(hierarchy, masterData) > 1 && (
    <MultiBoardComponent />
)}
```

### Pattern 3: Access Level Badge

```typescript
// ❌ Before
function getBadgeVariant(scopeLevel: string) {
    if (scopeLevel.includes("Full Access")) return "success";
    if (scopeLevel.includes("Single")) return "warning";
    return "info";
}
<Badge variant={getBadgeVariant(hierarchy.summary.scopeLevel)}>
    {hierarchy.summary.scopeLevel}
</Badge>

// ✅ After
function getBadgeVariant(hierarchy: UserScopeHierarchyDto) {
    if (hierarchy.isAdmin) return "success";
    const year = hierarchy.accessibleHierarchy[0];
    if (year?.hasFullBoardAccess) return "success";
    
    const accessibleBoards = year?.boards.filter(b => b.hasAccess) || [];
    if (accessibleBoards.length === 1 && accessibleBoards[0].hasFullMediumAccess) {
        return "info";
    }
    return "warning";
}
<Badge variant={getBadgeVariant(hierarchy)}>
    {getScopeLevel(hierarchy)}
</Badge>
```

## Helper Utility Functions

Create a utility file to centralize scope-related logic:

```typescript
// utils/userScope.ts

export class UserScopeUtils {
    
    static getScopeLevel(hierarchy: UserScopeHierarchyDto): string {
        // Implementation from Step 2
    }
    
    static calculateCounts(
        hierarchy: UserScopeHierarchyDto,
        masterData: MasterDataService
    ): AccessCounts {
        // Implementation from Step 3
    }
    
    static hasFullAccess(hierarchy: UserScopeHierarchyDto): boolean {
        if (hierarchy.isAdmin) return true;
        const year = hierarchy.accessibleHierarchy[0];
        return year?.hasFullBoardAccess || false;
    }
    
    static hasPartialAccess(hierarchy: UserScopeHierarchyDto): boolean {
        if (hierarchy.isAdmin) return false;
        const year = hierarchy.accessibleHierarchy[0];
        if (!year) return false;
        if (year.hasFullBoardAccess) return false;
        
        return year.boards.some(b => b.hasAccess);
    }
    
    static getAccessibleBoardIds(hierarchy: UserScopeHierarchyDto): string[] {
        const ids: string[] = [];
        for (const year of hierarchy.accessibleHierarchy) {
            for (const board of year.boards) {
                if (board.hasAccess) {
                    ids.push(board.boardId);
                }
            }
        }
        return ids;
    }
}
```

## Testing Your Migration

### Checklist

- [ ] Search codebase for `.summary` references
- [ ] Search for `ScopeSummaryDto` type usage
- [ ] Update all components that display scope information
- [ ] Update all components that use count values
- [ ] Test with admin user
- [ ] Test with full board access user
- [ ] Test with partial access user
- [ ] Test with no access user
- [ ] Verify counts are accurate (compare with old API if possible)
- [ ] Check for TypeScript compilation errors
- [ ] Run unit tests
- [ ] Run integration tests

### Example Test Cases

```typescript
describe('UserScopeUtils', () => {
    it('should return "Administrator - Full Access" for admin users', () => {
        const hierarchy = { isAdmin: true, accessibleHierarchy: [] };
        expect(UserScopeUtils.getScopeLevel(hierarchy)).toBe("Administrator - Full Access");
    });
    
    it('should return "Full Access - All Boards" when hasFullBoardAccess is true', () => {
        const hierarchy = {
            isAdmin: false,
            accessibleHierarchy: [{
                hasFullBoardAccess: true,
                boards: []
            }]
        };
        expect(UserScopeUtils.getScopeLevel(hierarchy)).toBe("Full Access - All Boards");
    });
    
    it('should calculate correct counts for admin users', () => {
        const hierarchy = { isAdmin: true, accessibleHierarchy: [] };
        const masterData = createMockMasterData({ totalBoards: 10, totalDivisions: 200 });
        const counts = UserScopeUtils.calculateCounts(hierarchy, masterData);
        
        expect(counts.boards).toBe(10);
        expect(counts.divisions).toBe(200);
    });
});
```

## Timeline & Rollout

### Phase 1: Preparation (Week 1)
- Review this migration guide
- Identify all components using `summary`
- Create utility functions
- Write tests

### Phase 2: Implementation (Week 2)
- Update components one by one
- Test each component
- Ensure TypeScript errors are resolved

### Phase 3: Testing (Week 3)
- Integration testing
- User acceptance testing
- Performance testing

### Phase 4: Deployment
- Deploy to staging
- Monitor for issues
- Deploy to production

## Support & Questions

If you encounter issues during migration:

1. Check this guide first
2. Review the [Frontend Integration Guide](FRONTEND_INTEGRATION_GUIDE.md)
3. Check the [Optimization Documentation](HIERARCHY_API_OPTIMIZATION.md)
4. Contact the backend team if you suspect an API issue

## Common Issues

### Issue 1: TypeScript Errors
**Error:** `Property 'summary' does not exist on type 'UserScopeHierarchyDto'`

**Solution:** Remove all references to `.summary` and use the helper functions provided in this guide.

### Issue 2: Incorrect Counts
**Error:** Counts are zero or incorrect

**Solution:** Make sure you're checking `HasFullAccess` flags and fetching from master data when appropriate.

### Issue 3: Empty Hierarchy
**Error:** `accessibleHierarchy` is empty but user should have access

**Solution:** Check if `isAdmin` is true. Admin users may have empty hierarchy due to optimization.

---

**Last Updated:** 2024
**Migration Priority:** HIGH
**Estimated Effort:** 2-3 days (depending on codebase size)
