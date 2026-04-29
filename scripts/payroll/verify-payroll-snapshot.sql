-- Verify snapshot schema and monthly state persistence

select to_regclass('public."HR_PayrollRunItems"') as payroll_run_items_table;
select to_regclass('public."IX_HR_PayrollRunItems_Tenant_Month_Employee"') as unique_index;

-- Replace :month_value with target month (e.g. '2026-04')
-- Replace :tenant_id with target tenant id UUID
select
    "MonthYear",
    count(*) as total_rows,
    sum(case when "ReviewStatus" = 'PENDING' then 1 else 0 end) as pending_rows,
    sum(case when "ReviewStatus" = 'REVIEWED' then 1 else 0 end) as reviewed_rows,
    sum(case when "ReviewStatus" = 'LOCKED' then 1 else 0 end) as locked_rows
from "HR_PayrollRunItems"
where "MonthYear" = :month_value
  and "TenantId" = :tenant_id
  and "IsDeleted" = false
group by "MonthYear";

-- Duplicate safety check
select
    "TenantId",
    "MonthYear",
    "EmployeeId",
    count(*) as duplicate_count
from "HR_PayrollRunItems"
where "IsDeleted" = false
group by "TenantId", "MonthYear", "EmployeeId"
having count(*) > 1;
