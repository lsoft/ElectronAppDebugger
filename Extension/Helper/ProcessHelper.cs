using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Helper
{
    public static class ProcessHelper
    {
        public static Process? FindChildProcessWithName(
            this Process process,
            string processName
            )
        {
            try
            {

                if (process.ProcessName == processName)
                {
                    return process;
                }

                foreach (var child in GetChildProcesses(process))
                {
                    try
                    {
                        var result = child.FindChildProcessWithName(processName);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                    catch
                    {
                        //suppress
                    }
                }
            }
            catch
            {
                //suppress
            }

            return null;
        }

        public static Process? FindChildProcessWithWindow(
            this Process process
            )
        {
            try
            {

                if (process.MainWindowHandle != default)
                {
                    return process;
                }

                foreach (var child in GetChildProcesses(process))
                {
                    try
                    {
                        var result = FindChildProcessWithWindow(child);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                    catch
                    {
                        //suppress
                    }
                }
            }
            catch
            {
                //suppress
            }

            return null;
        }

        /// <summary>
        /// Retrieves a list of child processes for a given parent process.
        /// </summary>
        /// <param name="parentProcess">The parent process.</param>
        /// <returns>A list of child processes.</returns>
        public static IList<Process> GetChildProcesses(this Process parentProcess)
        {
            if (parentProcess == null)
            {
                throw new ArgumentNullException(nameof(parentProcess));
            }

            // Query WMI for processes where ParentProcessID matches the parent's ID
            using var searcher = new ManagementObjectSearcher(
                $"Select * From Win32_Process Where ParentProcessID={parentProcess.Id}"
                );

            var childProcesses = new List<Process>();

            foreach (ManagementObject mo in searcher.Get())
            {
                try
                {
                    // Get the Process object by its ID
                    int processId = Convert.ToInt32(mo["ProcessID"]);
                    childProcesses.Add(Process.GetProcessById(processId));

                    mo.Dispose();
                }
                catch (ArgumentException)
                {
                    // Handle cases where a process might have exited between querying and retrieving
                    // This can happen if a child process terminates quickly
                }
            }

            return childProcesses;
        }
    }
}
