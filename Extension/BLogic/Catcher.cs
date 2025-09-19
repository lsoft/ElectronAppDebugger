using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using MSXML;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Extension.BLogic
{
    public sealed class Catcher
    {
        private readonly DTE2 _dte;
        private readonly CommandEvents _commandEvents;


        public Catcher(
            DTE2 dte
            )
        {
            // Важно сохранить _commandEvents в поле класса, иначе сборщик мусора его уничтожит!
            _dte = dte;

            _commandEvents = _dte.Events.CommandEvents;
            _commandEvents.BeforeExecute += OnBeforeExecute;
        }

        public static async Task<Catcher> StartCatchAsync()
        {
            var dte = await VsixPackage.Instance.GetServiceAsync(typeof(DTE)) as DTE2;
            return new Catcher(dte);
        }

        private void OnBeforeExecute(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var commandName = DetermineCommandName(guid, id);

            // Имя команды для F5 - "Debug.Start"
            if (commandName != "Debug.Start")
            {
                return;
            }

            var dte = VsixPackage.Instance.GetService<DTE, DTE2>();

            if (dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
            {
                return;
            }

            var aps = (object[])dte.ActiveSolutionProjects;
            if (aps is null || aps.Length != 1)
            {
                return;
            }
            var activeDteProject = (EnvDTE.Project)aps[0];

            var projectFileContent = System.IO.File.ReadAllText(
                activeDteProject.FullName
                );
            var doc = XDocument.Parse(projectFileContent);

            var packageReferences = doc.Descendants("PackageReference")
                .Select(pr => new
                {
                    PackageId = pr.Attribute("Include")?.Value,
                    Version = pr.Attribute("Version")?.Value
                })
                .Where(pr => pr.PackageId != null)
                .ToList();
            if (packageReferences.All(p => p.PackageId != "ElectronNET.API"))
            {
                return;
            }

            //отменить стандартное выполнение команды
            cancelDefault = true;

            Task.Run(
                async () =>
                {
                    var sessionController = await SessionController.CreateAsync(
                        activeDteProject
                        );
                    await sessionController.StartDebugSessionAsync();
                }).FileAndForget("Custom Debugging");
        }

        public static Guid GetProjectGuidFromDteProject(EnvDTE.Project dteProject, IServiceProvider sp)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsSolution = (IVsSolution)sp.GetService(typeof(SVsSolution));
            ErrorHandler.ThrowOnFailure(vsSolution.GetProjectOfUniqueName(dteProject.UniqueName, out IVsHierarchy hierarchy));
            ErrorHandler.ThrowOnFailure(vsSolution.GetGuidOfProject(hierarchy, out Guid guid));
            return guid;
        }

        private string? DetermineCommandName(
            string guid,
            int id
            )
        {
            // Можно проверять по имени, GUID или ID. Проверка по имени проще.
            try
            {
                // DTE может выбросить исключение, если команда не найдена, оборачиваем в try-catch
                var r =  _dte.Commands.Item(guid, id).Name;
                return r;
            }
            catch (Exception)
            {
                // Игнорируем
            }

            return null;
        }
    }
}
