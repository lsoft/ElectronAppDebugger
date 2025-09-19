using ElectronNET.API;
using ElectronNET.API.Entities;
using System.Runtime.InteropServices;

namespace BlazorElectronApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.UseElectron(args);
            builder.Services
                .AddElectron()
                ;

            // Add services to the container.
            builder.Services
                .AddRazorComponents()
                .AddInteractiveServerComponents()
                ;

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseAntiforgery();

            //app.UseRouting();

            app.MapRazorComponents<BlazorElectronApp.Components.App>()
                .AddInteractiveServerRenderMode()
                ;

            if (HybridSupport.IsElectronActive)
            {
                CreateElectronWindow();
            }

            app.Run();
        }

        private static async void CreateElectronWindow()
        {
            var window = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
            {
                Width = 1200,
                Height = 900,
                Show = false // Don't show the window until it's ready
            });

            // Once the window is ready to show, display it.
            // This prevents a blank white screen from appearing on startup.
            window.OnReadyToShow += () => window.Show();

            //window.WebContents.OpenDevTools();

            window.OnClosed += () =>
            {
                Console.WriteLine("window.OnClosed - Electron.App.Quit");
                
                // Завершаем приложение полностью
                Electron.App.Quit();
            };

            // Обрабатываем событие выхода из приложения
            Electron.App.WindowAllClosed += () =>
            {
                // Только для macOS: обычно приложение остаётся в фоне
                // Для Windows/Linux — выходим
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Console.WriteLine("Electron.App.WindowAllClosed - Electron.App.Quit");

                    Electron.App.Quit();
                }
            };
        }
    }
}
