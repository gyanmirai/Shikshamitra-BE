using System;
using System.Buffers.Binary;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class SchoolIdentity_Tests
{
	[Fact]
	public void Overrides_resolve_independently_and_preserve_the_signature_pair()
	{
		var logo = Custom(); var banner = Custom(); var signature = Custom() with { PrincipalName = "Principal A", Designation = "Principal" };
		var defaults = new SchoolIdentityChoices { Logo = logo, Banner = Custom(), Signature = Custom() };
		var board = new SchoolIdentityChoices { Banner = banner };
		var medium = new SchoolIdentityChoices { Signature = signature };
		SchoolIdentityResolver.Resolve(SchoolIdentityAssetKind.Logo, defaults, board, medium).Selection.ShouldBe(logo);
		SchoolIdentityResolver.Resolve(SchoolIdentityAssetKind.Banner, defaults, board, medium).Selection.ShouldBe(banner);
		var result = SchoolIdentityResolver.Resolve(SchoolIdentityAssetKind.Signature, defaults, board, medium);
		result.Selection.ShouldBe(signature); result.Source.ShouldBe("Medium override");
	}

	[Theory]
	[InlineData(SchoolIdentityAssetKind.Logo)]
	[InlineData(SchoolIdentityAssetKind.Banner)]
	[InlineData(SchoolIdentityAssetKind.Signature)]
	public void Hidden_stops_fallback_and_reset_restores_it(SchoolIdentityAssetKind kind)
	{
		var defaults = new SchoolIdentityChoices { Logo = Custom(), Banner = Custom(), Signature = Custom() };
		var hidden = new SchoolIdentityChoices
		{
			Logo = new() { Mode = SchoolIdentityAssetMode.Hidden },
			Banner = new() { Mode = SchoolIdentityAssetMode.Hidden },
			Signature = new() { Mode = SchoolIdentityAssetMode.Hidden }
		};
		SchoolIdentityResolver.Resolve(kind, defaults, null, hidden).Selection.Mode.ShouldBe(SchoolIdentityAssetMode.Hidden);
		SchoolIdentityResolver.Resolve(kind, defaults, null, new()).Selection.ShouldBe(defaults.Get(kind));
	}

	[Fact]
	public void Updating_defaults_does_not_replace_a_custom_medium_logo()
	{
		var medium = new SchoolIdentityChoices { Logo = Custom() };
		SchoolIdentityResolver.Resolve(SchoolIdentityAssetKind.Logo, new() { Logo = Custom() }, null, medium).Selection.ShouldBe(medium.Logo);
	}

	[Fact]
	public void Stale_edits_are_rejected_and_choices_roundtrip()
	{
		var profile = new SchoolIdentityProfile(Guid.NewGuid(), Guid.NewGuid());
		var stamp = profile.ConcurrencyStamp;
		var choices = new SchoolIdentityChoices { Logo = Custom() };
		profile.Replace(choices, stamp); profile.Choices.ShouldBe(choices);
		Should.Throw<AbpDbConcurrencyException>(() => profile.Replace(new(), "stale-version"));
		profile.Choices.ShouldBe(choices);
	}

	[Fact]
	public void Invalid_contexts_and_incomplete_signature_pairs_are_rejected()
	{
		Should.Throw<BusinessException>(() => new SchoolIdentityProfile(Guid.NewGuid(), Guid.NewGuid(), mediumId: Guid.NewGuid()));
		Should.Throw<BusinessException>(() => new SchoolIdentitySelection { Mode = SchoolIdentityAssetMode.Custom }.Validated(SchoolIdentityAssetKind.Logo));
		Should.Throw<ArgumentException>(() => Custom().Validated(SchoolIdentityAssetKind.Signature));
		Should.Throw<BusinessException>(() => (Custom() with { Mode = SchoolIdentityAssetMode.Inherit }).Validated(SchoolIdentityAssetKind.Logo));
	}

	[Fact]
	public void Scope_keys_distinguish_the_same_medium_id_under_different_boards()
	{
		var medium = Guid.NewGuid();
		SchoolIdentityProfile.Key(Guid.NewGuid(), medium).ShouldNotBe(SchoolIdentityProfile.Key(Guid.NewGuid(), medium));
		SchoolIdentityProfile.Key(null, null).ShouldBe("school");
	}

	[Fact]
	public void Upload_validation_decodes_real_images_and_rejects_spoofed_or_oversized_content()
	{
		var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aRZkAAAAASUVORK5CYII=");
		var result = SchoolIdentityImageValidation.Validate(SchoolIdentityAssetKind.Logo, png);
		result.ContentType.ShouldBe("image/png"); result.Width.ShouldBe(1); result.Height.ShouldBe(1);
		Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.Validate(SchoolIdentityAssetKind.Logo, "<svg></svg>"u8.ToArray()));
		Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.Validate(SchoolIdentityAssetKind.Signature, new byte[1024 * 1024 + 1]));
		BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), 20000);
		Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.Validate(SchoolIdentityAssetKind.Logo, png)).Code.ShouldBe("SchoolIdentity:ImageDimensionsInvalid");
	}

	private static SchoolIdentitySelection Custom() => new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = Guid.NewGuid() };
}
