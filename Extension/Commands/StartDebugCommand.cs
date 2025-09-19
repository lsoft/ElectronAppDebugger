namespace Extension
{
    [Command(PackageIds.StartDebugCommand)]
    internal sealed class StartDebugCommand : BaseCommand<StartDebugCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await VS.MessageBox.ShowWarningAsync("Vsix", "Button clicked");
        }
    }
}
