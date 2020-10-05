using Serilog;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Match_Verify
{
    /*
     *
     * Overzicht van de matches van Mix'n'Match: https://mix-n-match.toolforge.org/#/catalog/1827
     * Download: https://mix-n-match.toolforge.org/?#/download/1827
     */
    class Program
    {
        static async Task Main(string[] args)
        {
            AssemblyName app = Assembly.GetExecutingAssembly().GetName();
#if DEBUG
            string appVersion = $"{app.Name} [DEBUG] version {app.Version}";
#else
            string appVersion = $"{app.Name} version {app.Version}";
#endif

            string logBase = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "/var/log/";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File($"{logBase}{app.Name}-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Log.Logger.Information(appVersion);
            Log.Logger.Information(string.Empty);

            Console.Title = appVersion;
            Console.WriteLine(appVersion);
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  [FILENAME]         Input filename with Mix'n'match data.");
            Console.WriteLine();

            string inputFile = string.Empty;

            foreach (string arg in args)
            {
                string[] split = arg.Split('=', 2);

                switch (split[0].ToLower())
                {
                    default:
                        // Argument should be the filename of the mix'n'match dataset
                        if (string.IsNullOrEmpty(inputFile))
                        {
                            inputFile = arg;
                        }
                        break;
                }
            }

            // En nu op naar het echte werk.
            int exitCode = -1;
            try
            {
                // Vul de bijwerk/lever tabel en start de levering.
                Worker worker = new Worker(Log.Logger);

                if (await worker.Verify(inputFile))
                {
                    Console.WriteLine("Done.");
                    exitCode = 0;
                }
                else
                {
                    Console.WriteLine("Procedure stopped before finishing.");
                    exitCode = 1;
                }
            }
            catch (Exception e)
            {
                exitCode = 2;
                Log.Logger.Error(e, "Unhandled Exception");
                Console.WriteLine("Unhandled exception. Check log for details.");
            }

            Log.CloseAndFlush();
            Environment.Exit(exitCode);
        }
    }
}
