using System;
using System.Collections.Generic;
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

namespace ShikshaMitra.Finance.FeeStructureAssignment;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class StudentFeeAssignmentApi_Tests : ShikshaMitraApiTestBase
{
    private const string AssignUrl        = "/api/student-fee-structure/assign";
    private const string FeeStructuresUrl = "/api/fee-structures";
    private const string MetadataUrl      = "/api/fee-structures-metadata";
    private const string CollectFormUrl   = "/api/v2/fee-collections";

    public StudentFeeAssignmentApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
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

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves the first FeeStructureMetadata ID for the seeded academic year.
    /// The fee-collection seeder creates metadata as part of its setup.
    /// </summary>
    private async Task<Guid> GetOrCreateFeeStructureMetadataIdAsync()
    {
        var academicYearId = Factory.SeededAcademicYearId;

        var listResponse = await GetAndReadAsync<FeeStructureMetadataListResponse>(
            $"{MetadataUrl}?academicYearId={academicYearId}");

        if (listResponse.Items?.Count > 0)
            return listResponse.Items[0].Id;

        // Fall back: generate metadata if seeder hasn't created it yet
        var generateResponse = await Client.PostAsync(
            $"{MetadataUrl}/generate?academicYearId={academicYearId}", null);
        generateResponse.EnsureSuccessStatusCode();

        var listAfter = await GetAndReadAsync<FeeStructureMetadataListResponse>(
            $"{MetadataUrl}?academicYearId={academicYearId}");

        listAfter.Items.ShouldNotBeEmpty("Fee structure metadata should have been generated.");
        return listAfter.Items[0].Id;
    }

    /// <summary>
    /// Creates a fee structure with a single recurring fee head for the seeded standard.
    /// Returns the fee structure ID.
    /// </summary>
    private async Task<Guid> CreateFeeStructureAsync(Guid feeStructureMetadataId, Guid feeHeadId)
    {
        var request = new StandardFeeStructureSaveRequest
        {
            StandardId = Factory.SeededStandardId,
            StandardFeeStructures = new StandardFeeStructureRequest
            {
                Name = $"Test Structure {Guid.NewGuid():N}"[..24],
                FeeHeads =
                [
                    new FeeHeadAssignmentRequest
                    {
                        FeeHeadId    = feeHeadId,
                        Amount       = 12_000d,
                        IsOneTime    = false,
                        IsOptional   = false,
                        IsRefundable = true,
                    },
                ],
            },
        };

        var response = await Client.PostAsJsonAsync(
            $"{FeeStructuresUrl}?feeStructureMetadataId={feeStructureMetadataId}",
            request, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FeeStructureCreateResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Null FeeStructureCreateResponse");

        return result.Id;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that assigning a fee structure to a student creates the StudentFeeStructure
    /// record and DueFee records so that the collect form returns a populated ledger.
    /// </summary>
    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Assign_Fee_Structure_And_Collect_Form_Shows_Ledger()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId      = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var metadataId     = await GetOrCreateFeeStructureMetadataIdAsync();
        var feeStructureId = await CreateFeeStructureAsync(metadataId, feeHeadId);

        var assignResponse = await Client.PostAsJsonAsync(AssignUrl, new
        {
            studentId,
            standardFeeStructureId    = feeStructureId,
            selectedOptionalFeeHeadIds = Array.Empty<Guid>(),
        }, JsonOptions);

        assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var form = await GetAndReadAsync<FeeCollectFormDto>(
            $"{CollectFormUrl}/{studentId}/collect-form?academicYearId={Factory.SeededAcademicYearId}");

        form.ShouldNotBeNull();
        form.Ledger.ShouldNotBeNull();
        form.Ledger.ShouldNotBeEmpty(
            "Collect form ledger must have monthly entries after fee-structure assignment.");
        form.Summary.TotalFee.ShouldBeGreaterThan(0d,
            "TotalFee must reflect the assigned fee structure amount.");
        form.Summary.PendingFee.ShouldBeGreaterThan(0d,
            "PendingFee must be non-zero before any payment.");
    }

    /// <summary>
    /// Verifies that assigning a fee structure twice on the same student is rejected.
    /// </summary>
    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Reject_Duplicate_Fee_Structure_Assignment()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId      = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var metadataId     = await GetOrCreateFeeStructureMetadataIdAsync();
        var feeStructureId = await CreateFeeStructureAsync(metadataId, feeHeadId);

        var assignBody = new
        {
            studentId,
            standardFeeStructureId    = feeStructureId,
            selectedOptionalFeeHeadIds = Array.Empty<Guid>(),
        };

        var first = await Client.PostAsJsonAsync(AssignUrl, assignBody, JsonOptions);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await Client.PostAsJsonAsync(AssignUrl, assignBody, JsonOptions);
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest,
            "Re-assigning a fee structure to the same student must be rejected.");
    }

    /// <summary>
    /// Verifies that the student fee-details endpoint returns fee-head summaries with the
    /// correct total and pending amounts after a fee structure is assigned.
    /// </summary>
    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Fee_Head_Summaries_After_Assignment()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId      = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var metadataId     = await GetOrCreateFeeStructureMetadataIdAsync();
        var feeStructureId = await CreateFeeStructureAsync(metadataId, feeHeadId);

        await Client.PostAsJsonAsync(AssignUrl, new
        {
            studentId,
            standardFeeStructureId    = feeStructureId,
            selectedOptionalFeeHeadIds = Array.Empty<Guid>(),
        }, JsonOptions);

        var feeDetails = await GetAndReadAsync<StudentFeeDetailsDto>(
            $"{CollectFormUrl}/{studentId}/fee-details?academicYearId={Factory.SeededAcademicYearId}");

        feeDetails.ShouldNotBeNull();
        feeDetails.FeeHeads.ShouldNotBeNull();
        feeDetails.FeeHeads.ShouldNotBeEmpty("Fee heads must be present after assignment.");
        feeDetails.FeeOverview.TotalFee.ShouldBe(12_000d,
            "Total fee should equal the annual amount on the fee structure.");
        feeDetails.FeeOverview.PendingFee.ShouldBe(12_000d,
            "Full amount should be pending before any payment.");
    }
}
