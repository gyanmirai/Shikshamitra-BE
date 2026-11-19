using System;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using ShikshaMitra.Common;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceImportHeaderAliases_Tests
{
    [Fact]
    public async System.Threading.Tasks.Task Generated_Template_Should_Use_Staff_Headers_And_Roundtrip()
    {
        var service = new Import.AttendanceImportTemplateService(new ShikshaMitra.Common.Excel.ClosedXmlExcelService());
        var template = await service.GetTemplateAsync();
        using var stream = new MemoryStream(template.Content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        sheet.Cell(1, 2).GetString().ShouldBe("Staff Code");
        sheet.Cell(1, 7).GetString().ShouldBe("Staff Name");
        var rows = AttendanceImportFileParser.ParseFile(template.FileName, template.Content);
        rows.Count.ShouldBe(3);
        rows[0].EmployeeCode.ShouldBe("EMP0001");
        rows[0].EmployeeName.ShouldBe("John Doe");
    }

    [Theory]
    [InlineData("csv", "Staff Code", "Staff Name")]
    [InlineData("csv", "Employee Code", "Employee Name")]
    [InlineData("csv", "Staff Code", "Employee Name")]
    [InlineData("csv", "Employee Code", "Staff Name")]
    [InlineData("xlsx", "Staff Code", "Staff Name")]
    [InlineData("xlsx", "Employee Code", "Employee Name")]
    [InlineData("xlsx", "Staff Code", "Employee Name")]
    [InlineData("xlsx", "Employee Code", "Staff Name")]
    [InlineData("csv", " staff code ", " EMPLOYEE NAME ")]
    [InlineData("xlsx", " EMPLOYEE CODE ", " staff name ")]
    public void Should_Normalize_Aliases_Without_Changing_Internal_Fields(string extension, string code, string name)
    {
        var rows = AttendanceImportFileParser.ParseFile("attendance." + extension,
            File(extension, ["Attendance Date", code, name, "Status"], ["2026-10-05", "EMP001", "Original Name", "Present"]));
        rows.Count.ShouldBe(1);
        rows[0].EmployeeCode.ShouldBe("EMP001");
        rows[0].EmployeeName.ShouldBe("Original Name");
        rows[0].Status.ShouldBe("Present");
    }

    [Theory]
    [InlineData("csv", "Staff Code", "Employee Code")]
    [InlineData("xlsx", "Staff Code", "Employee Code")]
    [InlineData("csv", "Staff Name", "Employee Name")]
    [InlineData("xlsx", "Staff Name", "Employee Name")]
    [InlineData("csv", "Staff Code", "staff code")]
    [InlineData("xlsx", "Staff Name", "STAFF NAME")]
    public void Should_Reject_Duplicate_Field_Aliases(string extension, string first, string second)
    {
        var headers = first.Contains("Code", StringComparison.OrdinalIgnoreCase)
            ? new[] { "Attendance Date", first, second }
            : new[] { "Attendance Date", "Staff Code", first, second };
        var values = new string[headers.Length];
        Array.Fill(values, "value");
        var error = Should.Throw<BusinessException>(() => AttendanceImportFileParser.ParseFile("attendance." + extension, File(extension, headers, values)));
        error.Code.ShouldBe(HRErrorCodes.AttendanceImportDuplicateHeaders);
    }

    private static byte[] File(string extension, string[] headers, string[] values)
    {
        if (extension == "csv") return Encoding.UTF8.GetBytes(string.Join(',', headers) + "\n" + string.Join(',', values));
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Attendance");
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(2, i + 1).Value = values[i];
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
