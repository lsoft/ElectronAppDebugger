global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using Extension.BLogic;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Threading;

namespace Extension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.VsixString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Electron App Debugger", "General", 0, 0, true, SupportsProfiles = true)]
    public sealed class VsixPackage : ToolkitPackage
    {
        public static VsixPackage Instance;

        private System.Threading.Tasks.Task<Catcher> _cather;

        public VsixPackage()
        {
            Instance = this;
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            _cather = Catcher.StartCatchAsync();

            //VS.Commands.InterceptAsync(
            //    VSConstants.VSStd97CmdID.Start,
            //    () =>
            //    {
            //        return CommandProgression.Stop;
            //    }
            //    ).FileAndForget("InterceptAsync");
        }
    }
}