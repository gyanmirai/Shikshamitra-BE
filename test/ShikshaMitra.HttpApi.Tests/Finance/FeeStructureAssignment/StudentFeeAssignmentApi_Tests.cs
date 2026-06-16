using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
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
    private const string GetByStudentUrl  = "/api/student-fee-structure/student";
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

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Student_Fee_Structure_Returns_Fee_Heads_After_Assignment()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var metadataId = await GetOrCreateFeeStructureMetadataIdAsync();
        var feeStructureId = await CreateFeeStructureAsync(metadataId, feeHeadId);

        await Client.PostAsJsonAsync(AssignUrl, new
        {
            studentId,
            standardFeeStructureId = feeStructureId,
            selectedOptionalFeeHeadIds = Array.Empty<Guid>(),
        }, JsonOptions);

        var response = await Client.GetAsync($"{GetByStudentUrl}/{studentId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<StudentFeeStructureResponseDto>(JsonOptions);
        body.ShouldNotBeNull();
        body!.FeeHeads.ShouldNotBeNull();
        body.FeeHeads.ShouldNotBeEmpty();
        body.FeeHeads.ShouldContain(h => h.FeeHeadId == feeHeadId);
        body.StandardFeeStructureId.ShouldBe(feeStructureId);
    }

    /// <summary>
    /// Verifies that assigning a fee structure with customized optional fee heads generates
    /// due fees matching the custom amounts, one-time months, and recurring ranges.
    /// </summary>
    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Assign_Fee_Structure_With_Custom_Optional_Fee_Heads()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var mainFeeHeadId    = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var optionalRecId    = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var optionalOneId    = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);

        var metadataId = await GetOrCreateFeeStructureMetadataIdAsync();

        // Create a fee structure with 1 regular recurring fee, 1 optional recurring, and 1 optional one-time
        var request = new StandardFeeStructureSaveRequest
        {
            StandardId = Factory.SeededStandardId,
            StandardFeeStructures = new StandardFeeStructureRequest
            {
                Name = $"Test Custom {Guid.NewGuid():N}"[..24],
                FeeHeads = new List<FeeHeadAssignmentRequest>
                {
                    new() { FeeHeadId = mainFeeHeadId, Amount = 12000d, IsOneTime = false, IsOptional = false },
                    new() { FeeHeadId = optionalRecId, Amount = 6000d, IsOneTime = false, IsOptional = true },
                    new() { FeeHeadId = optionalOneId, Amount = 2000d, IsOneTime = true, IsOptional = true }
                }
            }
        };

        var response = await Client.PostAsJsonAsync(
            $"{FeeStructuresUrl}?feeStructureMetadataId={metadataId}",
            request, JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<FeeStructureCreateResponse>(JsonOptions);
        var feeStructureId = result!.Id;

        // Perform flexible optional fee assignment
        var assignResponse = await Client.PostAsJsonAsync(AssignUrl, new AssignStudentFeeStructureDto
        {
            StudentId = studentId,
            StandardFeeStructureId = feeStructureId,
            SelectedOptionalFeeHeadIds = new List<Guid> { optionalRecId, optionalOneId },
            FeeHeadDetails = new List<StudentFeeHeadDetailDto>
            {
                new()
                {
                    FeeHeadId = optionalRecId,
                    Amount = 1500d, // 1500 per month
                    StartPayableMonth = MonthEnum.July,
                    StartPayableYear = 2026,
                    EndPayableMonth = MonthEnum.November,
                    EndPayableYear = 2026
                },
                new()
                {
                    FeeHeadId = optionalOneId,
                    Amount = 2500d, // Custom amount
                    PayableMonth = MonthEnum.August,
                    PayableYear = 2026
                }
            }
        }, JsonOptions);

        assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Fetch student's assigned fee structure and verify reconstructed values
        var structureResponse = await Client.GetAsync($"{GetByStudentUrl}/{studentId}");
        structureResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await structureResponse.Content.ReadFromJsonAsync<StudentFeeStructureResponseDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.FeeHeads.ShouldNotBeEmpty();

        var recHead = body.FeeHeads.Find(h => h.FeeHeadId == optionalRecId);
        recHead.ShouldNotBeNull();
        recHead.SelectedAmount.ShouldBe(1500m);
        recHead.StartPayableMonth.ShouldBe(MonthEnum.July);
        recHead.StartPayableYear.ShouldBe(2026);
        recHead.EndPayableMonth.ShouldBe(MonthEnum.November);
        recHead.EndPayableYear.ShouldBe(2026);

        var oneHead = body.FeeHeads.Find(h => h.FeeHeadId == optionalOneId);
        oneHead.ShouldNotBeNull();
        oneHead.SelectedAmount.ShouldBe(2500m);
        oneHead.PayableMonth.ShouldBe(MonthEnum.August);
        oneHead.PayableYear.ShouldBe(2026);

        // Get collect form ledger and verify totals
        var form = await GetAndReadAsync<FeeCollectFormDto>(
            $"{CollectFormUrl}/{studentId}/collect-form?academicYearId={Factory.SeededAcademicYearId}");
        
        form.ShouldNotBeNull();
        form.Ledger.ShouldNotBeNull();

        // 12000 (regular) + 5 * 1500 (optional recurring) + 2500 (optional one-time) = 22000
        form.Summary.TotalFee.ShouldBe(22000d);
    }
}
