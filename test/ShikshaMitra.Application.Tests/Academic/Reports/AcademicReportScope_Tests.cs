using System;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportScope_Tests
{
    [Fact]
    public void Admin_With_ClassTeacher_Assignment_Should_Keep_Tenant_Wide_Access()
    {
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            IsAdmin = true
        };

        AcademicReportAppService.ShouldApplyClassTeacherRestriction(true, scope).ShouldBeFalse();
    }

    [Fact]
    public void Scoped_ClassTeacher_Should_Keep_Division_Restriction()
    {
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            DivisionIds = [Guid.NewGuid()]
        };

        AcademicReportAppService.ShouldApplyClassTeacherRestriction(true, scope).ShouldBeTrue();
    }
}
