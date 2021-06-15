using System;
using System.IO;
using System.Security.Cryptography;

namespace YAZip
{
    public static class ChecksumUtil
    {


        //Modified https://makolyte.com/csharp-get-a-files-checksum-using-any-hashing-algorithm-md5-sha256/
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

        public static void CheckFiles()
        {
            var filePath = @"C:\Users\Tor\Desktop\DARK SOULS PREPARE TO DIE EDITION";
            var dsRepack = @"C:\Users\Tor\source\repos\YAZip\YAZip\bin\Debug\DARK SOULS PREPARE TO DIE EDITION";
            var ogFiles = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories);
            var reFiles = Directory.GetFiles(dsRepack, "*", SearchOption.AllDirectories);
            var good = 0;
            var bad = 0;
            for (int i = 0; i < ogFiles.Length; i++)
            {
                var same = ChecksumUtil.GetChecksum(ogFiles[i]) == ChecksumUtil.GetChecksum(reFiles[i]);

                if (same)
                    good++;

                if (!same)
                {
                    Console.WriteLine(reFiles[i]);
                    bad++;
                }
            }
            Console.WriteLine("Good:");
            Console.WriteLine(good);
            Console.WriteLine("Bad:");
            Console.WriteLine(bad);
        }
    }

}
