using System.Text.RegularExpressions;
using AgentDump.Models;

namespace AgentDump.Parser;

public class Il2CppDumpParser
{
    private static readonly Regex NamespaceRegex = new(@"^// Namespace:\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex ClassRegex = new(@"^(public|private|internal|protected)?\s*(static|sealed|abstract)?\s*(class|struct|enum|interface)\s+(\S+)(?:\s*:\s*(.+))?\s*//\s*TypeDefIndex:\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex FieldRegex = new(@"^\s*(public|private|protected|internal)?\s*(static|readonly|const)?\s*(readonly)?\s*(.+?)\s+(\S+);\s*//\s*(0x[0-9A-Fa-f]+)", RegexOptions.Compiled);
    private static readonly Regex MethodRegex = new(@"^\s*//\s*RVA:\s*(0x[0-9A-Fa-f]+|-1)\s+Offset:\s*(0x[0-9A-Fa-f]+|-1)\s+VA:\s*0x[0-9A-Fa-f]+(?:\s+Slot:\s*(\d+))?", RegexOptions.Compiled);
    private static readonly Regex MethodSigRegex = new(@"^\s*(public|private|protected|internal)?\s*(static|virtual|override|abstract|sealed)?\s*(override|virtual)?\s*(.+?)\s+(\S+)\s*\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex EnumValueRegex = new(@"^\s*public const \S+ (\S+)\s*=\s*(.+);", RegexOptions.Compiled);

    public List<Il2CppClass> Parse(string filePath)
    {
        var classes = new List<Il2CppClass>();
        var lines = File.ReadAllLines(filePath);
        
        string currentNamespace = "";
        Il2CppClass? currentClass = null;
        string? pendingMethodSig = null;
        int braceDepth = 0;
        bool inClass = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            var nsMatch = NamespaceRegex.Match(trimmed);
            if (nsMatch.Success)
            {
                currentNamespace = nsMatch.Groups[1].Value.Trim();
                continue;
            }

            var classMatch = ClassRegex.Match(trimmed);
            if (classMatch.Success)
            {
                if (currentClass != null) classes.Add(currentClass);

                currentClass = new Il2CppClass
                {
                    Namespace = currentNamespace,
                    Modifiers = $"{classMatch.Groups[1].Value} {classMatch.Groups[2].Value}".Trim(),
                    ClassType = classMatch.Groups[3].Value,
                    Name = classMatch.Groups[4].Value,
                    TypeDefIndex = int.Parse(classMatch.Groups[6].Value),
                    StartLine = i + 1
                };

                if (classMatch.Groups[5].Success)
                {
                    var inheritance = classMatch.Groups[5].Value.Split(',').Select(s => s.Trim()).ToList();
                    if (inheritance.Count > 0)
                    {
                        currentClass.BaseClass = inheritance[0];
                        currentClass.Interfaces = inheritance.Skip(1).ToList();
                    }
                }
                inClass = true;
                braceDepth = 0;
                continue;
            }

            if (inClass && currentClass != null)
            {
                if (trimmed == "{") braceDepth++;
                if (trimmed == "}")
                {
                    braceDepth--;
                    if (braceDepth == 0)
                    {
                        currentClass.EndLine = i + 1;
                        inClass = false;
                    }
                }

                var fieldMatch = FieldRegex.Match(line);
                if (fieldMatch.Success && !line.Contains("("))
                {
                    currentClass.Fields.Add(new Il2CppField
                    {
                        Modifiers = $"{fieldMatch.Groups[1].Value} {fieldMatch.Groups[2].Value} {fieldMatch.Groups[3].Value}".Trim(),
                        Type = fieldMatch.Groups[4].Value.Trim(),
                        Name = fieldMatch.Groups[5].Value,
                        Offset = fieldMatch.Groups[6].Value
                    });
                    continue;
                }

                var enumMatch = EnumValueRegex.Match(line);
                if (enumMatch.Success && currentClass.ClassType == "enum")
                {
                    currentClass.Fields.Add(new Il2CppField
                    {
                        Name = enumMatch.Groups[1].Value,
                        DefaultValue = enumMatch.Groups[2].Value,
                        Type = "enum"
                    });
                    continue;
                }

                var methodSigMatch = MethodSigRegex.Match(line);
                if (methodSigMatch.Success && !line.Contains("//"))
                {
                    pendingMethodSig = line;
                    continue;
                }

                var methodMatch = MethodRegex.Match(line);
                if (methodMatch.Success && pendingMethodSig != null)
                {
                    var sigMatch = MethodSigRegex.Match(pendingMethodSig);
                    if (sigMatch.Success)
                    {
                        var method = new Il2CppMethod
                        {
                            Modifiers = $"{sigMatch.Groups[1].Value} {sigMatch.Groups[2].Value} {sigMatch.Groups[3].Value}".Trim(),
                            ReturnType = sigMatch.Groups[4].Value.Trim(),
                            Name = sigMatch.Groups[5].Value,
                            RVA = methodMatch.Groups[1].Value,
                            Offset = methodMatch.Groups[2].Value,
                            Slot = methodMatch.Groups[3].Success ? int.Parse(methodMatch.Groups[3].Value) : null
                        };

                        var paramsStr = sigMatch.Groups[6].Value;
                        if (!string.IsNullOrWhiteSpace(paramsStr))
                        {
                            foreach (var param in paramsStr.Split(','))
                            {
                                var parts = param.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    method.Parameters.Add(new Il2CppParameter
                                    {
                                        Type = string.Join(" ", parts.Take(parts.Length - 1)),
                                        Name = parts.Last()
                                    });
                                }
                            }
                        }
                        currentClass.Methods.Add(method);
                    }
                    pendingMethodSig = null;
                }
            }
        }

        if (currentClass != null) classes.Add(currentClass);
        return classes;
    }
}
