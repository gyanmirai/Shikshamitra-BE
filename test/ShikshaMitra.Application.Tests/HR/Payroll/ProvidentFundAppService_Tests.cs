using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

/// <summary>
/// End-to-End tests for Provident Fund App Service
/// </summary>
public class ProvidentFundAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IProvidentFundAppService _providentFundAppService;

    public ProvidentFundAppService_Tests()
    {
        _providentFundAppService = GetRequiredService<IProvidentFundAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Provident_Fund_Rule()
    {
        // Arrange
        var input = new CreateUpdateProvidentFundDto
        {
            Name = "Standard PF",
            Code = "STD_PF",
            EmployeePercentage = 12,
            EmployerPercentage = 12
        };

        // Act
        var result = await _providentFundAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe("Standard PF");
        result.Code.ShouldBe("STD_PF");
        result.EmployeePercentage.ShouldBe(12);
        result.EmployerPercentage.ShouldBe(12);
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_PF_Code()
    {
        // Arrange
        const string code = "PF_DUPLICATE";
        await _providentFundAppService.CreateAsync(new CreateUpdateProvidentFundDto
        {
            Name = "First PF Rule",
            Code = code,
            EmployeePercentage = 10,
            EmployerPercentage = 10
        });

        // Act + Assert
        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _providentFundAppService.CreateAsync(new CreateUpdateProvidentFundDto
            {
                Name = "Second PF Rule",
                Code = code,
                EmployeePercentage = 12,
                EmployerPercentage = 12
            });
        });

        exception.Code.ShouldBe("ShikshaMitra:ProvidentFundCodeAlreadyExists");
    }

    [Fact]
    public async Task Should_Create_With_Different_Percentages()
    {
        // Arrange — employer may contribute more than employee
        var input = new CreateUpdateProvidentFundDto
        {
            Name = "Enhanced PF",
            Code = "ENH_PF",
            EmployeePercentage = 12,
            EmployerPercentage = 15
        };

        // Act
        var result = await _providentFundAppService.CreateAsync(input);

        // Assert
        result.EmployeePercentage.ShouldBe(12);
        result.EmployerPercentage.ShouldBe(15);
    }

    [Fact]
    public async Task Should_Create_With_Zero_Percentages()
    {
        // Arrange
        var input = new CreateUpdateProvidentFundDto
        {
            Name = "No Contribution PF",
            Code = "ZERO_PF",
            EmployeePercentage = 0,
            EmployerPercentage = 0
        };

        // Act
        var result = await _providentFundAppService.CreateAsync(input);

        // Assert
        result.EmployeePercentage.ShouldBe(0);
        result.EmployerPercentage.ShouldBe(0);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Provident_Fund_By_Id()
    {
        // Arrange
        var created = await CreateTestProvidentFundAsync("Get By ID PF", "GET_PF");

        // Act
        var result = await _providentFundAppService.GetAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Get By ID PF");
        result.Code.ShouldBe("GET_PF");
    }

    [Fact]
    public async Task Should_Throw_When_PF_Not_Found()
    {
        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _providentFundAppService.GetAsync(Guid.NewGuid());
        });
    }

    [Fact]
    public async Task Should_Get_Paginated_List()
    {
        // Arrange
        var suffixes = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O" };
        for (int i = 0; i < 15; i++)
        {
            await CreateTestProvidentFundAsync($"Paginated PF {i + 1}", $"PG_PF_{suffixes[i]}");
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _providentFundAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public async Task Should_Search_By_Name()
    {
        // Arrange
        await CreateTestProvidentFundAsync("Searchable PF Rule", "SEARCH_PF");
        await CreateTestProvidentFundAsync("Other Rule", "OTHER_PF");

        // Act
        var result = await _providentFundAppService.GetFilteredListAsync(0, 50, "Searchable");

        // Assert
        result.Items.ShouldContain(x => x.Name.Contains("Searchable"));
        result.Items.ShouldNotContain(x => x.Name == "Other Rule");
    }

    [Fact]
    public async Task Should_Search_By_Code()
    {
        // Arrange
        await CreateTestProvidentFundAsync("PF By Code", "UNIQUE_CODE");
        await CreateTestProvidentFundAsync("Another PF", "DIFF_CODE");

        // Act
        var result = await _providentFundAppService.GetFilteredListAsync(0, 50, "UNIQUE");

        // Assert
        result.Items.ShouldContain(x => x.Code == "UNIQUE_CODE");
    }

    [Fact]
    public async Task Should_Return_All_When_Search_Is_Null()
    {
        // Arrange
        await CreateTestProvidentFundAsync("Null Search PF A", "NS_PF_A");
        await CreateTestProvidentFundAsync("Null Search PF B", "NS_PF_B");

        // Act
        var result = await _providentFundAppService.GetFilteredListAsync(0, 100, null);

        // Assert
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Provident_Fund()
    {
        // Arrange
        var created = await CreateTestProvidentFundAsync("Original PF", "ORIG_PF");

        var updateInput = new CreateUpdateProvidentFundDto
        {
            Name = "Updated PF",
            Code = "UPD_PF",
            EmployeePercentage = 15,
            EmployerPercentage = 18
        };

        // Act
        var result = await _providentFundAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated PF");
        result.Code.ShouldBe("UPD_PF");
        result.EmployeePercentage.ShouldBe(15);
        result.EmployerPercentage.ShouldBe(18);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Provident_Fund()
    {
        // Arrange
        var created = await CreateTestProvidentFundAsync("PF to Delete", "DEL_PF");

        // Act
        await _providentFundAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _providentFundAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Special_Characters_In_Name()
    {
        // Arrange
        var input = new CreateUpdateProvidentFundDto
        {
            Name = "PF Rule & Contribution (12%) - Standard",
            Code = "SPEC_PF",
            EmployeePercentage = 12,
            EmployerPercentage = 12
        };

        // Act
        var result = await _providentFundAppService.CreateAsync(input);

        // Assert
        result.Name.ShouldBe(input.Name);
    }

    [Fact]
    public async Task Should_Handle_Decimal_Percentages()
    {
        // Arrange
        var input = new CreateUpdateProvidentFundDto
        {
            Name = "Fractional PF",
            Code = "FRAC_PF",
            EmployeePercentage = 12.5m,
            EmployerPercentage = 13.67m
        };

        // Act
        var result = await _providentFundAppService.CreateAsync(input);

        // Assert
        result.EmployeePercentage.ShouldBe(12.5m);
        result.EmployerPercentage.ShouldBe(13.67m);
    }

    #endregion

    #region Helper Methods

    private async Task<ProvidentFundDto> CreateTestProvidentFundAsync(
        string name,
        string code,
        decimal employeePercentage = 12,
        decimal employerPercentage = 12)
    {
        var input = new CreateUpdateProvidentFundDto
        {
            Name = name,
            Code = code,
            EmployeePercentage = employeePercentage,
            EmployerPercentage = employerPercentage
        };

        return await _providentFundAppService.CreateAsync(input);
    }

    #endregion
}
