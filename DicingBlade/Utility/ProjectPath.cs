using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Utility
{
    internal static class ProjectPath
    {
        public static string GetFolderPath(string folder)
        {
            var workingDirectory = Environment.CurrentDirectory;
            var projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
            return Path.Combine(projectDirectory, folder);
        }

        public static string GetFilePathInFolder(string folder, string filename)
        {
            return Path.Combine(Path.Combine(GetFolderPath(folder), filename));
        }
    }
}
