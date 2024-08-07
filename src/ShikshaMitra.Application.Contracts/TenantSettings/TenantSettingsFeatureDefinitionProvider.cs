using ShikshaMitra.Localization;
using Volo.Abp.Features;
using Volo.Abp.Localization;

namespace ShikshaMitra.Onboarding;

public class TenantSettingsFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        FeatureGroupDefinition tenantSettingsGroup =
            context.AddGroup("TenantSettings",
                displayName: LocalizableString.Create<ShikshaMitraResource>("Features:TenantSettings"));

        tenantSettingsGroup.AddFeature("TenantSettings.Onboarding", defaultValue: "true",
            displayName: LocalizableString.Create<ShikshaMitraResource>("Features:TenantSettings.Onboarding"));
    }
}