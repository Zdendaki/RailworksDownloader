using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;

namespace RailworksDownloader
{
    public static class Utils
    {
        public static class MemoryInformation
        {
            [DllImport("KERNEL32.DLL")]
            private static extern int OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);
            [DllImport("KERNEL32.DLL")]
            private static extern int CloseHandle(int handle);

            [StructLayout(LayoutKind.Sequential)]
            private class PROCESS_MEMORY_COUNTERS
            {
                public int cb;
                public int PageFaultCount;
                public int PeakWorkingSetSize;
                public int WorkingSetSize;
                public int QuotaPeakPagedPoolUsage;
                public int QuotaPagedPoolUsage;
                public int QuotaPeakNonPagedPoolUsage;
                public int QuotaNonPagedPoolUsage;
                public int PagefileUsage;
                public int PeakPagefileUsage;
            }

            [DllImport("psapi.dll")]
            private static extern int GetProcessMemoryInfo(int hProcess, [Out] PROCESS_MEMORY_COUNTERS counters, int size);

            public static long GetMemoryUsageForProcess(long pid)
            {
                long mem = 0;
                int pHandle = OpenProcess(0x0400 | 0x0010, 0, (uint)pid);
                try
                {
                    PROCESS_MEMORY_COUNTERS pmc = new PROCESS_MEMORY_COUNTERS();
                    if (GetProcessMemoryInfo(pHandle, pmc, 40) != 0)
                        mem = pmc.WorkingSetSize;
                }
                finally
                {
                    CloseHandle(pHandle);
                }
                return mem;
            }
        }

        public static class PasswordEncryptor
        {
            public static string Encrypt(string input, string password)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return "";

                string output = "";

                for (int i = 0; i < input.Length; i++)
                {
                    int inp = input[i];
                    int pas = password[i % password.Length];

                    output += (char)(inp + pas);
                }

                return output;
            }

            public static string Decrypt(string input, string password)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return "";

                string output = "";

                for (int i = 0; i < input.Length; i++)
                {
                    int inp = input[i];
                    int pas = password[i % password.Length];

                    output += (char)(inp - pas);
                }

                return output;
            }
        }

        public class ZipTools
        {
            private const int ZIP_LEAD_BYTES = 0x04034b50;
            private const ushort GZIP_LEAD_BYTES = 0x8b1f;

            internal static bool IsPkZipCompressedData(byte[] data)
            {
                // if the first 4 bytes of the array are the ZIP signature then it is compressed data
                return BitConverter.ToInt32(data, 0) == ZIP_LEAD_BYTES;
            }

            internal static bool IsGZipCompressedData(byte[] data)
            {
                // if the first 2 bytes of the array are theG ZIP signature then it is compressed data;
                return BitConverter.ToUInt16(data, 0) == GZIP_LEAD_BYTES;
            }

            public static bool IsCompressedData(string fname)
            {
                byte[] buff = new byte[4];
                using (Stream fs = File.OpenRead(fname))
                {
                    fs.Read(buff, 0, buff.Length);
                }
                return IsPkZipCompressedData(buff) || IsGZipCompressedData(buff);
            }
        }

        public static string NormalizePath(string path, string ext = null)
        {

            if (string.IsNullOrEmpty(path))
                return path;

            // Remove path root.
            string path_root = Path.GetPathRoot(path);
            path = Path.ChangeExtension(path.Substring(path_root.Length), ext).Replace('/', Path.DirectorySeparatorChar);

            string[] path_components = path.Split(Path.DirectorySeparatorChar);

            // "Operating memory" for construction of normalized path.
            // Top element is the last path component. Bottom of the stack is first path component.
            Stack<string> stack = new Stack<string>(path_components.Length);

            foreach (string path_component in path_components)
            {
                if (path_component.Length == 0)
                    continue;

                if (path_component == ".")
                    continue;

                if (path_component == ".." && stack.Count > 0 && stack.Peek() != "..")
                {
                    stack.Pop();
                    continue;
                }

                stack.Push(path_component.ToLower());
            }

            string result = string.Join(new string(Path.DirectorySeparatorChar, 1), stack.Reverse().ToArray());
            result = Path.Combine(path_root, result);

            return result;

        }

        public static string GetRelativePath(string relativeTo, string path)
        {
            if (!relativeTo.EndsWith("/") && !relativeTo.EndsWith("\\"))
                relativeTo += Path.DirectorySeparatorChar;

            Uri uri = new Uri(relativeTo);
            string rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (rel.Contains(Path.DirectorySeparatorChar.ToString()) == false)
            {
                rel = $".{ Path.DirectorySeparatorChar }{ rel }";
            }
            return rel;
        }

        public static Stream RemoveInvalidXmlChars(string fname)
        {
            return new MemoryStream(File.ReadAllBytes(fname).Where(b => XmlConvert.IsXmlChar(Convert.ToChar(b))).ToArray());
        }

        public static Stream RemoveInvalidXmlChars(Stream istream)
        {
            return new MemoryStream(StreamToByteArray(istream).Where(b => XmlConvert.IsXmlChar(Convert.ToChar(b))).ToArray());
        }

        private static byte[] StreamToByteArray(Stream istream)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ostream = new MemoryStream())
            {
                int read;
                while ((read = istream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ostream.Write(buffer, 0, read);
                }
                return ostream.ToArray();
            }
        }
    }
}
