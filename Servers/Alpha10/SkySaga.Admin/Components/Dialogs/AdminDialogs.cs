using MudBlazor;
using SkySaga.Admin.Services;
using SkySaga.Data;

namespace SkySaga.Admin.Components.Dialogs;

public static class AdminDialogs
{
    private static readonly DialogOptions Options = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, BackgroundClass = "dialog-backdrop" };

    public static async Task<bool> EditPremiumAsync(this IDialogService dialogs, AccountActions actions, ISnackbar snackbar,
        Guid accountId, string username, DateTimeOffset? current)
    {
        var parameters = new DialogParameters<PremiumDialog>
        {
            { d => d.Username, username },
            { d => d.Current, current }
        };
        var dialog = await dialogs.ShowAsync<PremiumDialog>("VIP status", parameters, Options);

        if (await dialog.Result is not { Canceled: false, Data: PremiumDialog.Choice choice })
            return false;

        await actions.SetPremiumAsync(accountId, choice.Until);
        snackbar.Add(choice.Until is null ? $"VIP of {username} removed"
            : Forever.Is(choice.Until) ? $"{username} is VIP forever"
            : $"{username} is VIP until {Format.Until(choice.Until)}", Severity.Success);

        return true;
    }

    public static async Task<bool> BanAsync(this IDialogService dialogs, AccountActions actions, ISnackbar snackbar, Guid accountId, string username)
    {
        var parameters = new DialogParameters<BanDialog> { { d => d.Username, username } };
        var dialog = await dialogs.ShowAsync<BanDialog>("Ban", parameters, Options);

        if (await dialog.Result is not { Canceled: false, Data: BanDialog.Choice choice })
            return false;

        await actions.SetBanAsync(accountId, choice.Until, choice.Reason);
        snackbar.Add(Forever.Is(choice.Until) ? $"{username} banned permanently" : $"{username} banned until {Format.Until(choice.Until)}", Severity.Warning);

        return true;
    }
}
