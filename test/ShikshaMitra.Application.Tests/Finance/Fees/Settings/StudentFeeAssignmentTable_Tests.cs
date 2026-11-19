using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    [Fact]
    public async Task Assignment_Table_Filters_And_Sorts_Before_Counting_And_Paging()
    {
        using var reviewer = Reviewer();
        using var tenant = _currentTenant.Change(TestTenantId);
        var input = await ReadyAssignmentAsync();
        var repository = GetRequiredService<IRepository<Student, Guid>>();
        var original = await repository.GetAsync(input.StudentId);
        original.AssignGrNumber("ASSIGN-TABLE-001");
        original.AssignRollNo(2);
        await repository.UpdateAsync(original, autoSave: true);
        foreach (var roll in new int?[] { 12, null })
        {
            var student = new Student(Guid.NewGuid(), TestTenantId, "ColumnQuery", "Middle", "Student",
                input.AcademicYearId, original.AcademicBoardId!.Value, original.AcademicMediumId,
                original.AcademicMediumStageId!.Value, original.StandardId, status: StudentLifeCycleStatusEnum.Enrolled);
            student.AssignGrNumber($"ASSIGN-TABLE-{(roll.HasValue ? "002" : "003")}");
            student.AssignRollNo(roll);
            await repository.InsertAsync(student, autoSave: true);
        }
        var service = GetRequiredService<IStudentFeeStructureAppService>();
        var baseline = await service.GetStudentsAsync(new() { AcademicYearId = input.AcademicYearId, MaxResultCount = 100 });
        var baselineStudent = baseline.Items.Single(row => row.StudentId == input.StudentId);
        baselineStudent.GrNumber.ShouldBe("ASSIGN-TABLE-001");
        baselineStudent.RollNumber.ShouldBe(2);
        baselineStudent.IsActive.ShouldBeTrue();
        baselineStudent.IsFeeStructureAssigned.ShouldBeFalse();
        (await service.GetStudentsAsync(new() { AcademicYearId = input.AcademicYearId, Search = "assign-table", MaxResultCount = 100 }))
            .TotalCount.ShouldBe(3);
        var search = new AssignmentStudentListInput { AcademicYearId = input.AcademicYearId,
            Search = "assign-table", SortBy = "rollNumber", SortOrder = "desc", MaxResultCount = 1,
            Filters = """[{"column":"rollNumber","type":"between","value":{"min":2,"max":12}},{"column":"isFeeStructureAssigned","type":"equals","value":1},{"column":"isActive","type":"equals","value":0}]""" };
        var page = await service.GetStudentsAsync(search);
        page.TotalCount.ShouldBe(2);
        page.Items.Single().RollNumber.ShouldBe(12);
        page.Items.Single().StudentName.ShouldBe("ColumnQuery Middle Student");
        page.Items.Single().BoardName.ShouldNotBeNullOrWhiteSpace();
        page.Items.Single().MediumName.ShouldNotBeNullOrWhiteSpace();
        page.Items.Single().StageName.ShouldNotBeNullOrWhiteSpace();
        page.Items.Single().ClassName.ShouldNotBeNullOrWhiteSpace();
        search.SkipCount = 1;
        (await service.GetStudentsAsync(search)).Items.Single().StudentId.ShouldBe(input.StudentId);
        search.SkipCount = 0;
        search.Search = "ColumnQuery Middle";
        (await service.GetStudentsAsync(search)).TotalCount.ShouldBe(1);
        search.Search = "12";
        (await service.GetStudentsAsync(search)).Items.Single().RollNumber.ShouldBe(12);

        search.Search = "ASSIGN-TABLE-001";
        search.Filters = """[{"column":"isFeeStructureAssigned","type":"equals","value":0}]""";
        (await service.GetStudentsAsync(search)).TotalCount.ShouldBe(0);
        search.SortBy = "action";
        await Should.ThrowAsync<AbpValidationException>(() => service.GetStudentsAsync(search));
    }
}
