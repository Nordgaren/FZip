using System;
using System.IO;
using System.Text;
using SoulsFormats;
using System.Reflection;

namespace YAZip
{

    class Program
    {
        private static IProgress<(double value, string status)> Progress;

        static void Main(string[] args)
        {

            if (args.Length == 0)
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Console.WriteLine(
                    $"{assembly.GetName().Name} {assembly.GetName().Version.ToString().Replace(".0", "")}\n\n" +
                    "Drag and drop a folder onto the exe to bundle it,\n" +
                    "or a bundled file by drag and drop any bhd/bdt packed with YAZip.\n\n" +
                    "FromSoft game files were Not packed with this tool and will not be unpackable. \n\n" +
                    "Press any key to exit."
                    );
                Console.ReadKey();
                return;
            }

            //var watch = new System.Diagnostics.Stopwatch();
            //watch.Start();
            Progress = new Progress<(double value, string status)>(ProgressReport);

            foreach (var arg in args)
            {
                var writePath = Directory.GetParent(arg).ToString();
                string error;
                FileAttributes attr = File.GetAttributes(arg);
                string password = null;

                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    var encryptBHD = Confirm("Would you like to password protect these files?");
                    if (encryptBHD)
                    {
                        Console.Write("Please choose a password: ");
                        password = Console.ReadLine();
                    }

                    error = Packer.Pack(arg, writePath, password, Progress);
                    if (error != null)
                    {
                        Console.WriteLine(error);
                    }
                }
                else if (arg.EndsWith(".bhd") || arg.EndsWith(".bdt"))
                {
                    if (Unpacker.CheckEncrypted(arg.Replace(".bdt", ".bhd")))
                    {
                        Console.Write("File is Encrypted. What is the Password?: ");
                        password = Console.ReadLine();
                    }
                    error = Unpacker.Unpack(arg, password, null, Progress);
                    if (error != null)
                    {
                        Console.WriteLine(error);
                    }
                }
                
            }

            //watch.Stop();
            //Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
            Console.ReadLine();
        }

        /// <summary>
        /// Used for debugging
        /// </summary>
        /// <param name="writePath"></param>
        private static void BHDRead(string writePath)
        {
            BHD5 bhdReader;
            using (FileStream bhdStream = File.OpenRead(writePath + @"\DARK SOULS PREPARE TO DIE EDITION.bhd"))
            {
                bhdReader = BHD5.Read(bhdStream, BHD5.Game.DarkSouls3);
            }
        }

        private static void ProgressReport((double value, string message) obj)
        {
            var percent = obj.value * 100;
            Console.WriteLine($"{ (int)percent} %: {obj.message}");
        }

        /// <summary>
        /// Yes/No prompt
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public static bool Confirm(string title)
        {
            ConsoleKey response;
            do
            {
                Console.Write($"{ title } [y/n] ");
                response = Console.ReadKey(false).Key;
                if (response != ConsoleKey.Enter)
                {
                    Console.WriteLine();
                }
            } while (response != ConsoleKey.Y && response != ConsoleKey.N);

            return (response == ConsoleKey.Y);
        }
    }
}
