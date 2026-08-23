using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Shell;
using OrchardCore.Entities;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace Crest.ViewModels;

public sealed record CrestLoginSettings(
    bool AllowRememberMe,
    bool AllowChangingUsername,
    bool AllowChangingEmail,
    bool AllowChangingPhoneNumber,
    bool UseSiteTheme,
    bool DisableLocalLogin,
    bool RequireTwoFactorAuthentication,
    bool AllowRememberClientTwoFactorAuthentication,
    int NumberOfRecoveryCodesToGenerate,
    bool UseSiteThemeForTwoFactorAuthentication,
    bool UseExternalProviderIfOnlyOneDefined,
    bool UseScriptToSyncProperties,
    string? SyncPropertiesScript)
{
    public static CrestLoginSettings From(LoginSettings login, TwoFactorLoginSettings twoFactor, ExternalLoginSettings external) => new(
        login.AllowRememberMe, login.AllowChangingUsername, login.AllowChangingEmail, login.AllowChangingPhoneNumber,
        login.UseSiteTheme, login.DisableLocalLogin,
        twoFactor.RequireTwoFactorAuthentication, twoFactor.AllowRememberClientTwoFactorAuthentication,
        twoFactor.NumberOfRecoveryCodesToGenerate, twoFactor.UseSiteTheme,
        external.UseExternalProviderIfOnlyOneDefined, external.UseScriptToSyncProperties, external.SyncPropertiesScript);

    public LoginSettings ToLoginSettings() => new()
    {
        AllowRememberMe = AllowRememberMe, AllowChangingUsername = AllowChangingUsername,
        AllowChangingEmail = AllowChangingEmail, AllowChangingPhoneNumber = AllowChangingPhoneNumber,
        UseSiteTheme = UseSiteTheme, DisableLocalLogin = DisableLocalLogin,
    };

    public TwoFactorLoginSettings ToTwoFactorLoginSettings() => new()
    {
        RequireTwoFactorAuthentication = RequireTwoFactorAuthentication,
        AllowRememberClientTwoFactorAuthentication = AllowRememberClientTwoFactorAuthentication,
        NumberOfRecoveryCodesToGenerate = NumberOfRecoveryCodesToGenerate,
        UseSiteTheme = UseSiteThemeForTwoFactorAuthentication,
    };

    public ExternalLoginSettings ToExternalLoginSettings() => new()
    {
        UseExternalProviderIfOnlyOneDefined = UseExternalProviderIfOnlyOneDefined,
        UseScriptToSyncProperties = UseScriptToSyncProperties,
        SyncPropertiesScript = SyncPropertiesScript,
    };
}
