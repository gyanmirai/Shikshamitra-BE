# Pagination Support Implementation for Active Endpoints

## Overview
Successfully implemented pagination support for the active endpoints in both LeaveType and LeavePolicy services to improve scalability and performance when dealing with large datasets.

## Changes Made

### 1. Interface Updates

#### ILeaveTypeAppService
- **File**: `src\ShikshaMitra.Application.Contracts\HR\AttendanceAndLeaves\Settings\ILeaveTypeAppService.cs`
- **Changes**:
  - Updated `GetAllActiveAsync()` method signature
  - **Before**: `Task<ListResultDto<LeaveTypeDto>> GetAllActiveAsync()`
  - **After**: `Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync(PagedResultRequestDto input)`

#### ILeavePolicyAppService
- **File**: `src\ShikshaMitra.Application.Contracts\HR\AttendanceAndLeaves\Settings\ILeavePolicyAppService.cs`
- **Changes**:
  - Updated `GetAllActiveAsync()` method signature
  - **Before**: `Task<ListResultDto<LeavePolicyDto>> GetAllActiveAsync()`
  - **After**: `Task<PagedResultDto<LeavePolicyDto>> GetAllActiveAsync(PagedResultRequestDto input)`

### 2. Application Service Updates

#### LeaveTypeAppService
- **File**: `src\ShikshaMitra.Application\HR\AttendanceAndLeaves\Settings\LeaveTypeAppService.cs`
- **Changes**:
  1. Updated cache type from `IDistributedCache<List<LeaveTypeDto>>` to `IDistributedCache<PagedResultDto<LeaveTypeDto>>`
  2. Modified cache key generation to include pagination parameters:
     ```csharp
     private string GetActiveCacheKey(int skipCount, int maxResultCount) => 
         $"EmployeeLeaveTypes:Active:Tenant:{CurrentTenant?.Id ?? Guid.Empty}:Skip:{skipCount}:Take:{maxResultCount}";
     ```
  3. Implemented pagination logic in `GetAllActiveAsync`:
     - Accepts `PagedResultRequestDto input` parameter
     - Applies `Skip()` and `Take()` for pagination
     - Returns total count along with paged items
     - Caches results with pagination-specific keys
  4. Enhanced cache clearing to remove common pagination cache entries (10, 25, 50, 100 page sizes)

#### LeavePolicyAppService
- **File**: `src\ShikshaMitra.Application\HR\AttendanceAndLeaves\Settings\LeavePolicyAppService.cs`
- **Changes**: Same as LeaveTypeAppService
  1. Updated cache type to `IDistributedCache<PagedResultDto<LeavePolicyDto>>`
  2. Modified cache key generation with pagination parameters
  3. Implemented pagination logic in `GetAllActiveAsync`
  4. Enhanced cache clearing for common pagination scenarios

### 3. Controller Updates

#### LeaveTypeController
- **File**: `src\ShikshaMitra.HttpApi\Controllers\HR\AttendanceAndLeaves\LeaveTypeController.cs`
- **Changes**:
  - Updated `GetAllActiveAsync` endpoint to accept `PagedResultRequestDto` from query parameters
  - **Before**: `Task<ListResultDto<LeaveTypeDto>> GetAllActiveAsync()`
  - **After**: `Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync([FromQuery] PagedResultRequestDto input)`

#### LeavePolicyController
- **File**: `src\ShikshaMitra.HttpApi\Controllers\HR\AttendanceAndLeaves\Settings\LeavePolicyController.cs`
- **Changes**: Same as LeaveTypeController
  - Updated endpoint to accept pagination parameters from query string

## Implementation Details

### Pagination Logic
```csharp
public async Task<PagedResultDto<T>> GetAllActiveAsync(PagedResultRequestDto input)
{
    // 1. Generate pagination-specific cache key
    var activeCacheKey = GetActiveCacheKey(input.SkipCount, input.MaxResultCount);
    
    // 2. Check cache first
    var cached = await _cache.GetAsync(activeCacheKey);
    if (cached != null)
    {
        return cached;
    }
    
    // 3. Query active items
    var queryable = await Repository.GetQueryableAsync();
    var query = queryable.Where(x => x.IsActive);
    
    // 4. Get total count for pagination
    var totalCount = await AsyncExecuter.CountAsync(query);
    
    // 5. Apply pagination
    var items = await AsyncExecuter.ToListAsync(
        query.OrderBy(x => x.PropertyName)
             .Skip(input.SkipCount)
             .Take(input.MaxResultCount)
    );
    
    // 6. Create paged result
    var result = new PagedResultDto<T>(
        totalCount,
        mappedItems
    );
    
    // 7. Cache the result for 24 hours
    await _cache.SetAsync(activeCacheKey, result, ...);
    
    return result;
}
```

### Cache Management
- **Cache Keys**: Include tenant ID, skip count, and max result count
- **Cache Duration**: 24 hours (absolute expiration)
- **Cache Clearing**: Clears common pagination sizes (10, 25, 50, 100) on data modifications

## API Usage

