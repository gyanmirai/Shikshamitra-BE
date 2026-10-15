using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
    private static readonly IReadOnlyDictionary<string, FinanceTableColumn> AssignmentColumns = new Dictionary<string, FinanceTableColumn>
    {
        ["studentName"] = new(FinanceTableValueType.Text),
        ["grNumber"] = new(FinanceTableValueType.Text),
        ["rollNumber"] = new(FinanceTableValueType.Number),
        ["standardDivision"] = new(FinanceTableValueType.Text),
        ["boardName"] = new(FinanceTableValueType.Text),
        ["mediumName"] = new(FinanceTableValueType.Text),
        ["stageName"] = new(FinanceTableValueType.Text),
        ["className"] = new(FinanceTableValueType.Text),
        ["divisionName"] = new(FinanceTableValueType.Text),
        ["isActive"] = new(FinanceTableValueType.Enum, true, [0, 1]),
        ["isFeeStructureAssigned"] = new(FinanceTableValueType.Enum, true, [0, 1]),
    };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> AssignmentFields = new Dictionary<string, LambdaExpression>
    {
        ["studentName"] = (Expression<Func<AssignmentStudentDto, string>>)(r => r.StudentName),
        ["grNumber"] = (Expression<Func<AssignmentStudentDto, string>>)(r => r.GrNumber),
        ["rollNumber"] = (Expression<Func<AssignmentStudentDto, int?>>)(r => r.RollNumber),
        ["standardDivision"] = (Expression<Func<AssignmentStudentDto, string>>)(r => r.StandardDivision),
        ["boardName"] = (Expression<Func<AssignmentStudentDto, string>>)(r => r.BoardName),
        ["mediumName"] = (Expression<Func<AssignmentStudentDto, string>>)(r => r.MediumName),
        ["stageName"] = (Expression<Func<AssignmentStudentDto, string>>)(r => r.StageName),
        ["className"] = (Expression<Func<AssignmentStudentDto, string>>)(r => r.ClassName),
        ["divisionName"] = (Expression<Func<AssignmentStudentDto, string>>)(r => r.DivisionName),
        // Label order: Active before Inactive; Assigned before Unassigned.
        ["isActive"] = (Expression<Func<AssignmentStudentDto, int>>)(r => r.IsActive ? 0 : 1),
        ["isFeeStructureAssigned"] = (Expression<Func<AssignmentStudentDto, int>>)(r => r.IsFeeStructureAssigned ? 0 : 1),
    };
}
