using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class RoomImage_Tests
{
	private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];

	[Fact]
	public void Supported_images_have_a_server_derived_content_type()
	{
		RoomImageValidation.Validate("room.PNG", Png).ShouldBe("image/png");
		RoomImageValidation.Validate("room.jpeg", [255, 216, 255, 224, 0, 0, 0, 0, 0, 0, 0, 0]).ShouldBe("image/jpeg");
	}

	[Theory]
	[InlineData("room.svg")]
	[InlineData("room.gif")]
	[InlineData("room.webp")]
	[InlineData("room.html")]
	public void Unsupported_formats_are_rejected(string name)
	{
		Should.Throw<UserFriendlyException>(() => RoomImageValidation.Validate(name, Png));
	}

	[Fact]
	public void Empty_oversized_disguised_and_mismatched_images_are_rejected()
	{
		Should.Throw<BusinessException>(() => RoomImageValidation.Validate("room.png", []));
		Should.Throw<BusinessException>(() => RoomImageValidation.Validate("room.png", new byte[5 * 1024 * 1024 + 1]));
		Should.Throw<BusinessException>(() => RoomImageValidation.Validate("room.png", [60, 104, 116, 109, 108]));
		Should.Throw<BusinessException>(() => RoomImageValidation.Validate("room.jpg", Png));
	}
}
