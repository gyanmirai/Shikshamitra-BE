using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Reports;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportRequestGuard_Tests
{
    private readonly FinanceReportRequestGuard _guard = new();

    [Theory]
    [InlineData("Fee demand and collection summary")]
    [InlineData("Outstanding fees and ageing")]
    [InlineData("Student fee statement")]
    [InlineData("Daily collection register")]
    [InlineData("Receipt register and corrections")]
    [InlineData("Payment verification and clearing ageing")]
    public void Should_Accept_Phase_One_Report_Types(string reportType)
    {
        var request = ValidRequest();
        request.ReportType = reportType;
        request.Filters.AcademicYearIds = [Guid.NewGuid()];
        request.Filters.FromDate = new DateTime(2026, 11, 1);
        request.Filters.ToDate = new DateTime(2026, 11, 18);
        request.Filters.StudentId = Guid.NewGuid();

        _guard.ValidateAndNormalize(request);

        request.ReportType.ShouldBe(reportType);
    }

    [Fact]
    public async Task Should_Write_A_Decimal_Amount_As_A_Numeric_Excel_Cell()
    {
        var data = new FinanceReportPreviewDto
        {
            ReportType = "Daily collection register",
            Columns = [new() { Id = "Amount", Label = "Amount (INR)", Kind = "money" }],
            Rows = [new() { ["Amount"] = "103.25" }]
        };

        var bytes = await new FinanceExcelService().GenerateReportExcelAsync(data);
        using var stream = new System.IO.MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        workbook.Worksheet(1).Cell(2, 1).GetDouble().ShouldBe(103.25);
    }

    [Fact]
    public void Should_Normalize_A_Valid_Request()
    {
        var yearId = Guid.NewGuid();
        var request = new FinanceReportRequestDto
        {
            ReportType = $"  {FinanceReportTypes.FeeCollectionBatchwise}  ",
            Format = " PDF ",
            Filters = new FinanceReportFilterDto
            {
                AcademicYearIds = [yearId, yearId],
                Months = [12, 1, 12]
            }
        };

        _guard.ValidateAndNormalize(request);

        request.ReportType.ShouldBe(FinanceReportTypes.FeeCollectionBatchwise);
        request.Format.ShouldBe("pdf");
        request.Filters.AcademicYearIds.ShouldBe([yearId]);
        request.Filters.Months.ShouldBe([1, 12]);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    public void Should_Reject_Invalid_Pagination(int page, int pageSize)
    {
        var request = ValidRequest();
        request.Page = page;
        request.PageSize = pageSize;

        var error = Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(request));

        error.Code.ShouldBe(FinanceReportRequestGuard.InvalidRequest);
    }

    [Fact]
    public void Should_Reject_Unsupported_Report_Type()
    {
        var request = ValidRequest();
        request.ReportType = "Give me every finance table";

        var error = Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(request));

        error.Code.ShouldBe(FinanceReportRequestGuard.UnsupportedReportType);
    }

    [Fact]
    public void Should_Reject_Invalid_Months_And_Date_Ranges()
    {
        var invalidMonth = ValidRequest();
        invalidMonth.Filters.Months = [0, 13];
        Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(invalidMonth))
            .Code.ShouldBe(FinanceReportRequestGuard.InvalidRequest);

        var invalidDates = ValidRequest();
        invalidDates.Filters.FromDate = new DateTime(2026, 9, 2);
        invalidDates.Filters.ToDate = new DateTime(2026, 9, 1);
        Should.Throw<BusinessException>(() => _guard.ValidateAndNormalize(invalidDates))
            .Code.ShouldBe(FinanceReportRequestGuard.InvalidRequest);
    }

    [Theory]
    [InlineData(FinanceReportTypes.FeeCollectionBatchwise, true)]
    [InlineData(FinanceReportTypes.FeeCollectionBatchAndMonthwise, true)]
    [InlineData(FinanceReportTypes.FeeCollectionMonthAndFeeHeadwise, true)]
    [InlineData(FinanceReportTypes.FeeMonthlyRegister, false)]
    public void Should_Identify_Reports_That_Require_Year_Wide_Access(
        string reportType,
        bool expected)
    {
        FinanceReportRequestGuard.RequiresAcademicYearWideScope(reportType).ShouldBe(expected);
    }

    private static FinanceReportRequestDto ValidRequest() => new()
    {
        ReportType = FinanceReportTypes.FeeMonthlyRegister,
        Format = "excel",
        Page = 1,
        PageSize = 50
    };
}
