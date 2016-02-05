using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ParseHdf5Interface
{
    class Program
    {



        static void Main(string[] args)
        {

            Regex rePieces = new Regex(@"(([^\{;]*)\{([^\}]*)\}([^;]*);|([^\{\};]+;|\s*static\s*[^\(]*\([^\)]*\)\s*\{[^\}]*\}))", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            Regex reBracketedStructs = new Regex(@"typedef\s*struct\s*([^\{;]*)\{([^\}]*)\}([^;]*);", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
            Regex reWhites = new Regex(@"\s+");
            Regex reIsType = new Regex(@"^typedef[^\{\}]+;$");
            Regex reIsFunction = new Regex(@"^[^\(]+\([^\)]+\)\s*;$");
            Regex reIsEnum = new Regex(@"^enum[^\{]+\{[^\}]+\}\s*;$");
            Regex reIsTypedefEnum = new Regex(@"typedef\s+enum\s*([^\{;]+)\{([^\}]+)\}([^;]*);");
            Regex reIsTypedefStruct = new Regex(@"(typedef\s+|)struct\s*([^\{;]+)\{([^\}]+)\}([^;]*);");
            Regex reIsPlainStruct = new Regex(@"^struct\s*([^\{\};]+);", RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
            Regex reIsDeprecated = new Regex(@"^__declspec\(deprecated.*$", RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
            Regex reIsInline= new Regex(@"^static\s*__inline.*$", RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            Regex reReplaceDirectives = new Regex(@"(__cdecl|__declspec\(noreturn\)|__w64)");
            Regex reIsExtern = new Regex(@"extern\s*([^;]*);");




            var preprocFiles = Directory.EnumerateFiles(@"..\..\Hdf5PreprocessedHeaders");
            foreach (var file in preprocFiles)
            {
                var sb = new StringBuilder();
                var lines = File.ReadAllLines(file).Where(v => !(v.StartsWith("#pragma") | v.StartsWith("#line"))).Where(v => !String.IsNullOrWhiteSpace(v)).ToList();
                foreach (var line in lines)
                {
                    sb.AppendLine(line);
                    Console.WriteLine(line);
                }
                var wholeString = sb.ToString();
                MatchCollection mc = rePieces.Matches(wholeString);
                var piecesList = new List<String>();
                int mIdx = 0;
                foreach (Match m in mc)
                {
                    var mstring = m.Groups[0].Value.ToString().Trim();
                    var nodirect = reReplaceDirectives.Replace(mstring,"");
                    var newwhite = reWhites.Replace(nodirect, " ");
                    piecesList.Add(newwhite);
                    //for (int gIdx = 0; gIdx < m.Groups.Count; gIdx++)
                    //{
                    //    Console.WriteLine("[{0}][{1}] = {2}", mIdx, rePieces.GetGroupNames()[gIdx], m.Groups[gIdx].Value);
                    //}
                    //mIdx++;
                }
                var isType = piecesList.Select(v => reIsType.IsMatch(v)).ToArray();  
                var isFunction = piecesList.Select(v => reIsFunction.IsMatch(v)).ToArray();
                var isEnum = piecesList.Select(v => reIsEnum.IsMatch(v)).ToArray();
                var isTypedefEnum = piecesList.Select(v => reIsTypedefEnum.IsMatch(v)).ToArray();
                var isTypedefStruct = piecesList.Select(v => reIsTypedefStruct.IsMatch(v)).ToArray();
                var isPlainStruct = piecesList.Select(v => reIsPlainStruct.IsMatch(v)).ToArray();
                var isExtern = piecesList.Select(v => reIsExtern.IsMatch(v)).ToArray();
                var isDeprecated = piecesList.Select(v => reIsDeprecated.IsMatch(v)).ToArray();
                var isInline = piecesList.Select(v => reIsInline.IsMatch(v)).ToArray();

                var notProcessed = Enumerable.Range(0, piecesList.Count).Select(v => !(
                    isType[v]
                    || isFunction[v]
                    || isEnum[v]
                    || isTypedefEnum[v]
                    || isTypedefStruct[v]
                    || isPlainStruct[v]
                    || isExtern[v]
                    || isDeprecated[v]
                    || isInline[v]
                    )
                    ).ToArray();

                Console.WriteLine();
                for (int ii = 0; ii < piecesList.Count; ii++)
                {

                        Console.WriteLine("{0} ||| {1} |||", ii, piecesList[ii]);
                }
                Console.WriteLine();
                Console.WriteLine("FILE ==> {0}",file);
                for (int ii = 0; ii < piecesList.Count; ii++)
                {
                    if (notProcessed[ii])
                        Console.WriteLine("{0} --- {1}", ii, piecesList[ii]);
                }
                Console.ReadLine();
            }

            return;

        }
    }
}
