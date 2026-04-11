using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

/// <summary>
/// End-to-End tests for Professional Tax App Service
/// </summary>
public class ProfessionalTaxAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IProfessionalTaxAppService _professionalTaxAppService;

    public ProfessionalTaxAppService_Tests()
    {
        _professionalTaxAppService = GetRequiredService<IProfessionalTaxAppService>();
    }

    #region Create Tests

    [Fact]
    public async Task Should_Create_Professional_Tax_Slab()
    {
        // Arrange
        var input = new CreateUpdateProfessionalTaxDto
        {
            GenderType = GenderType.Male,
            FromSalary = 0,
            ToSalary = 15000,
            TaxAmount = 0
        };

        // Act
        var result = await _professionalTaxAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.GenderType.ShouldBe(GenderType.Male);
        result.FromSalary.ShouldBe(0);
        result.ToSalary.ShouldBe(15000);
        result.TaxAmount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Create_Tax_Slab_For_Female()
    {
        // Arrange
        var input = new CreateUpdateProfessionalTaxDto
        {
            GenderType = GenderType.Female,
            FromSalary = 15001,
            ToSalary = 25000,
            TaxAmount = 150
        };

        // Act
        var result = await _professionalTaxAppService.CreateAsync(input);

        // Assert
        result.GenderType.ShouldBe(GenderType.Female);
        result.FromSalary.ShouldBe(15001);
        result.ToSalary.ShouldBe(25000);
        result.TaxAmount.ShouldBe(150);
    }

    [Fact]
    public async Task Should_Create_Multiple_Tax_Slabs()
    {
        // Arrange & Act — create a salary slab structure
        var slab1 = await CreateTestProfessionalTaxAsync(0, 15000, 0);
        var slab2 = await CreateTestProfessionalTaxAsync(15001, 25000, 150);
        var slab3 = await CreateTestProfessionalTaxAsync(25001, 999999, 200);

        // Assert
        slab1.TaxAmount.ShouldBe(0);
        slab2.TaxAmount.ShouldBe(150);
        slab3.TaxAmount.ShouldBe(200);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Should_Get_Professional_Tax_By_Id()
    {
        // Arrange
        var created = await CreateTestProfessionalTaxAsync(0, 10000, 100);

        // Act
        var result = await _professionalTaxAppService.GetAsync(created.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.FromSalary.ShouldBe(0);
        result.ToSalary.ShouldBe(10000);
    }

    [Fact]
    public async Task Should_Throw_When_Tax_Not_Found()
    {
        // Act & Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _professionalTaxAppService.GetAsync(Guid.NewGuid());
        });
    }

    [Fact]
    public async Task Should_Get_Paginated_List()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            await CreateTestProfessionalTaxAsync(i * 10000, (i + 1) * 10000, i * 50);
        }

        var input = new PagedAndSortedResultRequestDto
        {
            SkipCount = 0,
            MaxResultCount = 10
        };

        // Act
        var result = await _professionalTaxAppService.GetListAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeLessThanOrEqualTo(10);
        result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Should_Update_Professional_Tax()
    {
        // Arrange
        var created = await CreateTestProfessionalTaxAsync(0, 10000, 100);

        var updateInput = new CreateUpdateProfessionalTaxDto
        {
            GenderType = GenderType.Female,
            FromSalary = 5000,
            ToSalary = 20000,
            TaxAmount = 200
        };

        // Act
        var result = await _professionalTaxAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.GenderType.ShouldBe(GenderType.Female);
        result.FromSalary.ShouldBe(5000);
        result.ToSalary.ShouldBe(20000);
        result.TaxAmount.ShouldBe(200);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Should_Delete_Professional_Tax()
    {
        // Arrange
        var created = await CreateTestProfessionalTaxAsync(0, 10000, 100);

        // Act
        await _professionalTaxAppService.DeleteAsync(created.Id);

        // Assert
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(async () =>
        {
            await _professionalTaxAppService.GetAsync(created.Id);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Should_Handle_Zero_Tax_Amount()
    {
        // Arrange — first slab typically has zero tax
        var result = await CreateTestProfessionalTaxAsync(0, 15000, 0);

        // Assert
        result.TaxAmount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Handle_Large_Salary_Range()
    {
        // Arrange
        var result = await CreateTestProfessionalTaxAsync(100000, 9999999, 2500);

        // Assert
        result.FromSalary.ShouldBe(100000);
        result.ToSalary.ShouldBe(9999999);
        result.TaxAmount.ShouldBe(2500);
    }

    #endregion

    #region Helper Methods

    private async Task<ProfessionalTaxDto> CreateTestProfessionalTaxAsync(
        double fromSalary,
        double toSalary,
        double taxAmount,
        GenderType genderType = GenderType.Male)
    {
        var input = new CreateUpdateProfessionalTaxDto
        {
            GenderType = genderType,
            FromSalary = fromSalary,
            ToSalary = toSalary,
            TaxAmount = taxAmount
        };

        return await _professionalTaxAppService.CreateAsync(input);
    }

    #endregion
}
