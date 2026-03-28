-- ============================================================================
-- UserScope API - Database Setup Script
-- ============================================================================
-- Purpose: Get entity IDs needed for Postman environment configuration
-- Version: 1.0
-- Last Updated: December 2024
-- ============================================================================

-- ============================================================================
-- STEP 1: Get Tenant Information
-- ============================================================================
-- Copy the tenant ID to Postman environment variable: tenant_id

SELECT 
    Id AS TenantId,
    Name AS TenantName,
    CreationTime
FROM AbpTenants
WHERE IsDeleted = 0
ORDER BY CreationTime DESC;

-- Expected Output: Copy the 'Id' column value
-- Example: 3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e


-- ============================================================================
-- STEP 2: Set Your Tenant ID
-- ============================================================================
-- Replace with your tenant ID from Step 1
DECLARE @TenantId UNIQUEIDENTIFIER = '3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e';


-- ============================================================================
-- STEP 3: Get Academic Year ID
-- ============================================================================
-- Copy to Postman environment variable: academic_year_id

SELECT 
    Id AS AcademicYearId,
    Year AS AcademicYear,
    StartDate,
    EndDate,
    IsCurrent
FROM AcademicYears
WHERE TenantId = @TenantId
ORDER BY IsCurrent DESC, StartDate DESC;

-- Expected Output: Copy the 'Id' for current year
-- Use the row where IsCurrent = 1


-- ============================================================================
-- STEP 4: Get Education Board ID
-- ============================================================================
-- Copy to Postman environment variable: board_id (optional)

SELECT 
    Id AS BoardId,
    Type AS BoardName
FROM EducationBoards
WHERE TenantId = @TenantId
ORDER BY Type;

-- Examples: CBSE, ICSE, State Board


-- ============================================================================
-- STEP 5: Get Education Medium ID
-- ============================================================================
-- Copy to Postman environment variable: medium_id (optional)

SELECT 
    m.Id AS MediumId,
    m.Name AS MediumName,
    b.Type AS BoardName
FROM EducationMediums m
INNER JOIN EducationBoards b ON m.EducationBoardId = b.Id
WHERE m.TenantId = @TenantId
ORDER BY b.Type, m.Name;

-- Examples: English, Hindi, Marathi


-- ============================================================================
-- STEP 6: Get Education Stage ID
-- ============================================================================
-- Copy to Postman environment variable: stage_id (optional)

SELECT 
    s.Id AS StageId,
    s.Name AS StageName,
    m.Name AS MediumName,
    b.Type AS BoardName
FROM EducationMediumStages s
INNER JOIN EducationMediums m ON s.EducationMediumId = m.Id
INNER JOIN EducationBoards b ON m.EducationBoardId = b.Id
WHERE s.TenantId = @TenantId
ORDER BY b.Type, m.Name, s.Name;

-- Examples: Primary, Secondary, Higher Secondary


-- ============================================================================
-- STEP 7: Get Standard/Class ID
-- ============================================================================
-- Copy to Postman environment variable: standard_id (REQUIRED)

SELECT 
    st.Id AS StandardId,
    st.Name AS StandardName,
    s.Name AS StageName,
    m.Name AS MediumName,
    b.Type AS BoardName
FROM Standards st
INNER JOIN EducationMediumStages s ON st.EducationMediumStageId = s.Id
INNER JOIN EducationMediums m ON s.EducationMediumId = m.Id
INNER JOIN EducationBoards b ON m.EducationBoardId = b.Id
WHERE st.TenantId = @TenantId
ORDER BY b.Type, m.Name, s.Name, st.Name;

-- Examples: Class 1, Class 2, ..., Class 10, Class 11, Class 12


-- ============================================================================
-- STEP 8: Get Division IDs
-- ============================================================================
-- Copy to Postman environment variables:
--   - division_id (REQUIRED - first division)
--   - division_a_id (for bulk examples)
--   - division_b_id (for bulk examples)

SELECT 
    d.Id AS DivisionId,
    d.Name AS DivisionName,
    st.Name AS StandardName,
    s.Name AS StageName,
    m.Name AS MediumName
