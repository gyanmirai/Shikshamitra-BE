using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Dashboard;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceDashboardProjection_Tests
{
    [Fact]
    public void Counts_distinct_overdue_students_and_keeps_charges_separate_from_principal()
    {
        var student = Guid.NewGuid();
        var gradeA = Guid.NewGuid();
        var gradeB = Guid.NewGuid();
        var result = FinanceDashboardBalanceProjection.Build(
            [
                new(gradeA, student, 100.25m, 30.25m),
                new(gradeA, student, 40m, 40m),
                new(gradeB, student, 25m, 5m),
                new(gradeB, null, 60m, 15m)
            ],
            [new(gradeA, 22m)],
            [new(gradeA, "Class 1", Guid.NewGuid(), Guid.NewGuid(), "CBSE / English"),
                new(gradeB, "Class 2", Guid.NewGuid(), Guid.NewGuid(), "CBSE / English")]);

        Assert.Equal(225.25m, result.Outstanding);
        Assert.Equal(90.25m, result.Overdue);
        Assert.Equal(1, result.StudentsWithOverdueFees);
        Assert.Equal(22m, result.Classes.Single(row => row.ClassId == gradeA).SeparateCharges);
        Assert.All(result.Classes, row => Assert.Equal(1, row.StudentsWithOverdueFees));
    }

    [Fact]
    public void Dashboard_entry_points_require_the_exact_dashboard_permission()
    {
        var policy = ((AuthorizeAttribute)Attribute.GetCustomAttribute(
            typeof(FinanceDashboardAppService), typeof(AuthorizeAttribute))!).Policy;
        Assert.Equal(ShikshaMitraSubModulePermissions.FinanceDashboard, policy);
    }
}
