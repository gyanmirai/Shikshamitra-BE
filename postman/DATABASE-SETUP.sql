-- ShikshaMitra UserScope Testing - Quick Start SQL
-- Run these queries to get IDs for your Postman environment

-- ============================================
-- 1. GET EXISTING DATA IDs
-- ============================================

-- Get Education Board IDs
SELECT 
    "Id" as board_id,
    "Name" as board_name
FROM "AppEducationBoards"
ORDER BY "Name"
LIMIT 5;

-- Get Education Medium IDs
SELECT 
    "Id" as medium_id,
    "Name" as medium_name,
    "EducationBoardId" as board_id
FROM "AppEducationMediums"
ORDER BY "Name"
LIMIT 5;

-- Get Education Medium Stage IDs
SELECT 
    "Id" as stage_id,
    "Name" as stage_name,
    "EducationMediumId" as medium_id
FROM "AppEducationMediumStages"
ORDER BY "Name"
LIMIT 5;

-- Get Standard/Class IDs
SELECT 
    "Id" as standard_id,
    "Name" as standard_name,
    "EducationMediumStageId" as stage_id
FROM "AppStandards"
ORDER BY "Name"
LIMIT 5;

-- Get Division IDs (organized by Standard)
SELECT 
    d."Id" as division_id,
    d."Name" as division_name,
    d."StandardId" as standard_id,
    s."Name" as standard_name
FROM "AppDivisions" d
INNER JOIN "AppStandards" s ON d."StandardId" = s."Id"
ORDER BY s."Name", d."Name"
LIMIT 10;

-- Get Academic Year IDs
SELECT 
    "Id" as academic_year_id,
    "Name" as academic_year_name,
    "StartDate",
    "EndDate",
    "IsActive"
FROM "AppAcademicYears"
ORDER BY "StartDate" DESC
LIMIT 5;

-- ============================================
-- 2. GET TWO DIVISIONS FROM SAME STANDARD
-- ============================================

-- Replace 'YOUR_STANDARD_ID' with an actual Standard ID from above
SELECT 
    "Id" as division_id,
    "Name" as division_name,
    "StandardId"
FROM "AppDivisions"
WHERE "StandardId" = 'YOUR_STANDARD_ID' -- Replace this!
LIMIT 2;

-- ============================================
-- 3. CHECK EXISTING ROLES
-- ============================================

SELECT 
    "Id" as role_id,
    "Name" as role_name,
    "NormalizedName",
    "IsPublic",
    "IsDefault"
FROM "AbpRoles"
ORDER BY "Name";

-- ============================================
-- 4. CHECK EXISTING USERS
-- ============================================

SELECT 
    u."Id" as user_id,
    u."UserName",
    u."Email",
    u."Name",
    u."Surname",
    STRING_AGG(r."Name", ', ') as roles
FROM "AbpUsers" u
LEFT JOIN "AbpUserRoles" ur ON u."Id" = ur."UserId"
LEFT JOIN "AbpRoles" r ON ur."RoleId" = r."Id"
GROUP BY u."Id", u."UserName", u."Email", u."Name", u."Surname"
ORDER BY u."UserName"
LIMIT 10;

-- ============================================
-- 5. CHECK ADMIN PERMISSIONS
-- ============================================

SELECT 
    "Name" as permission_name,
    "ProviderName",
    "ProviderKey"
FROM "AbpPermissionGrants"
WHERE "ProviderName" = 'R' 
  AND "ProviderKey" = 'admin'
  AND "Name" LIKE 'ShikshaMitra.UserScope%'
ORDER BY "Name";

-- ============================================
-- 6. CHECK EXISTING USER SCOPE ASSIGNMENTS
-- ============================================

SELECT 
    usa."Id" as assignment_id,
    usa."UserId",
    u."UserName",
    usa."RoleName",
    usa."StandardId",
    s."Name" as standard_name,
    usa."DivisionId",
    d."Name" as division_name,
    usa."Description",
    usa."IsActive",
    usa."CreationTime"
FROM "UserScopeAssignments" usa
LEFT JOIN "AbpUsers" u ON usa."UserId" = u."Id"
LEFT JOIN "AppStandards" s ON usa."StandardId" = s."Id"
LEFT JOIN "AppDivisions" d ON usa."DivisionId" = d."Id"
WHERE usa."IsDeleted" = false
ORDER BY usa."CreationTime" DESC
LIMIT 10;

-- ============================================
-- 7. SAMPLE DATA FOR POSTMAN ENVIRONMENT
-- ============================================

