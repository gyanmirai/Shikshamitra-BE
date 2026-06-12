using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.FeeStructureAssignment;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeStructureApi_Tests : ShikshaMitraApiTestBase
{
    private const string FeeStructuresUrl = "/api/fee-structures";
    private const string MetadataUrl      = "/api/fee-structures-metadata";

    public FeeStructureApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
        ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        ConcessionRequestTestHelper.DefaultMediumId       = factory.SeededMediumId;
        ConcessionRequestTestHelper.DefaultStandardId     = factory.SeededStandardId;
        ConcessionRequestTestHelper.DefaultDivisionId     = factory.SeededDivisionId;
        FeeCollectionTestHelper.DefaultAcademicYearId     = factory.SeededAcademicYearId;
        FeeCollectionTestHelper.DefaultMediumId           = factory.SeededMediumId;
        FeeCollectionTestHelper.DefaultStandardId         = factory.SeededStandardId;
        FeeCollectionTestHelper.DefaultDivisionId         = factory.SeededDivisionId;
    }

    private async Task<Guid> GetOrCreateFeeStructureMetadataIdAsync()
    {
        var academicYearId = Factory.SeededAcademicYearId;

        var listResponse = await GetAndReadAsync<FeeStructureMetadataListResponse>(
            $"{MetadataUrl}?academicYearId={academicYearId}");

        if (listResponse.Items?.Count > 0)
            return listResponse.Items[0].Id;

        var generateResponse = await Client.PostAsync(
            $"{MetadataUrl}/generate?academicYearId={academicYearId}", null);
        generateResponse.EnsureSuccessStatusCode();

        var listAfter = await GetAndReadAsync<FeeStructureMetadataListResponse>(
            $"{MetadataUrl}?academicYearId={academicYearId}");

        listAfter.Items.ShouldNotBeEmpty("Fee structure metadata should have been generated.");
        return listAfter.Items[0].Id;
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Enforce_Null_PayableMonth_When_IsOneTime_Is_False()
    {
        // Arrange
        var feeHeadId      = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var metadataId     = await GetOrCreateFeeStructureMetadataIdAsync();

        var request = new StandardFeeStructureSaveRequest
        {
            StandardId = Factory.SeededStandardId,
            StandardFeeStructures = new StandardFeeStructureRequest
            {
                Name = $"Test Enforce Null {Guid.NewGuid():N}"[..24],
                FeeHeads =
                [
                    new FeeHeadAssignmentRequest
                    {
                        FeeHeadId    = feeHeadId,
                        Amount       = 12_000d,
                        IsOneTime    = false, // Recurring fee
                        IsOptional   = false,
                        PayableMonth = MonthEnum.April, // Attempt to set PayableMonth
                        PayableYear  = 2026            // Attempt to set PayableYear
                    },
                ],
            },
        };

        // Act - Create the fee structure
        var response = await Client.PostAsJsonAsync(
            $"{FeeStructuresUrl}?feeStructureMetadataId={metadataId}",
            request, JsonOptions);
        response.EnsureSuccessStatusCode();

        // Act - Fetch the fee structure details
        var getResponse = await GetAndReadAsync<StandardFeeStructureResponse>(
            $"{FeeStructuresUrl}?standardId={Factory.SeededStandardId}");

        // Assert
        getResponse.ShouldNotBeNull();
        var structureItem = getResponse.StandardFeeStructures.FirstOrDefault(x => x.Name == request.StandardFeeStructures.Name);
        structureItem.ShouldNotBeNull();
        structureItem.FeeHeads.ShouldNotBeEmpty();

        var feeHead = structureItem.FeeHeads.FirstOrDefault(fh => fh.FeeHeadId == feeHeadId);
        feeHead.ShouldNotBeNull();
        feeHead.IsOneTime.ShouldBeFalse();
        feeHead.PayableMonth.ShouldBeNull();
        feeHead.PayableYear.ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Keep_PayableMonth_When_IsOneTime_Is_True()
    {
        // Arrange
        var feeHeadId      = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var metadataId     = await GetOrCreateFeeStructureMetadataIdAsync();

        var request = new StandardFeeStructureSaveRequest
        {
            StandardId = Factory.SeededStandardId,
            StandardFeeStructures = new StandardFeeStructureRequest
            {
                Name = $"Test Keep Month {Guid.NewGuid():N}"[..24],
                FeeHeads =
                [
                    new FeeHeadAssignmentRequest
                    {
                        FeeHeadId    = feeHeadId,
                        Amount       = 3_000d,
                        IsOneTime    = true, // One-time fee
                        IsOptional   = false,
                        PayableMonth = MonthEnum.June,
                        PayableYear  = 2026
                    },
                ],
            },
        };

        // Act - Create the fee structure
        var response = await Client.PostAsJsonAsync(
            $"{FeeStructuresUrl}?feeStructureMetadataId={metadataId}",
            request, JsonOptions);
        response.EnsureSuccessStatusCode();

        // Act - Fetch the fee structure details
        var getResponse = await GetAndReadAsync<StandardFeeStructureResponse>(
            $"{FeeStructuresUrl}?standardId={Factory.SeededStandardId}");

        // Assert
        getResponse.ShouldNotBeNull();
        var structureItem = getResponse.StandardFeeStructures.FirstOrDefault(x => x.Name == request.StandardFeeStructures.Name);
        structureItem.ShouldNotBeNull();
        structureItem.FeeHeads.ShouldNotBeEmpty();

        var feeHead = structureItem.FeeHeads.FirstOrDefault(fh => fh.FeeHeadId == feeHeadId);
        feeHead.ShouldNotBeNull();
        feeHead.IsOneTime.ShouldBeTrue();
        feeHead.PayableMonth.ShouldBe(MonthEnum.June);
        feeHead.PayableYear.ShouldBe(2026);
    }
}
