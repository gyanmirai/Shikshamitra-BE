using System;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Xunit;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings;

public class EducationBoard_Tests
{
	[Fact]
	public void Catalogue_contains_only_CBSE_and_GSEB_with_existing_database_values()
	{
		((int)EducationBoardType.CBSE).ShouldBe(0);
		((int)EducationBoardType.GSEB).ShouldBe(1);
		Enum.GetValues<EducationBoardType>().ShouldBe(new[] { EducationBoardType.CBSE, EducationBoardType.GSEB });
		EducationBoardNames.DisplayName(EducationBoardType.CBSE).ShouldBe("CBSE");
		EducationBoardNames.DisplayName(EducationBoardType.GSEB).ShouldBe("GSEB");
	}
	[Fact]
	public void Standard_board_names_cannot_be_overridden_by_custom_names()
	{
		var board = new EducationBoard(Guid.NewGuid(), Guid.NewGuid(), EducationBoardType.GSEB, Guid.NewGuid());
		board.SetCustomName("  Maharashtra Board  ");
		board.CustomName.ShouldBeNull();
		board.Name.ShouldBe("GSEB");
		board.SetEducationBoardType(EducationBoardType.CBSE).SetCustomName(null);
		board.ExtraProperties.ContainsKey("CustomName").ShouldBeFalse();
		board.Name.ShouldBe("CBSE");
	}
	[Theory]
	[InlineData(-1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(100)]
	public void Unsupported_board_types_are_rejected_by_creation_updates_and_name_validation(int value)
	{
		var type = (EducationBoardType)value;
		Should.Throw<BusinessException>(() => EducationBoardNames.NormalizeCustomName(type, "Regional Board")).Code.ShouldBe(EducationBoardNames.InvalidType);
		Should.Throw<BusinessException>(() => new EducationBoard(Guid.NewGuid(), Guid.NewGuid(), type, Guid.NewGuid())).Code.ShouldBe(EducationBoardNames.InvalidType);
		var board = new EducationBoard(Guid.NewGuid(), Guid.NewGuid(), EducationBoardType.CBSE, Guid.NewGuid());
		Should.Throw<BusinessException>(() => board.SetEducationBoardType(type)).Code.ShouldBe(EducationBoardNames.InvalidType);
		board.Type.ShouldBe(EducationBoardType.CBSE);
	}
}
