using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace batch
{
    public class DirectoryIterator
    {
        private readonly Action<string> ProcessFile;
        private readonly string path;


        private void ProcessDirectory(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileEntries)
                ProcessFile(fileName);

            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
                ProcessDirectory(subdirectory);
        }


        public DirectoryIterator(string path, Action<string> onFileAction)
        {
            this.path = path;
            this.ProcessFile = onFileAction;
        }

        public void Run()
        {
            if (File.Exists(path))
            {
                ProcessFile(path);
            } else if (Directory.Exists(path))
            {
                ProcessDirectory(path);
            } else
            {
                Console.WriteLine("{0} is not a valid file or directory.", path);
            }
        }
    }
}
