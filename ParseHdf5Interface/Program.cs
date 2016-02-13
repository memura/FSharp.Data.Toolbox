using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MoreLinq;

namespace ParseHdf5Interface
{
    class Program
    {

        public static RegexOptions reOptions = RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled;
        public static Regex reVoidCharStar = new Regex(@"void\s+char\s+\*", reOptions);
        public static Regex reConstCharStar = new Regex(@"const\s+char\s+\*", reOptions);
        public static Regex reIntPtr = new Regex(@"(const\s+void\s*\*|void\s*\*|void\s*\*\*)", reOptions);

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
            Regex reIsInline = new Regex(@"^static\s*__inline.*$", RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            Regex reReplaceDirectives = new Regex(@"(__cdecl|__declspec\(noreturn\)|__w64)");
            Regex reIsExtern = new Regex(@"extern\s*([^;]*);");




            var preprocFiles = Directory.EnumerateFiles(@"..\..\Hdf5PreprocessedHeaders");
            foreach (var file in preprocFiles)
            {
                var sb = new StringBuilder();
                var sbout = new StringBuilder();
                var smallFileName = Path.GetFileNameWithoutExtension(file);
                sbout.AppendLine("// from " + smallFileName);

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
                    var nodirect = reReplaceDirectives.Replace(mstring, "");
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
                Console.WriteLine("FILE ==> {0}", file);
                for (int ii = 0; ii < piecesList.Count; ii++)
                {
                    if (notProcessed[ii])
                        Console.WriteLine("{0} --- {1}", ii, piecesList[ii]);
                }

                for (int ii = 0; ii < piecesList.Count; ii++)
                {
                   // if (isType[ii]) Program.ProcessType(piecesList[ii], sbout);
                    if (isFunction[ii]) Program.ProcessFunction(piecesList[ii], sbout);



                    if (notProcessed[ii])
                        Console.WriteLine("{0} --- {1}", ii, piecesList[ii]);
                }


                Console.WriteLine(sbout.ToString());

                Console.ReadLine();
            }

            return;

        }


        public static String ProcessType(string line, StringBuilder sb)
        {
            Regex whitespaces = new Regex(@"\s+");
            var parts = whitespaces.Split(line.Trim().Trim(new char[] { ';' }));
            var fromType = String.Join(" ", parts.Skip(1).Reverse().Skip(1).Reverse().ToArray());
            var toType = parts.Last();

            switch (fromType)
            {

                case "bool":
                    fromType = "bool";
                    break;
                case "signed char":
                    fromType = "System.SByte";
                    break;
                case "unsigned char":
                    fromType = "System.Byte";
                    break;
                case "wchar_t":
                    fromType = "System.Char";
                    break;
                case "double":
                case "long double":
                    fromType = "System.Double";
                    break;
                case "int":
                case "signed int":
                case "long":
                case "signed long":
                    fromType = "System.Int32";
                    break;
                case "unsigned int":
                case "unsigned long":
                    fromType = "System.UInt32";
                    break;
                case "__int64":
                case "signed __int64":
                case "long long":
                case "signed long long":
                    fromType = "System.Int64";
                    break;
                case "unsigned long long":
                    fromType = "System.UInt64";
                    break;
                case "short":
                case "signed short":
                    fromType = "System.Int16";
                    break;
                case "unsigned short":
                    fromType = "System.UInt16";
                    break;
                case "void":
                    fromType = "System.Void";
                    break;
                case "void *":
                    fromType = "System.IntPtr";
                    break;
                case "char *":
                    fromType = "System.Text.StringBuilder";
                    break;
                default:
                    Console.WriteLine("MISSING --> {0}  in line {1}", fromType, line);
                    break;

            }

            sb.AppendLine(String.Format("type {0} {1}", toType, fromType));

            return " ";
        }


        // const char * is replaced by [< MarshalAs(UnmanagedType.LPStr) >] StringBuilder


        public static String ProcessFunction(string line, StringBuilder sb)
        {
            Regex whitespaces = new Regex(@"\s+");
            var trimmed = line.Trim().Trim(new char[] { ';' });
            var parts = whitespaces.Split(trimmed);
            var fromType = String.Join(" ", parts.Skip(1).Reverse().Skip(1).Reverse().ToArray());
            var toType = parts.Last();

            // trimmed = trimmed.Replace("void *", "System.IntPtr ");
            trimmed = reConstCharStar.Replace(trimmed,@"[< MarshalAs(UnmanagedType.LPStr) >] StringBuilder ");
            trimmed = reIntPtr.Replace(trimmed, "System.IntPtr ");

            var funname = whitespaces.Split(trimmed.Split('(').First()).Last();

            sb.AppendLine("// " + line);
            sb.AppendLine("[< DllImport(HDF5x64DLL, EntryPoint=\"" + funname + "\", CharSet=CharSet.Auto, CallingConvention=CallingConvention.StdCall) >]");
            sb.AppendLine("extern " + trimmed);
            sb.AppendLine();
            Console.WriteLine("Function --> {0}  in line {1}", funname, trimmed);
            
            
            // extern int H5LibGetVersion_x64([<Out>] int* majnum, [<Out>] int* minnum, [<Out>] int* relnum )


            return "";
        }

    }
}
//    Visual C++ type                             .NET Framework type
//    bool                                        System.Boolean
//    signed char (see /J for more information)   System.SByte
//    unsigned char                               System.Byte
//    wchar_t                                     System.Char
//    double and long double                      System.Double
//    float                                       System.Single
//    int, signed int, long, and signed long      System.Int32
//    unsigned int and unsigned long              System.UInt32
//    __int64 and signed __int64                  System.Int64
//    unsigned __int64                            System.UInt64
//    short and signed short                      System.Int16
//    unsigned short                              System.UInt16
//    void                                        System.Void
//    void *                                      System.IntPtr
