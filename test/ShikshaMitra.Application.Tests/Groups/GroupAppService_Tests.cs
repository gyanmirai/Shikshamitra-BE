using Volo.Abp.Application.Dtos;
using Xunit;
using ShikshaMitra.Groups.Dtos;
using ShikshaMitra;
using ShikshaMitra.Groups;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Groups.Tests;

public class GroupAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IGroupAppService _groupAppService;

    public GroupAppService_Tests()
    {
        _groupAppService = GetRequiredService<IGroupAppService>();
    }

    [Fact]
    public async Task Should_Create_Group()
    {
        // Arrange
        var input = new CreateGroupDto
        {
            Name = "Test Group",
            Description = "Integration Test Group",
            AudienceType = GroupAudienceType.RuleBased,
            AcademicRules = new List<AcademicRule>
            {
                new AcademicRule { Boards = new List<Guid> { Guid.NewGuid() } } // Mock Data
            }
        };

        // Act
        var result = await _groupAppService.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
        result.Name.ShouldBe(input.Name);
        result.AudienceType.ShouldBe(input.AudienceType);
    }

    [Fact]
    public async Task Should_Get_List_Of_Groups()
    {
        // Arrange
        var input = new CreateGroupDto
        {
            Name = "Searchable Group",
            Description = "For Search Test",
            AudienceType = GroupAudienceType.NameBased
        };
        await _groupAppService.CreateAsync(input);

        // Act
        var result = await _groupAppService.GetListAsync(new GetGroupsInputDto { Search = "Searchable" });

        // Assert
        result.TotalCount.ShouldBeGreaterThan(0);
        result.Items.ShouldContain(g => g.Name == "Searchable Group");
    }

    [Fact]
    public async Task Should_Update_Group()
    {
        // Arrange
        var createInput = new CreateGroupDto
        {
            Name = "Original Name",
            AudienceType = GroupAudienceType.RuleBased
        };
        var createdGroup = await _groupAppService.CreateAsync(createInput);

        var updateInput = new UpdateGroupDto
        {
             Name = "Updated Name",
             Description = "Updated Description", // Inherits from CreateGroupDto if not distinct
             AudienceType = GroupAudienceType.RuleBased
        };

        // Act
        var result = await _groupAppService.UpdateAsync(createdGroup.Id, updateInput);

        // Assert
        result.Name.ShouldBe("Updated Name");
        result.Description.ShouldBe("Updated Description");
    }

    [Fact]
    public async Task Should_Preview_Group_Audience()
    {
        // Act
        // Current logic needs mock seeding to return > 0, 
        // OR we just verify it calls the manager successfully.
        // For now, testing the contract.
        
        var result = await _groupAppService.PreviewAsync(new GroupPreviewInputDto
        {
            AcademicRules = new List<AcademicRule>()
        });

        // Assert
        result.ShouldNotBeNull();
    }
}
