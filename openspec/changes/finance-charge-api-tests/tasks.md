## 1. Test Infrastructure

- [ ] 1.1 Add `CreateClientAs(Guid userId)` protected method to `ShikshaMitraApiTestBase` — sets `X-Test-UserId` header on a new client
- [ ] 1.2 Create `ChargeTestHelper.cs` static class in `test/ShikshaMitra.HttpApi.Tests/Finance/Charges/` with static `Default*` fields and `CreateStudentAsync`, `CreateChargeCategoryAsync`, `CreateChargeAsync`, `BuildCreateDto` methods
- [ ] 1.3 Create `ChargeApiTestsBase.cs` abstract class in same folder — seeds `DefaultAcademicYearId` from factory, declares `BaseUrl = "/api/app/charges"` and `ChargeCategoriesUrl = "/api/charge-categories"`, provides `CreatePendingChargeAsync()` shortcut

## 2. Core API Tests (Critical tier)

- [ ] 2.1 Create `ChargeCoreApi_Tests.cs` — test: create with approval-required category → status = 0 (Pending)
- [ ] 2.2 Add test: create with no-approval category → status = 1 (Approved) (auto-approve)
- [ ] 2.3 Add test: create response shape has all scope fields populated (boardId, mediumId, standardId, divisionId, studentName, categoryName, requestorId)
- [ ] 2.4 Add test: GetById returns full `ChargeDetailResponse` shape (student, requestor, approvalDetails nested objects)
- [ ] 2.5 Add test: GetById with nonexistent Guid returns non-200
- [ ] 2.6 Add test: GetAll returns paginated list containing the created charge with status counts present

## 3. Workflow API Tests (Important tier)

- [ ] 3.1 Create `ChargeWorkflowApi_Tests.cs` — test: approve pending charge by different user → status = 1, approvedBy populated
- [ ] 3.2 Add test: reject pending charge by different user → status = 2, rejectionReason returned
- [ ] 3.3 Add test: update pending charge amount → returns 200 with updated amount
- [ ] 3.4 Add test: update approved charge → returns error (not 200)
- [ ] 3.5 Add test: delete pending charge → 204, subsequent GetById returns error
- [ ] 3.6 Add test: delete approved charge → returns error (not 204)
- [ ] 3.7 Add test: GetAll with `boardId` filter → all items match boardId
- [ ] 3.8 Add test: GetAll with `educationMediumId` filter → all items match mediumId
- [ ] 3.9 Add test: GetAll with `pageSize=200` → response `pageSize` equals 100 (cap)
- [ ] 3.10 Add test: GetAll `page=2` with few records → empty items list

## 4. Validation API Tests (Extended tier)

- [ ] 4.1 Create `ChargeValidationApi_Tests.cs` — test: approve own charge (same user as requestor) → returns error (maker-checker)
- [ ] 4.2 Add test: reject own charge (same user as requestor) → returns error (maker-checker)
- [ ] 4.3 Add test: create with inactive category → returns error
- [ ] 4.4 Add test: create with nonexistent studentId → returns error
- [ ] 4.5 Add test: create with nonexistent academicYearId → returns error
- [ ] 4.6 Add test: create with `amount = 0` → returns 400
- [ ] 4.7 Add test: create with `amount = -1` → returns 400
- [ ] 4.8 Add test: create with empty `purposeDescription` → returns 400
- [ ] 4.9 Add test: approve already-approved charge → returns error
- [ ] 4.10 Add test: reject already-rejected charge → returns error
- [ ] 4.11 Add test: reject with empty `rejectionReason` → returns 400

## 5. Verify

- [ ] 5.1 Run `dotnet build` — verify 0 errors
- [ ] 5.2 Run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=Finance"` — all new tests pass
- [ ] 5.3 Run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Critical"` — all critical tests pass
