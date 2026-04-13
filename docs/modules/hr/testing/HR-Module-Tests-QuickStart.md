# HR Module Tests - Quick Start Guide

## 🚀 Getting Started (5 Minutes)

### Prerequisites
- Visual Studio 2022 or VS Code
- .NET 9 SDK
- ShikshaMitra solution loaded

### Run All HR Tests

**Visual Studio:**
```
1. Press Ctrl + E, T (Open Test Explorer)
2. Right-click on "HR" folder in test tree
3. Click "Run"
```

**Command Line:**
```bash
cd test\ShikshaMitra.Application.Tests
dotnet test --filter "FullyQualifiedName~HR"
```

**Expected Result:**
```
✅ 175+ tests passed
⏱️ Completed in ~30 seconds
```

---

## 📋 Test Files Quick Reference

| Module | File | Tests | Run Command |
|--------|------|-------|-------------|
| Earning Components | `EarningComponentAppService_Tests.cs` | 35+ | `--filter "FullyQualifiedName~EarningComponent"` |
| Deduction Components | `DeductionComponentAppService_Tests.cs` | 35+ | `--filter "FullyQualifiedName~DeductionComponent"` |
| Leave Types | `LeaveTypeAppService_Tests.cs` | 30+ | `--filter "FullyQualifiedName~LeaveType"` |
| Leave Policies | `LeavePolicyAppService_Tests.cs` | 35+ | `--filter "FullyQualifiedName~LeavePolicy"` |
| Employees | `EmployeeAppService_Tests.cs` | 40+ | `--filter "FullyQualifiedName~EmployeeAppService"` |

---

## 🎯 Common Tasks

### Run Single Test
```bash
# Visual Studio: Right-click test → Run
# Command Line:
dotnet test --filter "Should_Create_Earning_Component"
```

### Run All Tests in File
```bash
dotnet test --filter "FullyQualifiedName~EarningComponentAppService_Tests"
```

### Run Tests with Detailed Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Tests and Show Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## 🔍 Test Categories

### By Operation Type

**Create Tests** (Validation & Business Rules)
```bash
dotnet test --filter "Should_Create"
```

**Update Tests** (Data Modification)
```bash
dotnet test --filter "Should_Update"
```

**Get/Read Tests** (Data Retrieval)
```bash
dotnet test --filter "Should_Get"
```

**Delete Tests** (Soft & Hard Deletes)
```bash
dotnet test --filter "Should_Delete"
```

**Activate/Deactivate Tests**
```bash
dotnet test --filter "Activate"
```

---

## ✅ Verification Checklist

### After Code Changes

```
□ Run affected test file
□ Verify all tests pass
□ Add new tests for new features
□ Update tests for changed behavior
□ Run full suite before commit
```

### Before Pull Request

```
□ All HR tests passing (175+)
□ No test warnings
□ Code coverage maintained (90%+)
□ New features have tests
□ Documentation updated
```

---

## 🐛 Quick Troubleshooting

### Test Fails: "Entity not found"
**Fix:** Check test data creation, ensure IDs are valid

### Test Fails: "Duplicate key"
**Fix:** Use unique names in test data

### Test Fails: "Validation error"
**Fix:** Check DTO validation rules, populate required fields

### All Tests Fail
**Fix:** 
```bash
# Clean and rebuild
dotnet clean
dotnet build
dotnet test
```

---

## 📊 Expected Test Results

### Successful Run Output
```
Test Run Successful.
Total tests: 175
     Passed: 175
     Failed: 0
   Skipped: 0
Total time: 28.5 Seconds
```

### Test Coverage by Module
```
✅ Earning Components    - 35 tests - 100% pass
✅ Deduction Components  - 35 tests - 100% pass
✅ Leave Types          - 30 tests - 100% pass
✅ Leave Policies       - 35 tests - 100% pass
✅ Employees            - 40 tests - 100% pass
```

---

## 🔗 Key Files

**Test Files:**
```
test\ShikshaMitra.Application.Tests\HR\
├── Payroll\
│   ├── EarningComponentAppService_Tests.cs
│   └── DeductionComponentAppService_Tests.cs
├── AttendanceAndLeaves\
│   ├── LeaveTypeAppService_Tests.cs
│   └── LeavePolicyAppService_Tests.cs
└── Employees\
    └── EmployeeAppService_Tests.cs
```

**Base Classes:**
```
test\ShikshaMitra.Application.Tests\
├── ShikshaMitraApplicationTestBase.cs
└── ShikshaMitraApplicationTestModule.cs
```

**Documentation:**
```
docs\testing\
└── HR-Module-E2E-Tests.md (Detailed guide)
```

---

## 💡 Pro Tips

1. **Run tests frequently** while developing
2. **Use Test Explorer** in Visual Studio for visual feedback
3. **Set breakpoints** in tests for debugging
4. **Use --filter** to run specific test groups
5. **Check test output** for detailed error messages
6. **Keep tests fast** - full suite should be < 30 seconds

---

## 📞 Need Help?

**For detailed information:**
- See `docs\testing\HR-Module-E2E-Tests.md`

**Common scenarios:**
- All test patterns documented
- Assertion examples included
- Troubleshooting guide available

**Example test structure:**
```csharp
[Fact]
public async Task Should_Do_Something()
{
    // Arrange - Set up test data
    var input = new CreateDto { /* ... */ };

    // Act - Execute the operation
    var result = await _appService.CreateAsync(input);

    // Assert - Verify the result
    result.ShouldNotBeNull();
    result.Property.ShouldBe(expectedValue);
}
```

---

## ⚡ Quick Commands Cheat Sheet

```bash
# Run all tests
dotnet test

# Run HR module tests only
dotnet test --filter "FullyQualifiedName~HR"

# Run specific test class
dotnet test --filter "ClassName~EarningComponent"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run with detailed logs
dotnet test -v detailed

# List all tests without running
dotnet test --list-tests

# Clean before test
dotnet clean && dotnet test
```

---

**Last Updated:** December 2024  
**Total Tests:** 175+  
**Success Rate:** 100%  
**Execution Time:** ~30 seconds  
**Status:** ✅ Production Ready