### LeaveType Active Endpoint
**Endpoint**: `GET /api/attendance/leave-types/active`

**Query Parameters**:
- `SkipCount` (int, optional, default: 0) - Number of records to skip
- `MaxResultCount` (int, optional, default: 10) - Maximum number of records to return

**Example Request**:
```http
GET /api/attendance/leave-types/active?SkipCount=0&MaxResultCount=25
```

**Response**:
```json
{
    "totalCount": 50,
    "items": [
        {
            "id": "guid",
            "type": "Annual Leave",
            "color": "#FF5733",
            "isActive": true,
            ...
        }
    ]
}
```

### LeavePolicy Active Endpoint
**Endpoint**: `GET /api/attendance/leave-policies/active`

**Query Parameters**:
- `SkipCount` (int, optional, default: 0)
- `MaxResultCount` (int, optional, default: 10)

**Example Request**:
```http
GET /api/attendance/leave-policies/active?SkipCount=10&MaxResultCount=25
```

**Response**:
```json
{
    "totalCount": 75,
    "items": [
        {
            "id": "guid",
            "policyName": "Standard Leave Policy",
            "leaveType": "Annual Leave",
            "maximumDaysPerYear": 20,
            "isActive": true,
            ...
        }
    ]
}
```

## Benefits

### 1. Scalability
- Handles large datasets efficiently
- Reduces memory consumption by loading only required records
- Database query optimization through pagination

### 2. Performance
- Cached results for common pagination scenarios
- Reduced network payload size
- Faster response times for paginated data

### 3. User Experience
- Supports lazy loading in frontend applications
- Enables infinite scroll and page-based navigation
- Better performance for mobile and low-bandwidth scenarios

### 4. Backward Compatibility
- While the signature changed from `ListResultDto` to `PagedResultDto`, clients can still retrieve all data by setting appropriate `MaxResultCount`
- Query parameters are optional (uses ABP Framework defaults)

## Build Status
✅ **Build Successful** - All changes compile without errors

## Testing Recommendations

### 1. Unit Tests
- Test pagination with various skip/take values
- Test cache hit/miss scenarios
- Test cache clearing on data modifications
- Test with zero, one, and many active records

### 2. Integration Tests
- Test endpoint with different pagination parameters
- Test with large datasets (1000+ records)
- Test concurrent requests with different pagination
- Test cache expiration behavior

### 3. Performance Tests
- Measure response time with and without cache
- Compare memory usage vs non-paginated approach
- Test with varying page sizes (10, 25, 50, 100, 1000)

## Migration Notes

### For Frontend Teams
1. Update API calls to include pagination parameters:
   ```javascript
   // Before
   GET /api/attendance/leave-types/active
   
   // After
   GET /api/attendance/leave-types/active?SkipCount=0&MaxResultCount=25
   ```

2. Handle `PagedResultDto` response structure:
   ```javascript
   const response = await fetch(url);
   const data = await response.json();
   const totalCount = data.totalCount;
   const items = data.items;
   ```

3. Default values (if parameters omitted):
   - `SkipCount`: 0
   - `MaxResultCount`: 10 (ABP default)

### Database Impact
- No database schema changes required
- No migration needed
- Existing data remains unchanged

## Related Files

### Modified Files
1. `src\ShikshaMitra.Application.Contracts\HR\AttendanceAndLeaves\Settings\ILeaveTypeAppService.cs`
2. `src\ShikshaMitra.Application.Contracts\HR\AttendanceAndLeaves\Settings\ILeavePolicyAppService.cs`
3. `src\ShikshaMitra.Application\HR\AttendanceAndLeaves\Settings\LeaveTypeAppService.cs`
4. `src\ShikshaMitra.Application\HR\AttendanceAndLeaves\Settings\LeavePolicyAppService.cs`
5. `src\ShikshaMitra.HttpApi\Controllers\HR\AttendanceAndLeaves\LeaveTypeController.cs`
6. `src\ShikshaMitra.HttpApi\Controllers\HR\AttendanceAndLeaves\LeavePolicyController.cs`

### Related Documentation
- `EMPLOYEE_LEAVE_FINAL_SUMMARY.md`
- `LEAVE_MANAGEMENT_IMPLEMENTATION_STATUS.md`
- `EMPLOYEE_LEAVE_MANAGEMENT_REMAINING_WORK.md`

## Next Steps

1. **Testing**: Implement unit and integration tests for pagination
2. **Documentation**: Update API documentation with pagination examples
3. **Frontend Updates**: Coordinate with frontend team for API changes
4. **Monitoring**: Add logging for cache hit/miss rates
5. **Optimization**: Consider implementing cache tags for better cache invalidation
6. **Consider Similar Endpoints**: Apply same pattern to other "GetAllActive" endpoints in the codebase (e.g., EarningComponents, DeductionComponents)

## Notes
- The implementation uses ABP Framework's `PagedResultRequestDto` which provides standard pagination parameters
- Cache invalidation clears only common page sizes; for full cache reset, consider implementing a cache tagging strategy
- The 24-hour cache duration is configurable and can be adjusted based on data volatility requirements
