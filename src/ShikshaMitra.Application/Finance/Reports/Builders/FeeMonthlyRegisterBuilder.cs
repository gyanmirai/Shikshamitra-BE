using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Finance.Reports.Builders;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(IFinanceReportBuilder))]
public class FeeMonthlyRegisterBuilder(
    IRepository<FeePaymentReceipt, Guid> receiptRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IAsyncQueryableExecuter asyncExecuter)
    : IFinanceReportBuilder, ITransientDependency
{
    public string ReportType => FinanceReportTypes.FeeMonthlyRegister;

    public async Task<FinanceReportPreviewDto> BuildAsync(FinanceReportRequestDto request, FinanceReportPreviewDto payload, bool exportAllRows)
    {
        var boardId = request.Filters.BoardId;
        var mediumId = request.Filters.MediumId;
        var organizationIds = request.Filters.OrganizationIds ?? new List<Guid>();
        var classIds = request.Filters.ClassIds ?? new List<Guid>();
        var divisionIds = request.Filters.DivisionIds ?? new List<Guid>();
        var receiptBookIds = request.Filters.ReceiptBookIds ?? new List<Guid>();

        // 1. Fetch Students
        var studentQuery = (await studentRepository.GetQueryableAsync())
            .WhereIf(boardId.HasValue, s => s.AcademicBoardId == boardId)
            .WhereIf(mediumId.HasValue, s => s.AcademicMediumId == mediumId)
            .WhereIf(organizationIds.Any(), s => s.AcademicMediumStageId.HasValue && organizationIds.Contains(s.AcademicMediumStageId.Value))
            .WhereIf(classIds.Any(), s => s.StandardId.HasValue && classIds.Contains(s.StandardId.Value))
            .WhereIf(divisionIds.Any(), s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value));

        var students = await asyncExecuter.ToListAsync(studentQuery);
        var studentIds = students.Select(s => s.Id).ToList();

        if (!studentIds.Any())
        {
            return payload;
        }

        // 2. Fetch Receipts
        var receiptQuery = (await receiptRepository.GetQueryableAsync())
            .Where(r => studentIds.Contains(r.StudentId))
            .WhereIf(receiptBookIds.Any(), r => r.ReceiptBookId.HasValue && receiptBookIds.Contains(r.ReceiptBookId.Value));

        var receipts = await asyncExecuter.ToListAsync(receiptQuery);

        // 3. Resolve Standards and Divisions
        var stdIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var divIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();

        var standards = await asyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(s => stdIds.Contains(s.Id)));
        var divisions = await asyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync()).Where(d => divIds.Contains(d.Id)));

        var stdMap = standards.ToDictionary(s => s.Id, s => s.Name);
        var divMap = divisions.ToDictionary(d => d.Id, d => d.Name);

        // 4. Group data by StudentId -> Month -> Amount
        var groupedData = new Dictionary<Guid, Dictionary<int, double>>();
        
        foreach (var receipt in receipts)
        {
            var studentId = receipt.StudentId;
            var month = receipt.ReceiptDate.Month;

            if (!groupedData.ContainsKey(studentId)) groupedData[studentId] = new();
            if (!groupedData[studentId].ContainsKey(month)) groupedData[studentId][month] = 0;

            groupedData[studentId][month] += receipt.TotalPaidAmount;
        }

        // 5. Define Columns
        payload.Columns.Add(new() { Id = "RollNo", Label = "Roll No." });
        payload.Columns.Add(new() { Id = "GRNo", Label = "ID No." });
        payload.Columns.Add(new() { Id = "StudentName", Label = "Name" });
        payload.Columns.Add(new() { Id = "Class", Label = "Class" });
        payload.Columns.Add(new() { Id = "Division", Label = "Division" });

        // Month columns (using standard Jan-Dec order for simplicity, though academic years might vary)
        var months = Enumerable.Range(1, 12).ToList();
        foreach (var month in months)
        {
            var monthName = new DateTime(2000, month, 1).ToString("MMM");
            payload.Columns.Add(new() { Id = $"M_{month}", Label = monthName });
        }
        payload.Columns.Add(new() { Id = "Total", Label = "Total" });

        // 6. Build Rows
        var rows = new List<Dictionary<string, string?>>();
        foreach (var student in students)
        {
            if (!groupedData.TryGetValue(student.Id, out var monthData))
            {
                continue; // Skip students with no payments, or we could include them with 0. Let's include only those with payments.
            }

            var stdName = student.StandardId.HasValue && stdMap.TryGetValue(student.StandardId.Value, out var sn) ? sn : "N/A";
            var divName = student.DivisionId.HasValue && divMap.TryGetValue(student.DivisionId.Value, out var dn) ? dn : "N/A";
            var studentName = $"{student.FirstName} {student.MiddleName} {student.LastName}".Replace("  ", " ").Trim();

            var row = new Dictionary<string, string?>
            {
                { "RollNo", student.RollNo?.ToString() ?? "N/A" },
                { "GRNo", student.GrNo?.ToString() ?? "N/A" },
                { "StudentName", studentName },
                { "Class", stdName },
                { "Division", divName }
            };

            double rowTotal = 0;
            foreach (var month in months)
            {
                if (monthData.TryGetValue(month, out var amount))
                {
                    row[$"M_{month}"] = amount.ToString("N2");
                    rowTotal += amount;
                }
                else
                {
                    row[$"M_{month}"] = "0.00";
                }
            }

            row["Total"] = rowTotal.ToString("N2");
            rows.Add(row);
        }

        payload.TotalCount = rows.Count;
        payload.Page = request.Page;
        payload.PageSize = request.PageSize;

        var sortedRows = rows.OrderBy(r => r["Class"]).ThenBy(r => r["Division"]).ThenBy(r => r["StudentName"]).ToList();

        payload.Rows = exportAllRows
            ? sortedRows
            : sortedRows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return payload;
    }
}
