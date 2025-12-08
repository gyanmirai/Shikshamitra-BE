# HR Module Pagination - Frontend Migration Guide

## 🚀 Quick Start

The HR module's "GetAllActive" endpoints now support pagination for better performance and scalability.

## What Changed?

### Before (Old API)
```javascript
// Returns ALL active records in one request
GET /api/attendance/leave-types/active

Response: {
  "items": [...] // All records
}
```

### After (New API)
```javascript
// Returns paginated records
GET /api/attendance/leave-types/active?SkipCount=0&MaxResultCount=25

Response: {
  "totalCount": 100,  // Total number of active records
  "items": [...]      // 25 records for this page
}
```

## Affected Endpoints

| Old Endpoint | Status | Migration Required |
|--------------|--------|-------------------|
| `GET /api/attendance/leave-types/active` | ✅ Updated | Yes |
| `GET /api/attendance/leave-policies/active` | ✅ Updated | Yes |
| `GET /api/payroll/earning-components/active` | ✅ Updated | Yes |
| `GET /api/payroll/deduction-components/active` | ✅ Updated | Yes |

## Migration Steps

### Option 1: Quick Fix (Get All Records)
If you want to maintain current behavior temporarily:

```javascript
// JavaScript/TypeScript
const response = await fetch(
  '/api/attendance/leave-types/active?SkipCount=0&MaxResultCount=1000'
);
const data = await response.json();
const items = data.items; // Use items as before
```

### Option 2: Implement Pagination (Recommended)
For better performance and UX:

```javascript
// React Example
import { useState, useEffect } from 'react';

function ActiveLeaveTypes() {
  const [data, setData] = useState({ items: [], totalCount: 0 });
  const [page, setPage] = useState(0);
  const [pageSize] = useState(25);

  useEffect(() => {
    fetchData();
  }, [page]);

  const fetchData = async () => {
    const skip = page * pageSize;
    const response = await fetch(
      `/api/attendance/leave-types/active?SkipCount=${skip}&MaxResultCount=${pageSize}`
    );
    const result = await response.json();
    setData(result);
  };

  const totalPages = Math.ceil(data.totalCount / pageSize);

  return (
    <div>
      {/* Render data.items */}
      <ul>
        {data.items.map(item => (
          <li key={item.id}>{item.type}</li>
        ))}
      </ul>

      {/* Pagination controls */}
      <div>
        <button 
          onClick={() => setPage(p => Math.max(0, p - 1))}
          disabled={page === 0}
        >
          Previous
        </button>
        
        <span>Page {page + 1} of {totalPages}</span>
        
        <button 
          onClick={() => setPage(p => Math.min(totalPages - 1, p + 1))}
          disabled={page >= totalPages - 1}
        >
          Next
        </button>
      </div>
    </div>
  );
}
```

## API Parameters

### Request Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `SkipCount` | number | No | 0 | Number of records to skip |
| `MaxResultCount` | number | No | 10 | Page size (max records to return) |

### Response Structure

```typescript
interface PagedResultDto<T> {
  totalCount: number;  // Total number of active records
  items: T[];          // Current page items
}
```

## Common Scenarios

### 1. Dropdown/Select Component (Load All)
```javascript
// For dropdown - load all at once
async function loadLeaveTypes() {
  const response = await fetch(
    '/api/attendance/leave-types/active?SkipCount=0&MaxResultCount=1000'
  );
  const data = await response.json();
  return data.items; // Use for dropdown options
}
```

### 2. Data Table with Pagination
```javascript
// For table with server-side pagination
async function loadPagedData(page, pageSize) {
  const skip = page * pageSize;
  const response = await fetch(
    `/api/attendance/leave-types/active?SkipCount=${skip}&MaxResultCount=${pageSize}`
  );
  return await response.json(); // { totalCount, items }
}
```

