using Newtonsoft.Json;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace YAZip
{
    internal class Packer
    {
        /// <summary>
        /// Packs file to bhd/bdt pair
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="writePath"></param>
        /// <param name="password"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static string Pack(string filePath, string writePath, string password, IProgress<(double value, string status)> progress)
        {
            var bhdWriter = new BHD5(BHD5.Game.DarkSouls3);
            var fileList = new List<string>();
            var fileDir = Directory.GetParent(filePath);
            var fileName = filePath.Replace($"{fileDir}\\", "");

            string pathBDT = $@"{writePath}\{fileName}.bdt";
            string pathBHD = $@"{writePath}\{fileName}.bhd";

            if (pathBDT.Contains(".Encrypt"))
            {
                pathBDT = pathBDT.Replace(".Encrypt", "");
                pathBHD = pathBHD.Replace(".Encrypt", "");
            }

            if (pathBDT.Contains(".DCX"))
            {
                pathBDT = pathBDT.Replace(".DCX", "");
                pathBHD = pathBHD.Replace(".DCX", "");
            }

            if (File.Exists(pathBDT))
                File.Delete(pathBDT);

            
            var folders = Directory.GetDirectories(filePath);
            int fileCount = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories).Length;
            string[] files;
            BHD5.Bucket bucket;
            var currentFile = 0;


            using (FileStream bhdStream = File.Create(pathBHD))
            using (var bdtStream = new FileStream(pathBDT, FileMode.Append))
            {
                foreach (var folder in folders)
                {
                    bucket = new BHD5.Bucket();
                    files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        currentFile++;
                        byte[] bytes;

                        try
                        {
                            bytes = File.ReadAllBytes(file);
                        }
                        catch (IOException e)
                        {
                            return $"Cannot {Path.GetFileName(file)} write to bhd. {e.Message}";
                        }

                        var headerPath = file.Replace(fileDir.ToString(), "");
                        bool compress = headerPath.Contains(".DCX");
                        if (compress)
                        {
                            bytes = DCX.Compress(bytes, DCX.Type.DCX_KRAK);
                            headerPath = headerPath.Replace(".DCX", "");
                            headerPath += ".dcx";
                        }
                        var fullEncrypt = headerPath.Contains(".Encrypt");
                        if (fullEncrypt)
                        {
                            headerPath = headerPath.Replace(".Encrypt", "");
                        }

                        fileList.Add(headerPath.Trim().Replace('\\', '/'));
                        var header = new BHD5.FileHeader();
                        header.AESKey = new BHD5.AESKey();
                        header.AESKey.Key = CryptoUtil.GetKey();
                        header.AESKey.Ranges = CryptoUtil.Encrypt(bytes, header.AESKey.Key, fullEncrypt);
                        header.FileNameHash = SFUtil.FromPathHash(headerPath);
                        header.FileOffset = bdtStream.Length;
                        header.PaddedFileSize = bytes.Length;
                        bucket.Add(header);
                        progress.Report((((double)(currentFile) / fileCount),
                                    $"Packing {headerPath} ({currentFile}/{fileCount})..."));
                        bdtStream.Write(bytes, 0, bytes.Length);
                    }

                    //var shuffled = bucket.OrderBy(x => Guid.NewGuid()).ToList(); //Shuffles the bucket before adding it to bhd.
                    bhdWriter.Buckets.Add(bucket);
                }

                // Get files in Top Directory and add to bucket
                bucket = new BHD5.Bucket();
                files = Directory.GetFiles(filePath, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    byte[] bytes;
                    currentFile++;
                    try
                    {
                        bytes = File.ReadAllBytes(file);
                    }
                    catch (IOException)
                    {
                        return $"File {Path.GetFileName(file)} cannot be written to bhd";
                    }
                    
                    var headerPath = file.Replace(fileDir.ToString(), "");
                    progress.Report((((double)(currentFile) / fileCount),
                                $"Packing {headerPath} ({currentFile}/{fileCount})..."));

                    bool compress = headerPath.Contains(".DCX");
                    if (compress)
                    {
                        bytes = DCX.Compress(bytes, DCX.Type.DCX_KRAK);
                        headerPath = headerPath.Replace(".DCX", "");
                        headerPath += ".dcx";
                    }
                        
                    var fullEncrypt = headerPath.Contains(".Encrypt");
                    if (fullEncrypt)
                    {
                        headerPath = headerPath.Replace(".Encrypt", "");
                    }

                    fileList.Add(headerPath.Trim().Replace('\\', '/'));
                    var header = new BHD5.FileHeader();
                    header.AESKey = new BHD5.AESKey();
                    header.AESKey.Key = CryptoUtil.GetKey();
                    header.AESKey.Ranges = CryptoUtil.Encrypt(bytes, header.AESKey.Key, fullEncrypt);
                    header.FileNameHash = SFUtil.FromPathHash(headerPath);
                    header.FileOffset = bdtStream.Length;
                    header.PaddedFileSize = bytes.Length;
                    bucket.Add(header);
                    
                    bdtStream.Write(bytes, 0, bytes.Length);
                }
                bhdWriter.Buckets.Add(bucket);

                CreateJson(bhdWriter, fileList, bdtStream);
                bhdWriter.Write(bhdStream);
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                CryptoUtil.EncryptBHD(pathBHD, password);
            }

            progress.Report(((1, $"Packing complete!")));
            return null;
        }

        /// <summary>
        /// Creates JSon bucket and adds to the end
        /// </summary>
        /// <param name="bhdWriter"></param>
        /// <param name="fileList"></param>
        /// <param name="bdtStream"></param>
        private static void CreateJson(BHD5 bhdWriter, List<string> fileList, FileStream bdtStream)
        {
            var json = JsonConvert.SerializeObject(fileList);
            var jsonBytes = Encoding.ASCII.GetBytes(json);
            var jsonBucket = new BHD5.Bucket();
            var jsonHeader = new BHD5.FileHeader();
            jsonHeader.AESKey = new BHD5.AESKey();
            jsonHeader.AESKey.Key = CryptoUtil.GetKey();
            jsonHeader.AESKey.Ranges = CryptoUtil.Encrypt(jsonBytes, jsonHeader.AESKey.Key, true);
            jsonHeader.FileNameHash = 0;
            jsonHeader.FileOffset = bdtStream.Length;
            jsonHeader.PaddedFileSize = jsonBytes.Length;
            jsonBucket.Add(jsonHeader);
            bdtStream.Write(jsonBytes, 0, jsonBytes.Length);
            bhdWriter.Buckets.Add(jsonBucket);
        }

    }
}