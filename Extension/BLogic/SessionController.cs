using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using Extension.Helper;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Extension.BLogic
{
    public sealed class SessionController
    {
        private static int _running = 0;
        
        private readonly DTE _dte;
        private readonly EnvDTE.Project _startProject;
        private readonly Community.VisualStudio.Toolkit.OutputWindowPane _pane;
        private readonly EnvDTE.DebuggerEvents _debuggerEvents;

        private int _attached = 0;

        public int? RootProcessId
        {
            get;
            private set;
        }

        public SessionController(
            EnvDTE.DTE dte,
            EnvDTE.Project startProject,
            Community.VisualStudio.Toolkit.OutputWindowPane pane
            )
        {
            if (dte is null)
            {
                throw new ArgumentNullException(nameof(dte));
            }

            if (startProject is null)
            {
                throw new ArgumentNullException(nameof(startProject));
            }

            if (pane is null)
            {
                throw new ArgumentNullException(nameof(pane));
            }

            _dte = dte;
            _startProject = startProject;
            _pane = pane;

            _debuggerEvents = _dte.Events.DebuggerEvents;
            _debuggerEvents.OnEnterDesignMode += OnEnterDesignMode;
        }

        private void OnEnterDesignMode(dbgEventReason Reason)
        {
            // Отладка завершена — Stop или Detach

            if (!General.Instance.DetachStopApp)
            {
                return;
            }

            if(Interlocked.Exchange(ref _attached, 0) != 1)
            {
                return;
            }

            if (!RootProcessId.HasValue)
            {
                return;
            }

            Task.Run(
                async () =>
                {
                    try
                    {
                        var rootProcess = System.Diagnostics.Process.GetProcessById(RootProcessId.Value);

                        if (rootProcess.HasExited)
                        {
                            return;
                        }

                        var childProcessWithWindow = rootProcess.FindChildProcessWithWindow();
                        if (childProcessWithWindow is null)
                        {
                            return;
                        }

                        _ = childProcessWithWindow.CloseMainWindow();
                    }
                    catch (Exception excp)
                    {
                        await OutputExceptionAsync(excp);
                    }
                }).FileAndForget("Close Electron.NET main window");
        }

        public static async Task<SessionController> CreateAsync(
            EnvDTE.Project startProject
            )
        {
            var pane = await VS.Windows.GetOutputWindowPaneAsync(Community.VisualStudio.Toolkit.Windows.VSOutputWindowPane.Debug);
            await pane.ActivateAsync();

            var dte = VsixPackage.Instance.GetService<DTE, DTE2>();

            return new SessionController(dte, startProject, pane);
        }

        public async Task StartDebugSessionAsync(
            
            )
        {
            if (Interlocked.Exchange(ref _running, 1) != 0)
            {
                await VS.MessageBox.ShowErrorAsync("Electron.NET debugging session is running already");
                return;
            }

            try
            {
                await ProcessAsync();
            }
            finally
            {
                _ = Interlocked.Exchange(ref _running, 0);
            }
        }

        private async Task ProcessAsync()
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await OutputLogAsync(
                    "Starting Electron.NET debugging..."
                    );

                var projectFolder = new FileInfo(_startProject.FullName).Directory.FullName;

                var assemblyName = DetermineTargetProcessName();

                await RunAsync(
                    assemblyName,
                    projectFolder,
                    async processWithWindow =>
                    {
                        await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        foreach (EnvDTE.Process p in _dte.Debugger.LocalProcesses)
                        {
                            if (p.ProcessID == processWithWindow.Id)
                            {
                                p.Attach(); // default engines / auto-detect
                                _attached = 1;
                                break;
                            }
                        }
                    }
                    );
            }
            catch (Exception excp)
            {
                await OutputExceptionAsync(excp);
            }
            finally
            {
                await OutputLogAsync(
                    "Electron.NET debugging session stopped."
                    );
            }
        }

        private async Task OutputExceptionAsync(Exception excp)
        {
            await OutputLogAsync(excp.Message);
            await OutputLogAsync(excp.StackTrace);
        }

        private string DetermineTargetProcessName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var assemblyNameProperty = _startProject.Properties.Item("AssemblyName");
            var assemblyName = assemblyNameProperty?.Value?.ToString() ?? _startProject.Name;
            return assemblyName;
        }

        private async Task RunAsync(
            string projectName,
            string projectFolder,
            Func<System.Diagnostics.Process, Task> attach
            )
        {
            await TaskScheduler.Default;

            using (var rootProcess = new System.Diagnostics.Process())
            {
                rootProcess.StartInfo.WorkingDirectory = projectFolder;
                rootProcess.StartInfo.FileName = "electronize";
                rootProcess.StartInfo.Arguments = "start";
                rootProcess.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                rootProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                //rootProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                rootProcess.StartInfo.CreateNoWindow = true;

                rootProcess.StartInfo.UseShellExecute = false;
                rootProcess.StartInfo.RedirectStandardInput = true;
                rootProcess.StartInfo.RedirectStandardOutput = true;
                rootProcess.StartInfo.RedirectStandardError = true;
                rootProcess.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (e is null || string.IsNullOrEmpty(e.Data))
                    {
                        return;
                    }

                    OutputLog(e.Data, "    ");
                });

                var startResult = rootProcess.Start();

                RootProcessId = rootProcess.Id;

                rootProcess.BeginErrorReadLine();

                using var outputCancellation = new CancellationTokenSource();
                var outputTask = Task.Run(async () => await OutputAsync(rootProcess, outputCancellation.Token));

                while (!rootProcess.HasExited)
                {
                    var processWithWindow = rootProcess.FindChildProcessWithName(projectName);
                    if (processWithWindow is not null)
                    {
                        await attach(processWithWindow);
                        break;
                    }
                }

                rootProcess.WaitForExit();

                outputCancellation.Cancel();
                await outputTask;
            }
        }

        private async Task OutputAsync(
            System.Diagnostics.Process process,
            CancellationToken token
            )
        {
            while (!process.HasExited && !token.IsCancellationRequested)
            {
                try
                {
                    var outputLine = await process.StandardOutput.ReadLineAsync();
                    if (!string.IsNullOrEmpty(outputLine))
                    {
                        await OutputLogAsync(outputLine, "    ");
                    }
                }
                catch (OperationCanceledException)
                {
                    //that's fine
                }
                catch
                {
                    //nothing to do
                }
            }
        }

        #region output

        private void OutputLog(
            string msg,
            string? prefix = null
            )
        {
            _pane.WriteLine(
                DateTime.Now.ToString() + ": " + (prefix ?? string.Empty) + msg
                );
        }

        private async Task OutputLogAsync(
            string msg,
            string? prefix = null
            )
        {
            await _pane.WriteLineAsync(
                DateTime.Now.ToString() + ": " + (prefix ?? string.Empty) + msg
                );
        }

        #endregion
    }

}