FROM Divisions d
INNER JOIN Standards st ON d.StandardId = st.Id
INNER JOIN EducationMediumStages s ON st.EducationMediumStageId = s.Id
INNER JOIN EducationMediums m ON s.EducationMediumId = m.Id
WHERE d.TenantId = @TenantId
ORDER BY st.Name, d.Name;

-- Examples: A, B, C, D


-- ============================================================================
-- STEP 9: Get Multiple Divisions for Bulk Assignment Examples
-- ============================================================================
-- For a specific standard (e.g., Class 10)

DECLARE @StandardId UNIQUEIDENTIFIER = NULL; -- Replace with your standard_id from Step 7

SELECT 
    Id AS DivisionId,
    Name AS DivisionName,
    'division_a_id / division_b_id' AS EnvironmentVariable
FROM Divisions
WHERE TenantId = @TenantId
  AND StandardId = @StandardId
ORDER BY Name;

-- Copy first division to: division_a_id
-- Copy second division to: division_b_id


-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

-- Verify your selections work together
-- Replace @Variables with your copied IDs

DECLARE @SelectedAcademicYearId UNIQUEIDENTIFIER = NULL;
DECLARE @SelectedStandardId UNIQUEIDENTIFIER = NULL;
DECLARE @SelectedDivisionId UNIQUEIDENTIFIER = NULL;

-- Check hierarchy
SELECT 
    'Academic Year' AS EntityType, Id, Year AS Name 
FROM AcademicYears 
WHERE Id = @SelectedAcademicYearId AND TenantId = @TenantId

UNION ALL

SELECT 
    'Standard' AS EntityType, st.Id, st.Name
FROM Standards st
WHERE st.Id = @SelectedStandardId AND st.TenantId = @TenantId

UNION ALL

SELECT 
    'Division' AS EntityType, d.Id, d.Name
FROM Divisions d
WHERE d.Id = @SelectedDivisionId AND d.TenantId = @TenantId;

-- Expected: 3 rows showing your selected entities


-- ============================================================================
-- QUICK REFERENCE: Environment Variables
-- ============================================================================

/*
Copy these IDs to Postman environment:

REQUIRED:
- tenant_id          : From Step 1
- academic_year_id   : From Step 3
- standard_id        : From Step 7
- division_id        : From Step 8

OPTIONAL (for advanced examples):
- board_id           : From Step 4
- medium_id          : From Step 5
- stage_id           : From Step 6
- division_a_id      : From Step 9 (for bulk)
- division_b_id      : From Step 9 (for bulk)

AUTO-POPULATED (by test scripts):
- access_token       : After login
- user_id            : After create user
- teacher_user_id    : In examples
- scope_assignment_id: After assignment

PRE-CONFIGURED:
- base_url           : https://localhost:44300
- tenant_username    : admin
- tenant_password    : Temp@123
*/


-- ============================================================================
-- EXAMPLE: Complete Verification
-- ============================================================================

-- After setting all variables in Postman, run this to verify data exists
SELECT 
    'Academic Year' = ay.Year,
    'Board' = b.Type,
    'Medium' = m.Name,
    'Stage' = s.Name,
    'Standard' = st.Name,
    'Division' = d.Name
FROM Divisions d
INNER JOIN Standards st ON d.StandardId = st.Id
INNER JOIN EducationMediumStages s ON st.EducationMediumStageId = s.Id
INNER JOIN EducationMediums m ON s.EducationMediumId = m.Id
INNER JOIN EducationBoards b ON m.EducationBoardId = b.Id
CROSS JOIN AcademicYears ay
WHERE d.TenantId = @TenantId
  AND ay.TenantId = @TenantId
  AND ay.IsCurrent = 1
  AND d.Id = @SelectedDivisionId
  AND st.Id = @SelectedStandardId;

-- Expected: 1 row showing complete hierarchy

-- ============================================================================
-- END OF SETUP SCRIPT
-- ============================================================================
