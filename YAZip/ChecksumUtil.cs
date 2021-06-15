using System;
using System.IO;
using System.Security.Cryptography;

namespace YAZip
{
    public static class ChecksumUtil
    {

        //Modified https://makolyte.com/csharp-get-a-files-checksum-using-any-hashing-algorithm-md5-sha256/
        /// <summary>
        /// Gets SHA256 hash from file.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string GetChecksum(string filename)
        {
            using (var hasher = HashAlgorithm.Create("SHA256"))
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = hasher.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Checks all files in source directory vs all filesin dest directory
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        public static void CheckFiles(string sourcePath, string destPath)
        {
            var ogFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            var reFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories);
            var good = 0;
            var bad = 0;
            for (int i = 0; i < ogFiles.Length; i++)
            {
                Console.Write($"Checking source: {ogFiles[i].Replace(sourcePath, "")} vs dest: {reFiles[i].Replace(destPath, "")}\r");

                var same = GetChecksum(ogFiles[i]) == GetChecksum(reFiles[i]);
                if (same)
                {
                    Console.Write($"Checking source: {ogFiles[i].Replace(sourcePath, "")} vs dest: {reFiles[i].Replace(destPath, "")}");
                    Console.Write(" Okay\n", Console.ForegroundColor = ConsoleColor.Green);
                    Console.ForegroundColor = ConsoleColor.White;
                    good++;
                }

                if (!same)
                {
                    Console.Write($"Checking {Path.GetFileName(ogFiles[i])} vs {Path.GetFileName(reFiles[i])}");
                    Console.Write(" Mismatch\n", Console.ForegroundColor = ConsoleColor.Red);
                    Console.ForegroundColor = ConsoleColor.White;
                    bad++;
                }
            }

            Console.WriteLine($"Match: {good}");
            Console.WriteLine($"Mismatch: {bad}");
        }
    }

}
