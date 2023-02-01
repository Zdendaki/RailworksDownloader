using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DownloadStationClient
{
    internal static class Utils
    {
        public static string NormalizePath(string path, string? ext = null)
        {

            if (string.IsNullOrEmpty(path))
                return path;

            // Remove path root.
            string? path_root = Path.GetPathRoot(path);
            path = Path.ChangeExtension(path.Substring(path_root?.Length ?? 0), ext).Replace('/', Path.DirectorySeparatorChar);

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
            
            if (path_root is not null)
                result = Path.Combine(path_root, result);

            return result;

        }
    }
}
