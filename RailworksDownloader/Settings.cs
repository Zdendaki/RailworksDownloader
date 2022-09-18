using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace RailworksDownloader
{
    internal class Settings
    {
        private string railworksLocation;
        public string RailworksLocation
        {
            get => railworksLocation;
            set
            {
                railworksLocation = value;
                RailworksPathChanged?.Invoke();
            }
        }

        public string Username { get; set; }

        public string Password { get; set; }

        public List<int> IgnoredPackages { get; set; }

        public List<string> PerformedCleanups { get; set; }

        public event PropertyChangedEventHandler RailworksPathChanged;

        public delegate void PropertyChangedEventHandler();

        private static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DLS", "settings.bin");
        private readonly object l = new object();

        public Settings()
        {
            IgnoredPackages = new List<int>();
            PerformedCleanups = new List<string>();
        }

        public void Load()
        {
            if (!File.Exists(path))
                return;

            byte[] buffer = File.ReadAllBytes(path);

            if (buffer.Length == 0)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)(buffer[i] ^ ((255 - i) & 255));

            using (MemoryStream ms = new MemoryStream(buffer))
            {
                using (GZipStream zip = new GZipStream(ms, CompressionMode.Decompress, false))
                {
                    using (BinaryReader br = new BinaryReader(zip))
                    {
                        RailworksLocation = br.ReadString();
                        Username = br.ReadString();
                        Password = br.ReadString();
                        IgnoredPackages = br.ReadListInt();
                        PerformedCleanups = br.ReadListString();
                    }
                }
            }
        }

        public void Save()
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

            lock (l)
            {
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    byte[] buffer;

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (GZipStream zip = new GZipStream(ms, CompressionMode.Compress, false))
                        {
                            using (BinaryWriter bw = new BinaryWriter(zip))
                            {
                                bw.Write(RailworksLocation ?? "");
                                bw.Write(Username ?? "");
                                bw.Write(Password ?? "");
                                bw.WriteList(IgnoredPackages);
                                bw.WriteList(PerformedCleanups);
                            }
                        }

                        buffer = ms.ToArray();
                    }

                    for (int i = 0; i < buffer.Length; i++)
                        buffer[i] = (byte)(buffer[i] ^ ((255 - i) & 255));

                    fs.Write(buffer, 0, buffer.Length);
                }
            }
        }
    }
}
