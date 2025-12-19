using AgentDump.Parser;
using AgentDump.Search;
using AgentDump.Output;
using AgentDump.Models;

namespace AgentDump;

class Program
{
    static Il2CppSearchEngine? _engine;
    static string _dumpPath = "";
    static bool _jsonOutput = false;
    static bool _tsOutput = false;
    static int _limit = 50;

    static void Main(string[] args)
    {
        string? cmdArg = null;
        _dumpPath = "game_il2cpp_dump";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--path" or "-p" when i + 1 < args.Length:
                    _dumpPath = args[++i];
                    break;
                case "--cmd" or "-c" when i + 1 < args.Length:
                    cmdArg = args[++i];
                    break;
                case "--json" or "-j":
                    _jsonOutput = true;
                    break;
                case "--ts":
                    _tsOutput = true;
                    break;
                case "--limit" or "-l" when i + 1 < args.Length:
                    int.TryParse(args[++i], out _limit);
                    break;
                case "--help" or "-h":
                    PrintFullHelp();
                    return;
            }
        }

        var dumpFile = Path.Combine(_dumpPath, "dump.cs");
        if (!File.Exists(dumpFile))
        {
            Output($"ERROR: dump.cs not found at {dumpFile}");
            return;
        }

        var parser = new Il2CppDumpParser();
        var classes = parser.Parse(dumpFile);
        _engine = new Il2CppSearchEngine(classes);

        if (!string.IsNullOrEmpty(cmdArg))
        {
            ExecuteCommand(cmdArg);
            return;
        }

        Console.WriteLine("=== AgentDump - IL2CPP Search Tool ===");
        var stats = _engine.GetStats();
        Console.WriteLine($"Loaded: {stats.TotalClasses} classes, {stats.TotalMethods} methods, {stats.TotalFields} fields");
        Console.WriteLine($"Types: {stats.TotalInterfaces} interfaces, {stats.TotalEnums} enums, {stats.TotalStructs} structs");
        Console.WriteLine($"Unity: {stats.MonoBehaviours} MonoBehaviours, {stats.ScriptableObjects} ScriptableObjects\n");
        PrintHelp();
        RunInteractiveMode();
    }

    static void Output(string text) => Console.WriteLine(text);
    static void OutputJson(object obj) => Console.WriteLine(ResultFormatter.ToJson(obj));
    static void OutputTs(object obj, string typeName) => Console.WriteLine(ResultFormatter.ToTypeScript(obj, typeName));

    static void OutputData(object obj, string tsTypeName)
    {
        if (_tsOutput) OutputTs(obj, tsTypeName);
        else OutputJson(obj);
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"COMMANDS:

CLASS: class <name>, class.exact <name>, ns <namespace>, fullname <name>
       base <class>, impl <interface>, type <class|struct|enum|interface>
       mono, scriptable

FIELD: field <name>, field.type <type>, field.offset <0x10>

METHOD: method <name>, method.ret <type>, method.param <type>
        rva <0x123>, method.offset <0x123>

ADVANCED: find <query>, hierarchy <class>, derived <class>
          hasmeth <name>, hasfield <name>

DETAILS: detail <class>, idx <index>

INFO: stats, namespaces, help, exit
");
    }

    static void PrintFullHelp()
    {
        Console.WriteLine(@"AgentDump - IL2CPP Search Tool

USAGE: AgentDump.exe [options]

OPTIONS:
  --path, -p <folder>    IL2CPP dump folder (default: game_il2cpp_dump)
  --cmd, -c <command>    Execute command and exit
  --json, -j             Output as JSON
  --ts                   Output as TypeScript
  --limit, -l <number>   Max results (default: 50)
  --help, -h             Show help

EXAMPLES:
  AgentDump.exe --cmd ""class Player"" --json
  AgentDump.exe --cmd ""find health"" --ts --limit 10
");
    }

    static void RunInteractiveMode()
    {
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
            try { ExecuteCommand(input); }
            catch (Exception ex) { Output($"Error: {ex.Message}"); }
        }
    }

    static void ExecuteCommand(string input)
    {
        if (_engine == null) return;

        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLower();
        var arg = parts.Length > 1 ? parts[1].Trim() : "";

        switch (cmd)
        {
            case "class":
                OutputClassResults(arg, "class_name", _engine.SearchByClassName(arg));
                break;
            case "class.exact":
                OutputClassResults(arg, "class_name_exact", _engine.SearchByClassName(arg, exact: true));
                break;
            case "ns" or "namespace":
                OutputClassResults(arg, "namespace", _engine.SearchByNamespace(arg));
                break;
            case "fullname" or "full":
                OutputClassResults(arg, "full_name", _engine.SearchByFullName(arg));
                break;
            case "base":
                OutputClassResults(arg, "base_class", _engine.SearchByBaseClass(arg));
                break;
            case "impl" or "interface":
                OutputClassResults(arg, "interface", _engine.SearchByInterface(arg));
                break;
            case "type":
                OutputClassResults(arg, "class_type", _engine.SearchByType(arg));
                break;
            case "mono" or "monobehaviour":
                OutputClassResults("MonoBehaviour", "monobehaviour", _engine.GetMonoBehaviours());
                break;
            case "scriptable" or "scriptableobject":
                OutputClassResults("ScriptableObject", "scriptableobject", _engine.GetScriptableObjects());
                break;
            case "field":
                OutputFieldResults(arg, _engine.SearchFields(arg));
                break;
            case "field.type":
                OutputFieldResults($"type:{arg}", _engine.SearchFieldsByType(arg));
                break;
            case "field.offset":
                OutputFieldResults($"offset:{arg}", _engine.SearchByOffset(arg));
                break;
            case "method":
                OutputMethodResults(arg, _engine.SearchMethods(arg));
                break;
            case "method.ret" or "method.return":
                OutputMethodResults($"return:{arg}", _engine.SearchMethodsByReturnType(arg));
                break;
            case "method.param":
                OutputMethodResults($"param:{arg}", _engine.SearchMethodsByParameterType(arg));
                break;
            case "rva":
                OutputMethodResults($"rva:{arg}", _engine.SearchByRVA(arg));
                break;
            case "method.offset":
                OutputMethodResults($"offset:{arg}", _engine.SearchMethodsByOffset(arg));
                break;
            case "find" or "search":
                SmartSearch(arg);
                break;
            case "hierarchy":
                var chain = _engine.GetInheritanceChain(arg);
                if (_jsonOutput || _tsOutput)
                    OutputData(new { query = arg, type = "inheritance_chain", chain }, "InheritanceChainResult");
                else
                {
                    Output($"Inheritance chain for '{arg}':");
                    for (int i = 0; i < chain.Count; i++)
                        Output($"  {new string(' ', i * 2)}{chain[i]}");
                }
                break;
            case "derived":
                OutputClassResults(arg, "derived_classes", _engine.GetDerivedClasses(arg));
                break;
            case "hasmeth" or "hasmethod":
                OutputClassResults($"has_method:{arg}", "classes_with_method", _engine.FindClassesWithMethod(arg));
                break;
            case "hasfield":
                OutputClassResults($"has_field:{arg}", "classes_with_field", _engine.FindClassesWithField(arg));
                break;
            case "detail" or "details" or "info":
                var detailCls = _engine.SearchByClassName(arg, exact: true).FirstOrDefault() 
                             ?? _engine.SearchByClassName(arg).FirstOrDefault();
                if (detailCls != null)
                {
                    if (_jsonOutput || _tsOutput)
                        OutputData(ResultFormatter.WrapClassDetail(arg, detailCls), "ClassDetailResult");
                    else
                        Output(ResultFormatter.FormatClass(detailCls, detailed: true));
                }
                else Output($"Class '{arg}' not found");
                break;
            case "idx" or "index":
                if (int.TryParse(arg, out var idx))
                {
                    var cls = _engine.GetByTypeDefIndex(idx);
                    if (cls != null)
                    {
                        if (_jsonOutput || _tsOutput)
                            OutputData(ResultFormatter.WrapClassDetail(arg, cls), "ClassDetailResult");
                        else
                            Output(ResultFormatter.FormatClass(cls, detailed: true));
                    }
                    else Output($"TypeDefIndex {idx} not found");
                }
                break;
            case "stats":
                var stats = _engine.GetStats();
                if (_jsonOutput || _tsOutput) OutputData(stats, "DumpStats");
                else
                {
                    Output($"Classes: {stats.TotalClasses} | Methods: {stats.TotalMethods} | Fields: {stats.TotalFields}");
                    Output($"Namespaces: {stats.TotalNamespaces} | Interfaces: {stats.TotalInterfaces}");
                    Output($"Enums: {stats.TotalEnums} | Structs: {stats.TotalStructs}");
                    Output($"MonoBehaviours: {stats.MonoBehaviours} | ScriptableObjects: {stats.ScriptableObjects}");
                }
                break;
            case "namespaces":
                var namespaces = _engine.GetAllNamespaces().ToList();
                if (_jsonOutput || _tsOutput)
                    OutputData(new { total = namespaces.Count, namespaces = namespaces.Take(_limit) }, "NamespacesResult");
                else
                {
                    Output($"Found {namespaces.Count} namespaces:");
                    foreach (var ns in namespaces.Take(_limit))
                        Output($"  {(string.IsNullOrEmpty(ns) ? "(global)" : ns)}");
                    if (namespaces.Count > _limit) Output($"  ... and {namespaces.Count - _limit} more");
                }
                break;
            case "help":
                PrintHelp();
                break;
            default:
                Output($"Unknown command: {cmd}. Type 'help' for commands.");
                break;
        }
    }

    static void SmartSearch(string query)
    {
        if (_engine == null) return;
        var classResults = _engine.SearchByClassName(query).Take(_limit).ToList();
        var methodResults = _engine.SearchMethods(query).Take(_limit).ToList();
        var fieldResults = _engine.SearchFields(query).Take(_limit).ToList();

        if (_jsonOutput || _tsOutput)
        {
            OutputData(ResultFormatter.WrapSmartSearch(query, classResults, methodResults, fieldResults, _limit), "SmartSearchResult");
        }
        else
        {
            if (classResults.Count > 0)
            {
                Output($"CLASSES ({classResults.Count}):");
                foreach (var c in classResults) Output($"  {ResultFormatter.FormatClassCompact(c)}");
            }
            if (methodResults.Count > 0)
            {
                Output($"METHODS ({methodResults.Count}):");
                foreach (var m in methodResults) Output($"  {ResultFormatter.FormatMethod(m.Class, m.Method)}");
            }
            if (fieldResults.Count > 0)
            {
                Output($"FIELDS ({fieldResults.Count}):");
                foreach (var f in fieldResults) Output($"  {ResultFormatter.FormatField(f.Class, f.Field)}");
            }
            if (classResults.Count == 0 && methodResults.Count == 0 && fieldResults.Count == 0)
                Output("No results found.");
        }
    }

    static void OutputClassResults(string query, string searchType, List<Il2CppClass> results)
    {
        if (_jsonOutput || _tsOutput)
            OutputData(ResultFormatter.WrapClassResults(query, searchType, results, _limit), "ClassSearchResult");
        else
        {
            Output($"Found {results.Count} results:");
            foreach (var c in results.Take(_limit)) Output(ResultFormatter.FormatClassCompact(c));
            if (results.Count > _limit) Output($"... and {results.Count - _limit} more");
        }
    }

    static void OutputFieldResults(string query, List<(Il2CppClass Class, Il2CppField Field)> results)
    {
        if (_jsonOutput || _tsOutput)
            OutputData(ResultFormatter.WrapFieldResults(query, results, _limit), "FieldSearchResult");
        else
        {
            Output($"Found {results.Count} fields:");
            foreach (var f in results.Take(_limit)) Output(ResultFormatter.FormatField(f.Class, f.Field));
            if (results.Count > _limit) Output($"... and {results.Count - _limit} more");
        }
    }

    static void OutputMethodResults(string query, List<(Il2CppClass Class, Il2CppMethod Method)> results)
    {
        if (_jsonOutput || _tsOutput)
            OutputData(ResultFormatter.WrapMethodResults(query, results, _limit), "MethodSearchResult");
        else
        {
            Output($"Found {results.Count} methods:");
            foreach (var m in results.Take(_limit)) Output(ResultFormatter.FormatMethod(m.Class, m.Method));
            if (results.Count > _limit) Output($"... and {results.Count - _limit} more");
        }
    }
}
