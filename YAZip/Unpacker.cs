using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YAZip
{
    /// <summary>
    /// Modified from UXM
    /// </summary>
    static class Unpacker
    {
        private const int WRITE_LIMIT = 1024 * 1024 * 100;

        /// <summary>
        /// Unpacks targeted directory to specificed folder. Unpacks to bhd/bdt folder if destination is null or whitespace
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="password">if null or whitspace, skips encrypting bhd file</param>
        /// <param name="destination">if null or whitespace, becomes filePath</param>
        /// <param name="progress"></param>
        /// <returns>error</returns>
        public static string Unpack(string filePath, string password, string destination, IProgress<(double value, string status)> progress)
        {
            progress.Report((0, "Preparing to unpack..."));
            string fileDir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(destination))
                destination = fileDir;

            string archive = Path.GetFileNameWithoutExtension(filePath);
            string key = "";

            string error = UnpackArchive(fileDir, archive, key, BHD5.Game.DarkSouls3, password, destination, progress).Result;
            if (error != null)
                return error;

            //unDCX the files if they are DCX'd
            var dcx = Directory.GetFiles($@"{destination}\{archive}", "*.dcx", SearchOption.AllDirectories);

            if (dcx.Length > 0)
            {
                var currentFile = 0;
                var fileCount = dcx.Length;

                progress.Report((((double)(0),
                                    $"Decompressing files...")));
                foreach (var file in dcx)
                {
                    currentFile++;
                    progress.Report((((double)(currentFile) / fileCount),
                                    $"Unpacking {Path.GetFileNameWithoutExtension(file)} ({currentFile}/{fileCount})..."));
                    Decompress(file);
                    File.Delete(file);
                }
            }

            progress.Report((1, "Unpacking complete!"));
            return null;
        }

        /// <summary>
        /// Unapck Archive modified from UXM
        /// </summary>
        /// <param name="fileDir"></param>
        /// <param name="archive"></param>
        /// <param name="key"></param>
        /// <param name="gameVersion"></param>
        /// <param name="password"></param>
        /// <param name="destination"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        private static async Task<string> UnpackArchive(string fileDir, string archive, string key,
            BHD5.Game gameVersion, string password, string destination, IProgress<(double value, string status)> progress)
        {
            progress.Report(((0), $"Loading {archive}..."));
            string bhdPath = $@"{fileDir}\{archive}.bhd";
            string bdtPath = $@"{fileDir}\{archive}.bdt";
            string jsonPath = $@"{fileDir}\{archive}.json";
            

            if (File.Exists(bhdPath) && File.Exists(bdtPath))
            {
                BHD5 bhd;
                try
                {
                    bool encrypted = CheckEncrypted(bhdPath);

                    if (encrypted)
                    {
                        try
                        {
                            using (MemoryStream bhdStream = CryptoUtil.DecryptBHD(bhdPath, password))
                            {
                                bhd = BHD5.Read(bhdStream, gameVersion);
                            }
                        }
                        catch (Exception)
                        {
                            return "Incorrect Password";
                        }
                        
                    }
                    else
                    {
                        using (FileStream bhdStream = File.OpenRead(bhdPath))
                        {
                            bhd = BHD5.Read(bhdStream, gameVersion);
                        }
                    }
                    
                }
                catch (OverflowException ex)
                {
                    return $"Failed to open BHD:\n{bhdPath}\n\n{ex}";
                }

                

                int fileCount = bhd.Buckets.Sum(b => b.Count) - 1;

                try
                {
                    var asyncFileWriters = new List<Task<long>>();
                    using (FileStream bdtStream = File.OpenRead(bdtPath))
                    {
                        int currentFile = -1;
                        long writingSize = 0;

                        var buckets = bhd.Buckets[bhd.Buckets.Count - 1];
                        var json = buckets[0];
                        List<string> filePaths = null;
                        if (json.FileNameHash == 0)
                        {
                            var jsonBytes = json.ReadFile(bdtStream);
                            filePaths = JsonConvert.DeserializeObject<List<string>>(Encoding.ASCII.GetString(jsonBytes));
                        }

                        var archiveDictionary = new Dictionary<uint, string>();

                        if (filePaths != null)
                        {
                            foreach (var path in filePaths)
                            {
                                archiveDictionary.Add(SFUtil.FromPathHash(path), path);
                            }
                        }

                        foreach (BHD5.Bucket bucket in bhd.Buckets)
                        {

                            foreach (BHD5.FileHeader header in bucket)
                            {
                               
                                currentFile++;

                                string path;
                                bool unknown;
                                if (archiveDictionary.TryGetValue(header.FileNameHash, out path))
                                {
                                    unknown = false;
                                    path = destination + path.Replace('/', '\\');
                                    if (File.Exists(path))
                                        continue;
                                }
                                else
                                {
                                    if (header.FileNameHash == 0)
                                        continue;

                                    unknown = true;
                                    string filename = $"{archive}_{header.FileNameHash:D10}";
                                    string directory = $@"{destination}\_unknown";
                                    path = $@"{directory}\{filename}";
                                    if (File.Exists(path) || Directory.Exists(directory) && Directory.GetFiles(directory, $"{filename}.*").Length > 0)
                                        continue;
                                }

                                progress.Report((((double)(currentFile + 1) / fileCount),
                                    $"Unpacking {path.Replace(fileDir, "")} ({currentFile + 1}/{fileCount})..."));

                                while (asyncFileWriters.Count > 0 && writingSize + header.PaddedFileSize > WRITE_LIMIT)
                                {
                                    for (int i = 0; i < asyncFileWriters.Count; i++)
                                    {
                                        if (asyncFileWriters[i].IsCompleted)
                                        {
                                            writingSize -= await asyncFileWriters[i];
                                            asyncFileWriters.RemoveAt(i);
                                        }
                                    }

                                    if (asyncFileWriters.Count > 0 && writingSize + header.PaddedFileSize > WRITE_LIMIT)
                                        Thread.Sleep(10);
                                }

                                byte[] bytes;
                                try
                                {

                                    bytes = header.ReadFile(bdtStream);

                                    if (unknown)
                                    {
                                        BinaryReaderEx br = new BinaryReaderEx(false, bytes);
                                        if (bytes.Length >= 3 && br.GetASCII(0, 3) == "GFX")
                                            path += ".gfx";
                                        else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "FSB5")
                                            path += ".fsb";
                                        else if (bytes.Length >= 0x19 && br.GetASCII(0xC, 0xE) == "ITLIMITER_INFO")
                                            path += ".itl";
                                        else if (bytes.Length >= 0x10 && br.GetASCII(8, 8) == "FEV FMT ")
                                            path += ".fev";
                                        else if (bytes.Length >= 4 && br.GetASCII(1, 3) == "Lua")
                                            path += ".lua";
                                        else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "DDS ")
                                            path += ".dds";
                                        else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "#BOM")
                                            path += ".txt";
                                        else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "BHF4")
                                            path += ".bhd";
                                        else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "BDF4")
                                            path += ".bdt";
                                        else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "ENFL")
                                            path += ".entryfilelist";
                                        else if (bytes.Length >= 4 && br.GetASCII(0, 4) == "DCX\0")
                                            path += ".dcx";
                                        br.Stream.Close();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    return $"Failed to read file:\r\n{path}\r\n\r\n{ex}";
                                }

                                try
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                                    writingSize += bytes.Length;
                                    asyncFileWriters.Add(WriteFileAsync(path, bytes));
                                }
                                catch (Exception ex)
                                {
                                    return $"Failed to write file:\r\n{path}\r\n\r\n{ex}";
                                }
                            }
                        }
                    }

                    foreach (Task<long> task in asyncFileWriters)
                        await task;
                }
                catch (Exception ex)
                {
                    return $"Failed to unpack BDT:\r\n{bdtPath}\r\n\r\n{ex}";
                }
            }

            return null;
        }

        /// <summary>
        /// Decompress DCX from Yabber.DCX
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <returns></returns>
        private static bool Decompress(string sourceFile)
        {
            string sourceDir = Path.GetDirectoryName(sourceFile);
            string outPath;

            outPath = $"{sourceDir}\\{Path.GetFileNameWithoutExtension(sourceFile)}";

            byte[] bytes = DCX.Decompress(sourceFile, out DCX.Type compression);
            File.WriteAllBytes(outPath, bytes);

            return false;
        }

        public static bool CheckEncrypted(string bhdPath)
        {
            bool encrypted = true;
            using (FileStream fs = File.OpenRead(bhdPath))
            {
                byte[] magic = new byte[4];
                fs.Read(magic, 0, 4);
                encrypted = Encoding.ASCII.GetString(magic) != "BHD5";
            }

            return encrypted;
        }

        private static async Task<long> WriteFileAsync(string path, byte[] bytes)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, false))
            {
                await fs.WriteAsync(bytes, 0, bytes.Length);
            }
            return bytes.Length;
        }
    }
}
