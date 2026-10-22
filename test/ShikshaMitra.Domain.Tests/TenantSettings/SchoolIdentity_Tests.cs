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
	public void Organization_choices_override_medium_and_reset_restores_medium_inheritance()
	{
		var medium = new SchoolIdentityChoices { Banner = Custom() };
		var organization = new SchoolIdentityChoices { Banner = Custom() };
		SchoolIdentityResolver.Resolve(SchoolIdentityAssetKind.Banner, null, null, medium, organization)
			.Selection.ShouldBe(organization.Banner);
		SchoolIdentityResolver.Resolve(SchoolIdentityAssetKind.Banner, null, null, medium, new())
			.Selection.ShouldBe(medium.Banner);
		var hidden = new SchoolIdentityChoices { Banner = new() { Mode = SchoolIdentityAssetMode.Hidden } };
		SchoolIdentityResolver.Resolve(SchoolIdentityAssetKind.Banner, null, null, medium, hidden)
			.Selection.Mode.ShouldBe(SchoolIdentityAssetMode.Hidden);
	}

	[Fact]
	public void Organization_profile_requires_medium_ancestry_and_has_its_own_scope_key()
	{
		var organization = Guid.NewGuid();
		Should.Throw<BusinessException>(() => new SchoolIdentityProfile(Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), organizationId: organization));
		var profile = new SchoolIdentityProfile(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), organization);
		profile.ScopeKey.ShouldBe($"organization:{organization:N}");
		profile.OrganizationId.ShouldBe(organization);
		profile.ScopeKey.Length.ShouldBeLessThanOrEqualTo(96);
	}

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
		var png = SchoolIdentityTestImage.Png(32, 32);
		var result = SchoolIdentityImageValidation.Validate(SchoolIdentityAssetKind.Logo, png);
		result.ContentType.ShouldBe("image/png"); result.Width.ShouldBe(32); result.Height.ShouldBe(32);
		Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.Validate(SchoolIdentityAssetKind.Logo, "<svg></svg>"u8.ToArray()));
		Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.Validate(SchoolIdentityAssetKind.Signature, new byte[1024 * 1024 + 1]));
		BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), 20000);
		Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.Validate(SchoolIdentityAssetKind.Logo, png)).Code.ShouldBe("SchoolIdentity:ImageDimensionsInvalid");
	}

	[Theory]
	[InlineData(SchoolIdentityAssetKind.Logo, 32, 32)]
	[InlineData(SchoolIdentityAssetKind.Signature, 32, 32)]
	[InlineData(SchoolIdentityAssetKind.Banner, 1400, 300)]
	public void Upload_enforces_minimum_width_and_height_inclusively(SchoolIdentityAssetKind kind, int width, int height)
	{
		var result = SchoolIdentityImageValidation.Validate(kind, SchoolIdentityTestImage.Png(width, height));
		result.Width.ShouldBe(width);
		result.Height.ShouldBe(height);
		foreach (var bytes in new[] { SchoolIdentityTestImage.Png(width - 1, height), SchoolIdentityTestImage.Png(width, height - 1) })
		{
			var error = Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.Validate(kind, bytes));
			error.Code.ShouldBe(SchoolIdentityErrorCodes.ImageTooSmall);
			error.Data["MinimumWidth"].ShouldBe(width);
			error.Data["MinimumHeight"].ShouldBe(height);
		}
	}

	private static SchoolIdentitySelection Custom() => new() { Mode = SchoolIdentityAssetMode.Custom, AssetId = Guid.NewGuid() };

	[Theory]
	[InlineData(SchoolIdentityAssetKind.Logo)]
	[InlineData(SchoolIdentityAssetKind.Banner)]
	[InlineData(SchoolIdentityAssetKind.Signature)]
	public void Legacy_import_preserves_small_existing_images_but_still_decodes_the_content(SchoolIdentityAssetKind kind)
	{
		var png = SchoolIdentityTestImage.Png(1, 1);
		SchoolIdentityImageValidation.ValidateLegacy(kind, png).Width.ShouldBe(1);
		Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.Validate(kind, png)).Code.ShouldBe(SchoolIdentityErrorCodes.ImageTooSmall);
		Should.Throw<BusinessException>(() => SchoolIdentityImageValidation.ValidateLegacy(kind, "<svg/>"u8.ToArray())).Code.ShouldBe(SchoolIdentityErrorCodes.ImageInvalid);
	}
}