### 3. Infinite Scroll
```javascript
// For infinite scroll
const [items, setItems] = useState([]);
const [page, setPage] = useState(0);
const pageSize = 25;

async function loadMore() {
  const skip = page * pageSize;
  const response = await fetch(
    `/api/attendance/leave-types/active?SkipCount=${skip}&MaxResultCount=${pageSize}`
  );
  const data = await response.json();
  
  setItems(prev => [...prev, ...data.items]);
  setPage(prev => prev + 1);
}
```

## Framework-Specific Examples

### React with TypeScript
```typescript
import { useState, useEffect } from 'react';

interface PagedResultDto<T> {
  totalCount: number;
  items: T[];
}

interface LeaveTypeDto {
  id: string;
  type: string;
  color: string;
  isActive: boolean;
}

function usePagedLeaveTypes(pageSize = 25) {
  const [data, setData] = useState<PagedResultDto<LeaveTypeDto>>({ 
    items: [], 
    totalCount: 0 
  });
  const [page, setPage] = useState(0);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchData();
  }, [page]);

  const fetchData = async () => {
    setLoading(true);
    try {
      const skip = page * pageSize;
      const response = await fetch(
        `/api/attendance/leave-types/active?SkipCount=${skip}&MaxResultCount=${pageSize}`
      );
      const result = await response.json();
      setData(result);
    } finally {
      setLoading(false);
    }
  };

  return { data, page, setPage, loading, totalPages: Math.ceil(data.totalCount / pageSize) };
}
```

### Angular
```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

interface PagedResultDto<T> {
  totalCount: number;
  items: T[];
}

@Injectable({ providedIn: 'root' })
export class LeaveTypeService {
  constructor(private http: HttpClient) {}

  getAllActive(skipCount = 0, maxResultCount = 25): Observable<PagedResultDto<any>> {
    const params = new HttpParams()
      .set('SkipCount', skipCount.toString())
      .set('MaxResultCount', maxResultCount.toString());

    return this.http.get<PagedResultDto<any>>(
      '/api/attendance/leave-types/active',
      { params }
    );
  }
}

// Component
export class LeaveTypesComponent implements OnInit {
  data: PagedResultDto<any>;
  page = 0;
  pageSize = 25;

  constructor(private service: LeaveTypeService) {}

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    const skip = this.page * this.pageSize;
    this.service.getAllActive(skip, this.pageSize).subscribe(result => {
      this.data = result;
    });
  }

  nextPage() {
    this.page++;
    this.loadData();
  }

  previousPage() {
    this.page = Math.max(0, this.page - 1);
    this.loadData();
  }
}
```

### Vue.js
```vue
<template>
  <div>
    <ul>
      <li v-for="item in data.items" :key="item.id">
        {{ item.type }}
      </li>
    </ul>

    <div class="pagination">
      <button @click="previousPage" :disabled="page === 0">
        Previous
      </button>
      <span>Page {{ page + 1 }} of {{ totalPages }}</span>
      <button @click="nextPage" :disabled="page >= totalPages - 1">
        Next
      </button>
    </div>
  </div>
</template>

<script>
export default {
  data() {
    return {
      data: { items: [], totalCount: 0 },
      page: 0,
      pageSize: 25
    };
  },
  computed: {
    totalPages() {
      return Math.ceil(this.data.totalCount / this.pageSize);
    }
  },
  methods: {
    async fetchData() {
      const skip = this.page * this.pageSize;
      const response = await fetch(
        `/api/attendance/leave-types/active?SkipCount=${skip}&MaxResultCount=${this.pageSize}`
      );
      this.data = await response.json();
    },
    nextPage() {
      if (this.page < this.totalPages - 1) {
        this.page++;
        this.fetchData();
      }
    },
    previousPage() {
      if (this.page > 0) {
        this.page--;
        this.fetchData();
      }
    }
  },
  mounted() {
    this.fetchData();
  }
};
</script>
```

## Testing Your Changes

