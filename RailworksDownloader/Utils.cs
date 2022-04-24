using ModernWpf.Controls;
using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace RailworksDownloader
{
    public static class Utils
    {
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

        public static MemoryStream RemoveInvalidXmlChars(MemoryStream istream)
        {
            byte[] buffer = StreamToByteArray(istream).Where(b => XmlConvert.IsXmlChar(Convert.ToChar(b))).ToArray();

            if (Array.Exists(buffer, x => x == (byte)'&'))
            {
                int prevSize = buffer.Length;

                string s = Encoding.UTF8.GetString(buffer);
                buffer = Encoding.UTF8.GetBytes(Regex.Replace(s, @"(?!\&[a-zA-Z0-9#]{1,8}\;)(&)", "&amp;"));

                Trace.Assert(buffer.Length < prevSize + prevSize / 100, "Error occured while fixing XML file.");
                if (buffer.Length > prevSize + prevSize / 100)
                {
                    if (App.ReportErrors)
                    {
                        SentrySdk.CaptureMessage($"Regex replaced over {(buffer.Length - prevSize) / 5} characters from XML file.", scope =>
                        {
                            scope.AddAttachment(buffer, "dump.xml", AttachmentType.Default, "text/xml");
                        }, SentryLevel.Warning);
                    }
                }
            }

            return new MemoryStream(buffer);
        }

        public static async Task<bool> CheckLogin(Action callback, MainWindow mw, Uri ApiUrl, bool invalidateToken = false)
        {
            if (App.Token == default || invalidateToken)
            {
                if (string.IsNullOrWhiteSpace(App.Settings.Username) || string.IsNullOrWhiteSpace(App.Settings.Password))
                {
                    App.Window.Dispatcher.Invoke(() =>
                    {
                        LoginDialog ld = new LoginDialog(ApiUrl, callback);
                        App.DialogQueue.AddDialog(Environment.TickCount, 2, ld);
                    });
                    return false;
                }

                string login = App.Settings.Username;
                string passwd = PasswordEncryptor.Decrypt(App.Settings.Password, login.Trim());

                ObjectResult<LoginContent> result = await WebWrapper.Login(login, passwd, ApiUrl);

                if (result == null || !IsSuccessStatusCode(result.code) || result.content == null || result.content.privileges < 0)
                {
                    App.Window.Dispatcher.Invoke(() =>
                    {
                        LoginDialog ld = new LoginDialog(ApiUrl, callback);
                        App.DialogQueue.AddDialog(Environment.TickCount, 2, ld);
                    });
                    return false;
                }

                LoginContent loginContent = result.content;
                App.Token = loginContent.token;
            }

            if (!MainWindow.ReportedDLC)
                mw.ReportDLC();

            return true;
        }

        public static bool IsSuccessStatusCode(int statusCode)
        {
            return (statusCode >= 200) && (statusCode <= 299);
        }

        public static bool CheckIsSerz(Stream fstream)
        {
            byte[] buff = new byte[4];
            fstream.Read(buff, 0, 4);
            fstream.Seek(0, SeekOrigin.Begin);
            bool flag = BitConverter.ToUInt32(buff, 0) == SerzReader.SERZ_MAGIC;
            return (flag);
        }

        public static bool CheckIsSerz(string fname)
        {
            using (Stream fs = File.OpenRead(fname))
            {
                return CheckIsSerz(fs);
            }
        }

        public static bool CheckIsXML(Stream stream)
        {
            try
            {
                string buffer;
                BinaryReader br = new BinaryReader(stream);
                char[] ch = br.ReadChars(32);
                buffer = new string(ch);

                return buffer.TrimStart().StartsWith("<");
            }
            catch
            {
                return false;
            }
            finally
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
        }

        public static string FindFile(string dir, string pattern)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                foreach (FileInfo file in di.GetFiles(pattern))
                {
                    string fe = file.Extension.ToLower();
                    if (fe == ".xml" || fe == ".bin")
                        return file.FullName;
                }
                return null;
            } catch
            {
                return null;
            }
        }

        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll")]
        private static extern bool FindClose(IntPtr hFindFile);

        public static bool CheckDirectoryEmpty(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(path);
            }

            if (Directory.Exists(path))
            {
                if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    path += "*";
                else
                    path += Path.DirectorySeparatorChar + "*";

                IntPtr findHandle = FindFirstFile(path, out WIN32_FIND_DATA findData);

                if (findHandle != INVALID_HANDLE_VALUE)
                {
                    try
                    {
                        bool empty = true;
                        do
                        {
                            if (findData.cFileName != "." && findData.cFileName != "..")
                                empty = false;
                        } while (empty && FindNextFile(findHandle, out findData));

                        return empty;
                    }
                    finally
                    {
                        FindClose(findHandle);
                    }
                }

                throw new Exception("Failed to get directory first file",
                    Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
            throw new DirectoryNotFoundException();
        }

        public static void RemoveEmptyFolder(string folderToRemove)
        {
            if (CheckDirectoryEmpty(folderToRemove) && App.Railworks.AssetsPath != NormalizePath(folderToRemove))
            {
                Directory.Delete(folderToRemove);
                RemoveEmptyFolder(Directory.GetParent(folderToRemove).FullName);
            }
        }

        public static List<string> RemoveFiles(List<string> filesToRemove)
        {
            List<string> removedFiles = new List<string>();

            foreach (string file in filesToRemove)
            {
                try
                {
                    string absolute = Path.Combine(App.Railworks.AssetsPath, file);
                    File.Delete(absolute);
                    removedFiles.Add(file);
                    RemoveEmptyFolder(Path.GetDirectoryName(absolute));
                }
                catch { }
            }

            return removedFiles;
        }

        public static bool CheckInvalidPathChars(string path, bool checkAdditional = false)
        {
            if (path == null)
                return true;

            return path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || (checkAdditional && AnyPathHasWildCardCharacters(path));
        }

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void ElevatePrivileges()
        {
            try
            {
                var exeName = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
                startInfo.Verb = "runas";
                Process.Start(startInfo);
                Application.Current.Shutdown();
            } catch
            {
                DisplayError(Localization.Strings.ManualElevateTitle, Localization.Strings.ManualElevateContent, (_) => Application.Current.Shutdown());
            }
        }

        private static bool AnyPathHasWildCardCharacters(string path, int startIndex = 0)
        {
            char currentChar;
            for (int i = startIndex; i < path.Length; i++)
            {
                currentChar = path[i];
                if (currentChar == '*' || currentChar == '?') return true;
            }
            return false;
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

        public static void DisplayError(string title, string message, Action<bool> callback = null)
        {
            App.Window.Dispatcher.Invoke(() =>
            {
                ContentDialog errorDialog = new ContentDialog()
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = Localization.Strings.Ok,
                    Owner = App.Window,
                };

                App.DialogQueue.AddDialog(Environment.TickCount, 99, errorDialog, callback);
            });
        }

        public static void DisplayYesNo(string title, string message, string yesLabel, string noLabel, Action<bool> callback)
        {
            App.Window.Dispatcher.Invoke(() =>
            {
                ContentDialog yesNoDialog = new ContentDialog()
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = yesLabel,
                    SecondaryButtonText = noLabel,
                    Owner = App.Window,
                };

                App.DialogQueue.AddDialog(Environment.TickCount, 100, yesNoDialog, callback);
            });
        }
    }
}
