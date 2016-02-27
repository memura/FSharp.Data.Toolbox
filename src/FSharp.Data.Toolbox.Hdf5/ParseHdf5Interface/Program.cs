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
        public static Regex reSimpleEnum = new Regex(@"enum\s+([A-Za-z0-9_]+)\s+\{([^\}]+)\}\s*\;", reOptions);
        public static Regex reTypedefEnum = new Regex(@"typedef\s+enum\s*([A-Za-z0-9_]*)\s*\{([^}]+)\}\s*([A-Za-z0-9_]*)\s*;", reOptions);
        public static Regex reTypedefStruct = new Regex(@"typedef\s+struct\s*([A-Za-z0-9_]*)\s*\{([^}]+)\}\s*([^;]+);", reOptions);
        public static Regex reStructStar = new Regex(@"struct\s+\w+\s+\*", reOptions);
        public static Regex reTypeDefElements = new Regex(@"(\w+)(\[[^\]]+\]|)\s*(;|)\s*$",reOptions);
        public static Regex reFunctionPointer = new Regex(@"([^\(]+)\(\*(\w+)\)\(([^\)]+)\)", reOptions);
        public static Regex reSplitArg = new Regex(@"(\w+)$");

        private static HashSet<String> typesHash = new HashSet<string>();
        private static Dictionary<string, string> typesDictionary = new Dictionary<string, string>();

        private static Dictionary<string, string> basicTypesDictionary = new Dictionary<string, string>() 
            { 
                {"bool","bool"},

                {"signed char","System.SByte"},

                {"unsigned char","System.Byte"},
                {"wchar_t","System.Char"},
                {"double","System.Double"},
                {"long double","System.Double"},
                     
                {"int","System.Int32"},
                {"signed int","System.Int32"},
                {"long","System.Int32"},
                {"signed long","System.Int32"},
                       
                {"unsigned int","System.UInt32"},
                {"unsigned","System.UInt32"},
                {"unsigned long","System.UInt32"},
                      
                {"__int64","System.Int64"},
                {"signed __int64","System.Int64"},
                {"long long","System.Int64"},
                {"signed long long","System.Int64"},
                       
                {"unsigned long long","System.UInt64"},
                       
                {"short","System.Int16"},
                {"signed short","System.Int16"},
                       
                {"unsigned short","System.UInt16"},
                      
                {"void","System.Void"},
                      
                {"void *","System.IntPtr"},
                       
                {"char *","System.Text.StringBuilder"},
                {"wchar_t *","System.Text.StringBuilder"},
    
                {"int *","int&"},
                {"const unsigned short *","System.UInt16&"},
                {"const unsigned char *","System.Byte&"},
                {"unsigned short *","System.UInt16&"},
                {"const char *","System.Text.StringBuilder"},
            };

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

            StringBuilder sbBasicTypes = new StringBuilder();
            sbBasicTypes.AppendLine("// Basic Types");
            foreach(var kv in basicTypesDictionary)
            {
                typesHash.Add(kv.Key);
                typesDictionary.Add(kv.Key, kv.Value);
                sbBasicTypes.AppendLine("// basic type " + kv.Key + " = " + kv.Value);
            }
            sbBasicTypes.AppendLine();


            var preprocFiles = Directory.EnumerateFiles(@"..\..\Hdf5PreprocessedHeaders");
            foreach (var file in preprocFiles)
            {
                var sb = new StringBuilder();
                var sbout = new StringBuilder();
                var smallFileName = Path.GetFileNameWithoutExtension(file);
                sbout.AppendLine("// from " + smallFileName);
                sbout.AppendLine();
                sbout.AppendLine(sbBasicTypes.ToString());

              

                var lines = File.ReadAllLines(file).Where(v => !(v.StartsWith("#pragma") | v.StartsWith("#line"))).Where(v => !String.IsNullOrWhiteSpace(v)).ToList();
                foreach (var line in lines)
                {
                    sb.AppendLine(line);
                    // Console.WriteLine(line);
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
                Console.WriteLine("FILE raw ==> {0}", file);
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
                    if (isType[ii]) Program.ProcessType(piecesList[ii], sbout, ii, smallFileName);
                    if (isFunction[ii]) Program.ProcessFunction(piecesList[ii], sbout, ii, smallFileName);
                    if (isEnum[ii]) Program.ProcessEnum(piecesList[ii], sbout, ii, smallFileName);
                    if (isTypedefEnum[ii]) Program.ProcessTypedefEnum(piecesList[ii], sbout, ii, smallFileName);
                    if (isTypedefStruct[ii]) Program.ProcessTypedefStruct(piecesList[ii], sbout, ii, smallFileName);



                    if (notProcessed[ii])
                        Console.WriteLine("{0} --- {1}", ii, piecesList[ii]);
                }

                Console.WriteLine(sbout.ToString());
                Console.ReadLine();
            }

            return;

        }


        public static void ProcessType(string line, StringBuilder sb, int lineNum, string file)
        {
            //Console.WriteLine(line);
            Regex whitespaces = new Regex(@"\s+");
            var parts = whitespaces.Split(line.Trim().Trim(new char[] { ';' }));
            var fromType = String.Join(" ", parts.Skip(1).Reverse().Skip(1).Reverse().ToArray()).Trim();


            if(!typesDictionary.ContainsKey(fromType))
            {
           
                Console.WriteLine("UNKNOWN TYPE -- "+ fromType + "  from the line " + line);
                // check if it is a special pointer type
                if (reStructStar.IsMatch(fromType))
                    fromType = "System.IntPtr";
            }


            var toType = parts.Last().Trim();


            if (!typesDictionary.ContainsKey(toType))
            {
                SetTypeFromDict(toType, fromType, typesDictionary);

                sb.AppendLine("// " + file + "(" + lineNum + ")");
                sb.AppendLine("// " + line);
                if(basicTypesDictionary.ContainsKey(fromType))
                    sb.AppendLine("type " + toType + " = " + basicTypesDictionary[fromType]);
                else
                    sb.AppendLine("type " + toType + " = " + fromType);
                sb.AppendLine();
            }

            return; // GetTypeFromDict(fromType, typesDictionary);

            #region bigcomment
            //switch (fromType)
            //{

            //    case "bool":
            //        fromType = "bool";
            //        break;
            //    case "signed char":
            //        fromType = "System.SByte";
            //        break;
            //    case "unsigned char":
            //        fromType = "System.Byte";
            //        break;
            //    case "wchar_t":
            //        fromType = "System.Char";
            //        break;
            //    case "double":
            //    case "long double":
            //        fromType = "System.Double";
            //        break;
            //    case "int":
            //    case "signed int":
            //    case "long":
            //    case "signed long":
            //        fromType = "System.Int32";
            //        break;
            //    case "unsigned int":
            //    case "unsigned long":
            //        fromType = "System.UInt32";
            //        break;
            //    case "__int64":
            //    case "signed __int64":
            //    case "long long":
            //    case "signed long long":
            //        fromType = "System.Int64";
            //        break;
            //    case "unsigned long long":
            //        fromType = "System.UInt64";
            //        break;
            //    case "short":
            //    case "signed short":
            //        fromType = "System.Int16";
            //        break;
            //    case "unsigned short":
            //        fromType = "System.UInt16";
            //        break;
            //    case "void":
            //        fromType = "System.Void";
            //        break;
            //    case "void *":
            //        fromType = "System.IntPtr";
            //        break;
            //    case "char *":
            //        fromType = "System.Text.StringBuilder";
            //        break;
            //    default:
            //        Console.WriteLine("MISSING --> {0}  in line {1}", fromType, line);
            //        break;

            //}

            //sb.AppendLine(String.Format("type {0} {1}", toType, fromType));
            #endregion bigcomment
        }


        public static void SetTypeFromDict(string fromType, string toType, Dictionary<string, string> typeDict)
        {
            if (!typeDict.ContainsKey(fromType.Trim()))
            {
                #region bigcomment
                //switch (fromType)
                //{

                //    case "bool":
                //        toType = "bool";
                //        break;
                //    case "signed char":
                //        toType = "System.SByte";
                //        break;
                //    case "unsigned char":
                //        toType = "System.Byte";
                //        break;
                //    case "wchar_t":
                //        toType = "System.Char";
                //        break;
                //    case "double":
                //    case "long double":
                //        toType = "System.Double";
                //        break;
                //    case "int":
                //    case "signed int":
                //    case "long":
                //    case "signed long":
                //        toType = "System.Int32";
                //        break;
                //    case "unsigned int":
                //    case "unsigned long":
                //        toType = "System.UInt32";
                //        break;
                //    case "__int64":
                //    case "signed __int64":
                //    case "long long":
                //    case "signed long long":
                //        toType = "System.Int64";
                //        break;
                //    case "unsigned long long":
                //        toType = "System.UInt64";
                //        break;
                //    case "short":
                //    case "signed short":
                //        toType = "System.Int16";
                //        break;
                //    case "unsigned short":
                //        toType = "System.UInt16";
                //        break;
                //    case "void":
                //        toType = "System.Void";
                //        break;
                //    case "void *":
                //        toType = "System.IntPtr";
                //        break;
                //    case "char *":
                //        toType = "System.Text.StringBuilder";
                //        break;
                //    default:
                //        Console.WriteLine("MISSING Type--> {0}  ", fromType);
                //        toType = "UNKNOWN " + fromType;
                //        break;

                //}
                //Console.WriteLine("MISSING Type--> {0}  ", fromType);
                //toType = "UNKNOWN " + fromType;
                #endregion big comment
            }

            typeDict.Add(fromType.Trim(), toType.Trim());
            return;
        }

        public static String GetTypeFromDict(string fromType, Dictionary<string,string> typeDict)
        {

            if (typeDict.ContainsKey(fromType.Trim()))
                return typeDict[fromType];
            string toType;

            switch (fromType)
            {

                case "bool":
                    toType = "bool";
                    break;
                case "signed char":
                    toType = "System.SByte";
                    break;
                case "unsigned char":
                    toType = "System.Byte";
                    break;
                case "wchar_t":
                    toType = "System.Char";
                    break;
                case "double":
                case "long double":
                    toType = "System.Double";
                    break;
                case "int":
                case "signed int":
                case "long":
                case "signed long":
                    toType = "System.Int32";
                    break;
                case "unsigned int":
                case "unsigned long":
                    toType = "System.UInt32";
                    break;
                case "__int64":
                case "signed __int64":
                case "long long":
                case "signed long long":
                    toType = "System.Int64";
                    break;
                case "unsigned long long":
                    toType = "System.UInt64";
                    break;
                case "short":
                case "signed short":
                    toType = "System.Int16";
                    break;
                case "unsigned short":
                    toType = "System.UInt16";
                    break;
                case "void":
                    toType = "System.Void";
                    break;
                case "void *":
                    toType = "System.IntPtr";
                    break;
                case "char *":
                    toType = "System.Text.StringBuilder";
                    break;
                default:
                    Console.WriteLine("MISSING Type--> {0}  ", fromType);
                    toType = "UNKNOWN "+fromType;
                    break;

            }

            typeDict.Add(fromType.Trim(), toType);
            return typeDict[fromType];

        }

        public static String ProcessEnum(string line, StringBuilder sb, int lineNum, string file)
        {
            Regex whitespaces = new Regex(@"\s+");
            //var parts = whitespaces.Split(line.Trim().Trim(new char[] { ';' }));
            //var fromType = String.Join(" ", parts.Skip(1).Reverse().Skip(1).Reverse().ToArray());
            //var toType = parts.Last();
            var match = reSimpleEnum.Match(line);
            var enumName = match.Groups[1].Value;
            var enumItems = match.Groups[2].Value;
            var enumElements = enumItems.Split(',').Select(v => v.Trim()).ToArray();
            //sb.AppendLine(String.Format("type {0} {1}", toType, fromType));
            //Console.WriteLine("Function --> {0}  in line {1}", "--", line);

            sb.AppendLine("// " + file + "(" + lineNum + ")");
            sb.AppendLine("// " + line);
            sb.AppendLine("type  " + enumName + " = ");
            int ctr = 0;
            for (int ii = 0; ii < enumElements.Length; ii++)
            {
                var item = enumElements[ii];
                if (item.Contains("="))
                {
                    var enumElemName = Int32.Parse(item.Split('=').First().Trim());

                    ctr = Int32.Parse(item.Split('=').Last().Trim());
                    sb.AppendLine("    | " + enumElemName + " = " + ctr);
                }
                else
                    sb.AppendLine("    | " + enumElements[ii] + " = " + ctr);
                ctr++;
            }
            sb.AppendLine();
            // Console.WriteLine("Function --> {0}  in line {1}", funname, trimmed);


            return " ";
        }



        public static String ProcessTypedefEnum(string line, StringBuilder sb, int lineNum, String file)
        {
            Regex reWhitespaces = new Regex(@"\s+");
            Regex reHexStuff = new Regex(@"(0x|0X|\||\&)");
            //var parts = whitespaces.Split(line.Trim().Trim(new char[] { ';' }));
            //var fromType = String.Join(" ", parts.Skip(1).Reverse().Skip(1).Reverse().ToArray());
            //var toType = parts.Last();
            var match = reTypedefEnum.Match(line);
            var enumName = match.Groups[3].Value;
            if(String.IsNullOrWhiteSpace(enumName))
                enumName = match.Groups[1].Value;

            var enumItems = match.Groups[2].Value;
            var enumElements = enumItems.Split(',').Select(v => v.Trim()).ToArray();
            //sb.AppendLine(String.Format("type {0} {1}", toType, fromType));
            Console.WriteLine("Function --> {0}  in line {1}", "--", line);


            sb.AppendLine("// " + file + "(" + lineNum + ")");
            sb.AppendLine("// " + line);
            sb.AppendLine("type  " + enumName + " = ");
            int ctr = 0;
            for (int ii = 0; ii < enumElements.Length; ii++)
            {
                var item = enumElements[ii];
                if (item.Contains("="))
                {
                    var enumElemName =item.Split('=').First().Trim();
                    var numstring = item.Split('=').Last().Replace('(', ' ').Replace(')', ' ').Trim();
     
                    if(reHexStuff.IsMatch(numstring))
                    {
                        numstring = numstring.Replace("|", " ||| ").Replace("&", " &&& ");
                        sb.AppendLine("    | " + enumElemName + " = " + numstring);
                    }
                    else
                    { 
                        ctr = Int32.Parse(numstring);
                        sb.AppendLine("    | " + enumElemName + " = " + ctr);
                    }
                }
                else
                    sb.AppendLine("    | " + enumElements[ii] + " = " + ctr);
                ctr++;
            }
            sb.AppendLine();
            // Console.WriteLine("Function --> {0}  in line {1}", funname, trimmed);


            return " ";
        }


        public static void ProcessTypedefStruct(string line, StringBuilder sbout,int lineNum, string file)
        {
            Regex reWhitespaces = new Regex(@"\s+");
            Regex reHexStuff = new Regex(@"(0x|0X|\||\&)");
            Regex reArraySizes = new Regex(@"\[\s*(\d+)\s*\]");
            StringBuilder sb = new StringBuilder();

            var match = reTypedefStruct.Match(line);
            var structName = match.Groups[1].Value;
            if (String.IsNullOrWhiteSpace(structName))
                structName = match.Groups[3].Value;


            if (typesDictionary.ContainsKey(structName))
                return;

            var structItems = match.Groups[2].Value;
            var elements = structItems.Split(';').Select(v => v.Trim()).ToArray();

            Console.WriteLine("TypedefStruct --> {0}  in line {1}", structName, line);

            sb.AppendLine("// " + file + "(" + lineNum + ")");
            sb.AppendLine("// " + line);
            sb.AppendLine("[< StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi) >]");
            sb.AppendLine("[< Struct >]");
            sb.AppendLine("type " + structName + " = ");
            int ctr = 0;

            Console.Write(sb.ToString());

            for (int ii = 0; ii < elements.Length; ii++)
            {
                var item = elements[ii].Trim();


                if (String.IsNullOrWhiteSpace(item))
                    continue;

                // get rid of enum
                item = item.Replace("enum ", "");


                var matchB = reTypeDefElements.Match(item);
                var itemName = matchB.Groups[1].Value;
                var bracketPart = matchB.Groups[2].Value;

                var itemType =    item.Substring(0,item.Length-(itemName.Length + bracketPart.Length)).Trim();

                //var itemName = parts.Last();
                //var itemType = parts[0].Trim();

                if (basicTypesDictionary.ContainsKey(itemType))
                    itemType = basicTypesDictionary[itemType];

                if(!typesDictionary.ContainsKey(itemType))
                {

                }
                

                // itemType = GetTypeFromDict(itemType, typesDictionary);
                //if (item.Contains(""))

                // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                // public delegate void M1(IntPtr self, int a, float b);

                if(item.Contains("("))
                {
                    // this is a pointer function
                    var fmatch = reFunctionPointer.Match(item);
                    var innerftype = fmatch.Groups[1].Value.Trim();

                    var innerfname = fmatch.Groups[2].Value;
                    innerfname = innerfname.Replace('*',' ').Replace('(',' ').Replace(')',' ').Trim();

                    var innerargs = fmatch.Groups[3].Value.Trim();

                    var inneritems = innerargs.Split(',').Select(v=>v.Trim()).ToArray();

                    var fnArgs = new List<String>();
                    foreach(var inneritem in inneritems)
                    {
                        var funelemmatch = reSplitArg.Match(inneritem);
                        var funelemname = funelemmatch.Groups[1].Value;
                        var funelemtype = inneritem.Substring(0,inneritem.Length - funelemname.Length).Trim();

                        if (funelemtype.Contains('*'))
                        {
                            if(basicTypesDictionary.ContainsKey(funelemtype))
                                funelemtype = basicTypesDictionary[funelemtype];

                            var typeused = GetTypeFromDict(funelemtype.Replace('*', ' ').Trim(), typesDictionary).Replace("struct ", "").Replace("enum ", "").Trim();

                            fnArgs.Add("ref " +typeused + " " + funelemname);
                        }
                        else
                        {
                            var typeused = GetTypeFromDict(funelemtype, typesDictionary);

                            fnArgs.Add(typeused + " " + funelemname);
                        }
                    }

                    sb.AppendLine("    [< UnmanagedFunctionPointer(CallingConvention.Cdecl) >]");
                    sb.AppendLine("    pubic delegate "+ innerftype +" " + innerfname +"_fp(" + String.Join(", ",fnArgs)  +")");
                    continue;
                }

                if (bracketPart.Contains("["))
                {
                    bracketPart = bracketPart.Trim('[').Trim(']');
                    String sizeConst ="1";
                    if (bracketPart.Contains("+"))
                    {
                        sizeConst = bracketPart.Split('+').Select(v => v.Trim()).Select(v => Int32.Parse(v)).Sum().ToString();
                    }                     
                    else
                    {
                         sizeConst = bracketPart.Trim();
                    }
                    
                    sb.AppendLine("    [<field: MarshalAs(UnmanagedType.ByValArray, SizeConst = "+sizeConst+")>]");
                    sb.AppendLine("    val " + itemName + ": array " + itemType);
                }
                else
                sb.AppendLine("    val " + itemName + ": " + itemType);

                //if (item.Contains("="))
                //{
                //    var enumElemName = item.Split('=').First().Trim();
                //    var numstring = item.Split('=').Last().Replace('(', ' ').Replace(')', ' ').Trim();

                //    if (reHexStuff.IsMatch(numstring))
                //    {
                //        numstring = numstring.Replace("|", " ||| ").Replace("&", " &&& ");
                //        sb.AppendLine("    val " + enumElemName + " = " + numstring);
                //    }
                //    else
                //    {
                //        ctr = Int32.Parse(numstring);
                //        sb.AppendLine("    val " + enumElemName + " = " + ctr);
                //    }
                //}
                //else
                //    sb.AppendLine("    val " + elements[ii] + " = " + ctr);
                //ctr++;
            }
            sb.AppendLine();
            Console.Write(sb.ToString());

            sbout.Append(sb.ToString());

            if (!typesDictionary.ContainsKey(structName))
                typesDictionary.Add(structName, structItems);

            // Console.WriteLine("Function --> {0}  in line {1}", funname, trimmed);


            return;
        }



        // const char * is replaced by [< MarshalAs(UnmanagedType.LPStr) >] StringBuilder


        public static String ProcessFunction(string line, StringBuilder sb, int lineNum, string file)
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

            sb.AppendLine("// " + file + "(" + lineNum + ")");
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




//POINTER	    F# TYPE	        DECLARATION	    INVOCATION
//Unmanaged	    nativeint	    <type>*	        &&<type>
//Managed	    byref <type>    <type>&	        &type