### 1. Test with Different Page Sizes
```javascript
// Test with various page sizes
const pageSizes = [10, 25, 50, 100];

for (const size of pageSizes) {
  const response = await fetch(
    `/api/attendance/leave-types/active?SkipCount=0&MaxResultCount=${size}`
  );
  const data = await response.json();
  console.log(`Page size ${size}: Got ${data.items.length} items out of ${data.totalCount}`);
}
```

### 2. Test Pagination
```javascript
// Test navigating through pages
async function testPagination() {
  const pageSize = 25;
  let page = 0;
  let allItems = [];

  while (true) {
    const skip = page * pageSize;
    const response = await fetch(
      `/api/attendance/leave-types/active?SkipCount=${skip}&MaxResultCount=${pageSize}`
    );
    const data = await response.json();
    
    allItems.push(...data.items);
    console.log(`Page ${page + 1}: ${data.items.length} items`);
    
    if (allItems.length >= data.totalCount) {
      break;
    }
    page++;
  }

  console.log(`Total items fetched: ${allItems.length}`);
}
```

### 3. Test Edge Cases
```javascript
// Test first page
const firstPage = await fetch('/api/attendance/leave-types/active?SkipCount=0&MaxResultCount=10');

// Test last page
const lastPage = await fetch('/api/attendance/leave-types/active?SkipCount=90&MaxResultCount=10');

// Test beyond data
const beyondData = await fetch('/api/attendance/leave-types/active?SkipCount=1000&MaxResultCount=10');
```

## Performance Tips

### 1. Use Appropriate Page Sizes
```javascript
// Good page sizes for different scenarios
const PAGE_SIZES = {
  DROPDOWN: 1000,      // Load all for dropdowns
  TABLE: 25,           // Standard table pagination
  MOBILE: 10,          // Smaller pages for mobile
  DESKTOP: 50,         // Larger pages for desktop
  INFINITE_SCROLL: 20  // For infinite scroll
};
```

### 2. Cache Results
```javascript
// Simple cache implementation
const cache = new Map();

async function getCachedData(page, pageSize) {
  const key = `${page}-${pageSize}`;
  
  if (cache.has(key)) {
    return cache.get(key);
  }

  const data = await fetchData(page, pageSize);
  cache.set(key, data);
  return data;
}
```

### 3. Prefetch Next Page
```javascript
// Prefetch next page for better UX
async function prefetchNextPage(currentPage, pageSize) {
  const nextPage = currentPage + 1;
  const skip = nextPage * pageSize;
  
  fetch(
    `/api/attendance/leave-types/active?SkipCount=${skip}&MaxResultCount=${pageSize}`
  ); // Don't await, just trigger request
}
```

## Troubleshooting

### Issue: Getting empty items array
**Solution**: Check totalCount. If it's 0, there are no active records.

### Issue: SkipCount larger than totalCount
**Solution**: Validate page number before making request.

### Issue: Slow performance
**Solution**: Reduce MaxResultCount or implement infinite scroll instead of loading all pages.

### Issue: Duplicate items
**Solution**: Ensure you're not appending items from the same page twice.

## Migration Checklist

- [ ] Update all API calls to include pagination parameters
- [ ] Handle the new response structure (`totalCount` + `items`)
- [ ] Implement pagination UI (if applicable)
- [ ] Test with various page sizes
- [ ] Test navigation between pages
- [ ] Test edge cases (empty results, last page, etc.)
- [ ] Update unit tests for API calls
- [ ] Update integration tests
- [ ] Remove any hardcoded expectations of full data loading

## Support

If you encounter issues:
1. Check the [Complete Summary](./HR_MODULE_PAGINATION_COMPLETE_SUMMARY.md)
2. Review the [API documentation](../docs/api/)
3. Test with Swagger/Postman first
4. Contact the backend team

---

**Version**: 1.0  
**Last Updated**: January 2024  
**Status**: ✅ Production Ready