-- This query generates a JSON-like output for easy copy-paste
-- Copy the result and set these values in your Postman environment

WITH sample_data AS (
    SELECT 
        (SELECT "Id" FROM "AppEducationBoards" LIMIT 1) as board_id,
        (SELECT "Id" FROM "AppEducationMediums" LIMIT 1) as medium_id,
        (SELECT "Id" FROM "AppEducationMediumStages" LIMIT 1) as stage_id,
        (SELECT "Id" FROM "AppStandards" LIMIT 1) as standard_id,
        (SELECT "Id" FROM "AppAcademicYears" WHERE "IsActive" = true LIMIT 1) as academic_year_id
)
SELECT 
    'board_id: ' || board_id as "Set in Postman",
    'medium_id: ' || medium_id,
    'stage_id: ' || stage_id,
    'standard_id: ' || standard_id,
    'academic_year_id: ' || academic_year_id
FROM sample_data;

-- Get two divisions from first standard
SELECT 
    'division_a_id: ' || "Id" as "For Division A",
    'division_b_id: ' || "Id" as "For Division B"
FROM "AppDivisions"
WHERE "StandardId" = (SELECT "Id" FROM "AppStandards" LIMIT 1)
LIMIT 2;

-- ============================================
-- 8. CLEANUP QUERIES (OPTIONAL)
-- ============================================

-- Delete test user scope assignments
-- DELETE FROM "UserScopeAssignments" WHERE "Description" LIKE '%Test%';

-- Delete test users
-- DELETE FROM "AbpUsers" WHERE "UserName" LIKE 'teacher.%';

-- ============================================
-- 9. CREATE SAMPLE TEST DATA (IF NEEDED)
-- ============================================

-- Create a test standard if none exist
/*
INSERT INTO "AppStandards" ("Id", "Name", "Code", "DisplayOrder", "IsActive", "EducationMediumStageId", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "CreatorId")
VALUES 
    (gen_random_uuid(), 'Class 5', 'CLS5', 5, true, 'YOUR_STAGE_ID', '{}', gen_random_uuid()::text, NOW(), NULL);
*/

-- Create test divisions if none exist
/*
INSERT INTO "AppDivisions" ("Id", "Name", "DisplayOrder", "IsActive", "StandardId", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "CreatorId")
VALUES 
    (gen_random_uuid(), 'Division A', 1, true, 'YOUR_STANDARD_ID', '{}', gen_random_uuid()::text, NOW(), NULL),
    (gen_random_uuid(), 'Division B', 2, true, 'YOUR_STANDARD_ID', '{}', gen_random_uuid()::text, NOW(), NULL);
*/

-- ============================================
-- 10. VERIFICATION QUERIES
-- ============================================

-- Verify a specific user's scope
/*
SELECT 
    usa.*,
    s."Name" as standard_name,
    d."Name" as division_name
FROM "UserScopeAssignments" usa
LEFT JOIN "AppStandards" s ON usa."StandardId" = s."Id"
LEFT JOIN "AppDivisions" d ON usa."DivisionId" = d."Id"
WHERE usa."UserId" = 'YOUR_USER_ID'
  AND usa."IsDeleted" = false;
*/

-- Count assignments by scope level
SELECT 
    CASE 
        WHEN "DivisionId" IS NOT NULL THEN 'Division Level'
        WHEN "StandardId" IS NOT NULL THEN 'Standard Level'
        WHEN "EducationMediumStageId" IS NOT NULL THEN 'Stage Level'
        WHEN "EducationMediumId" IS NOT NULL THEN 'Medium Level'
        WHEN "EducationBoardId" IS NOT NULL THEN 'Board Level'
        ELSE 'No Scope'
    END as scope_level,
    COUNT(*) as assignment_count
FROM "UserScopeAssignments"
WHERE "IsDeleted" = false
  AND "IsActive" = true
GROUP BY scope_level
ORDER BY assignment_count DESC;

-- ============================================
-- INSTRUCTIONS
-- ============================================

/*
1. Run Section 1 queries to get all available IDs
2. Copy the IDs you want to use
3. Set them in your Postman environment variables:
   - board_id
   - medium_id
   - stage_id
   - standard_id
   - division_id
   - division_a_id
   - division_b_id
   - academic_year_id

4. Run Section 3 to verify roles exist
   If not, create them via Postman (see TESTING-GUIDE.md)

5. Run Section 5 to verify admin has UserScope permissions
   If not, grant them via Postman

6. Start testing with the Postman collection!

For detailed testing workflows, see: postman/TESTING-GUIDE.md
*/
