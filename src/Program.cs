using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ForzaVinylPainting
{
    class Program
    {
        [DllImport("User32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static readonly int VK_F6 = 0x75; // Play
        private static readonly int VK_F7 = 0x76; // Pause
        private static readonly int VK_F8 = 0x77; // Exit

        static int _screenWidth;
        static int _screenHeight;

        private static bool _isRunning;

        async static Task Main(string[] args)
        {
            var configRoot = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var config = configRoot.GetRequiredSection("Settings").Get<Config>();

            _screenWidth = Screen.PrimaryScreen.Bounds.Width;
            _screenHeight = Screen.PrimaryScreen.Bounds.Height;

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("DISCLAIMER: This program should only be used to test out design ideas, I am not responsible for anything that could happen as a result of using it. Always abide by Forza rules.");
            Console.WriteLine("This program is provided as-is without any warranties of any kind. You are solely responsible for determining the appropriateness of using the program and assume any risks associated with it.");
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Press [F] then [Enter] to read the Forza Enforcement Guidelines (will open a webbrowser to the Forza Support site");
                Console.WriteLine("Press [Y] then [Enter] to accept");
                var input = Console.ReadLine().ToUpper();
                if (input == "F")
                {
                    var url = "https://support.forzamotorsport.net/hc/en-us/articles/360035563914-Forza-Enforcement-Guidelines";
                    Console.WriteLine($"Opening {url}");
                    OpenBrowser(url);
                }
                else if (input == "Y")
                {
                    break;
                }
            }

            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("This program will take control of your mouse and keyboard, you need to remember these shortcuts to use ingame.");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Start/Resume: [F6]");
            Console.WriteLine("Pause: [F7]");
            Console.WriteLine("Exit: [F8]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine("Which file do you want to load? (Save a JSON file from Geometrize into the Images folder");
            Console.WriteLine();

            var folder = Path.GetDirectoryName(Environment.ProcessPath) + @"\Images\";
            var files = Directory.GetFiles(folder, "*.json");

            while (true)
            {
                for (int f = 0; f < files.Length; f++)
                {
                    Console.WriteLine($"{f}: {Path.GetFileName(files[f])}");
                }
                Console.WriteLine("Which file?");

                var choice = Console.ReadLine();
                if (int.TryParse(choice, out int value) && value < files.Length)
                {
                    Console.WriteLine("Starting");
                    ListenForKeys();
                    await DoShapes(files[value], config);
                    break;
                }

                Console.WriteLine("u wot m8");
                Console.WriteLine();
            }
        }

        private static async Task DoShapes(string path, Config config)
        {
            var simulator = new Simulator(_screenWidth, _screenHeight, config);

            var shapes = ShapeData.FromFile(path);

            var minX = 0f;
            var minY = 0f;
            var maxX = _screenWidth / 2f;
            var maxY = _screenHeight / 2f;

            foreach (var item in shapes.shapes)
            {
                if (item.ShapeX < minX || item.ShapeX > maxX || item.ShapeY < minY || item.ShapeY > maxY)
                {
                    Console.WriteLine($"Error: Shape didn't fit in min {minX},{minY} and max {maxX},{maxY}, aborting");
                    return;
                }
            }

            var totalShapes = shapes.shapes.Length;

            _isRunning = false;
            Console.WriteLine("Follow these steps now:");
            Console.WriteLine("- Open the Forza Vinyl editor");
            Console.WriteLine("- Press OK to create a new layer");
            Console.WriteLine("- Select 'Apply a vinyl shape'");
            Console.WriteLine("- Select the circle (one down from the top left)");
            Console.WriteLine("- Choose any color");
            Console.WriteLine("- Press F6 to unpause and start the process");

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var processedShapes = 0;
            foreach (var item in shapes.shapes)
            {
                if (!_isRunning)
                {
                    Console.WriteLine("Paused, maximize Forza again then press F6 to start/resume");
                    while (!_isRunning) // Yes I hate myself
                    {
                        await Task.Delay(10);
                    }
                }

                if (item.ShapeType == ShapeType.Unknown)
                {
                    continue;
                }

                await simulator.ProcessShape(item);
                processedShapes++;

                var msPerItem = stopwatch.ElapsedMilliseconds / processedShapes;
                var minsLeft = (msPerItem * (totalShapes - processedShapes)) / 1000M / 60M;
                Console.Title = $"{processedShapes}/{totalShapes}, ETA: {(int)minsLeft}m, Time per item: {msPerItem}ms";
            }
        }

        protected static async Task ListenForKeys()
        {
            while (true)
            {
                await Task.Delay(10);

                if (IsKeyPressed(VK_F6))
                {
                    Console.WriteLine("Resuming");
                    _isRunning = true;
                }

                if (IsKeyPressed(VK_F7))
                {
                    Console.WriteLine("Will pause after current shape");
                    _isRunning = false;
                }

                if (IsKeyPressed(VK_F8))
                {
                    Environment.Exit(-1);
                }
            }
        }

        private static bool IsKeyPressed(int key)
        {
            var keyState = GetAsyncKeyState(key);
            return ((keyState >> 15) & 0x0001) == 0x0001;
        }

        // https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/
        public static void OpenBrowser(string url)
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
        }
    }
}
