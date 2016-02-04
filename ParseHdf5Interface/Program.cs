using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ParseHdf5Interface
{
    class Program
    {
        static void Main(string[] args)
        {
            var preprocFiles = Directory.EnumerateFiles(@"..\..\Hdf5PreprocessedHeaders");
            foreach(var file in preprocFiles)
            {
               var lines = File.ReadAllLines(file).Where(v => !(v.StartsWith("#pragma") | v.StartsWith("#line"))).Where(v => !String.IsNullOrWhiteSpace(v)).ToList();
               foreach(var line in lines)
               {
                   Console.WriteLine(line);
               }
            
            }

            return;

        }
    }
}
