using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using AgentDump.Models;

namespace AgentDump.Output;

public static class TypeExtensions
{
    public static bool IsAnonymousType(this Type type)
    {
        return type.Name.Contains("AnonymousType") || 
               (type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Length > 0 &&
                type.Name.Contains("<>"));
    }
}

public static class ResultFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private class MinimalClass
    {
        public string FullName { get; set; } = "";
        public string? BaseClass { get; set; }
        public List<string>? Interfaces { get; set; }
        public string Modifiers { get; set; } = "";
        public string ClassType { get; set; } = "";
        public List<MinimalField>? Fields { get; set; }
        public List<MinimalMethod>? Methods { get; set; }
    }

    private class MinimalField
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string? Modifiers { get; set; }
    }

    private class MinimalMethod
    {
        public string Name { get; set; } = "";
        public string ReturnType { get; set; } = "";
        public string? Modifiers { get; set; }
        public List<MinimalParam>? Parameters { get; set; }
    }

    private class MinimalParam
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
    }

    private static MinimalClass ToMinimal(Il2CppClass cls) => new()
    {
        FullName = cls.FullName,
        BaseClass = string.IsNullOrEmpty(cls.BaseClass) ? null : cls.BaseClass,
        Interfaces = cls.Interfaces.Count > 0 ? cls.Interfaces : null,
        Modifiers = cls.Modifiers,
        ClassType = cls.ClassType,
        Fields = cls.Fields.Count > 0 ? cls.Fields.Select(f => new MinimalField 
        { 
            Name = f.Name, 
            Type = f.Type, 
            Modifiers = string.IsNullOrEmpty(f.Modifiers) ? null : f.Modifiers 
        }).ToList() : null,
        Methods = cls.Methods.Count > 0 ? cls.Methods.Select(m => new MinimalMethod 
        { 
            Name = m.Name, 
            ReturnType = m.ReturnType, 
            Modifiers = string.IsNullOrEmpty(m.Modifiers) ? null : m.Modifiers,
            Parameters = m.Parameters.Count > 0 ? m.Parameters.Select(p => new MinimalParam { Name = p.Name, Type = p.Type }).ToList() : null
        }).ToList() : null
    };

    private static MinimalField ToMinimalField(Il2CppField f) => new() 
    { 
        Name = f.Name, 
        Type = f.Type, 
        Modifiers = string.IsNullOrEmpty(f.Modifiers) ? null : f.Modifiers 
    };
    
    private static MinimalMethod ToMinimalMethod(Il2CppMethod m) => new() 
    { 
        Name = m.Name, 
        ReturnType = m.ReturnType, 
        Modifiers = string.IsNullOrEmpty(m.Modifiers) ? null : m.Modifiers,
        Parameters = m.Parameters.Count > 0 ? m.Parameters.Select(p => new MinimalParam { Name = p.Name, Type = p.Type }).ToList() : null
    };

    public static string FormatClass(Il2CppClass cls, bool detailed = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{cls.ClassType.ToUpper()}] {cls.FullName}");
        sb.AppendLine($"  Modifiers: {cls.Modifiers}");
        
        if (!string.IsNullOrEmpty(cls.BaseClass))
            sb.AppendLine($"  Inherits: {cls.BaseClass}");
        
        if (cls.Interfaces.Count > 0)
            sb.AppendLine($"  Implements: {string.Join(", ", cls.Interfaces)}");

        sb.AppendLine($"  Fields: {cls.Fields.Count} | Methods: {cls.Methods.Count}");

        if (detailed)
        {
            if (cls.Fields.Count > 0)
            {
                sb.AppendLine("\n  === FIELDS ===");
                foreach (var f in cls.Fields)
                {
                    var mod = string.IsNullOrEmpty(f.Modifiers) ? "" : $"[{f.Modifiers}] ";
                    sb.AppendLine($"    {mod}{f.Type} {f.Name}");
                }
            }

            if (cls.Methods.Count > 0)
            {
                sb.AppendLine("\n  === METHODS ===");
                foreach (var m in cls.Methods)
                {
                    var mod = string.IsNullOrEmpty(m.Modifiers) ? "" : $"[{m.Modifiers}] ";
                    var parms = string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    sb.AppendLine($"    {mod}{m.ReturnType} {m.Name}({parms})");
                }
            }
        }

        return sb.ToString();
    }

    public static string FormatClassCompact(Il2CppClass cls)
    {
        return $"[{cls.ClassType}] {cls.FullName} (fields:{cls.Fields.Count}, methods:{cls.Methods.Count})";
    }

    public static string FormatField(Il2CppClass cls, Il2CppField field)
    {
        var mod = string.IsNullOrEmpty(field.Modifiers) ? "" : $"[{field.Modifiers}] ";
        return $"{cls.FullName}.{field.Name}: {mod}{field.Type}";
    }

    public static string FormatMethod(Il2CppClass cls, Il2CppMethod method)
    {
        var mod = string.IsNullOrEmpty(method.Modifiers) ? "" : $"[{method.Modifiers}] ";
        var parms = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
        return $"{cls.FullName}.{method.Name}({parms}): {mod}{method.ReturnType}";
    }

    public static string ToJson(object obj) => JsonSerializer.Serialize(obj, JsonOptions);

    public static string ToTypeScript(object obj, string typeName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"const result: {typeName} = {{");
        WriteObjectAsTs(sb, obj, 1);
        sb.AppendLine("};");
        return sb.ToString();
    }

    private static void WriteObjectAsTs(StringBuilder sb, object? obj, int indent)
    {
        if (obj == null) { sb.Append("null"); return; }

        var type = obj.GetType();
        var ind = new string(' ', indent * 2);
        var ind2 = new string(' ', (indent + 1) * 2);

        if (obj is string s)
            sb.Append($"\"{EscapeString(s)}\"");
        else if (obj is int or long or float or double or decimal)
            sb.Append(obj);
        else if (obj is bool b)
            sb.Append(b ? "true" : "false");
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (System.Collections.IList)obj;
            if (list.Count == 0) { sb.Append("[]"); return; }
            sb.AppendLine("[");
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append(ind2);
                WriteObjectAsTs(sb, list[i], indent + 1);
                if (i < list.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.Append(ind + "]");
        }
        else if (type.IsClass || type.IsAnonymousType())
        {
            var props = type.GetProperties();
            if (props.Length == 0) { sb.Append("{}"); return; }
            sb.AppendLine("{");
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                var val = prop.GetValue(obj);
                var name = char.ToLower(prop.Name[0]) + prop.Name[1..];
                sb.Append($"{ind2}{name}: ");
                WriteObjectAsTs(sb, val, indent + 1);
                if (i < props.Length - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.Append(ind + "}");
        }
        else
            sb.Append(obj.ToString());
    }

    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

    public static object WrapClassResults(string query, string searchType, List<Il2CppClass> results, int limit = 50)
    {
        return new
        {
            query,
            searchType,
            totalFound = results.Count,
            returned = Math.Min(results.Count, limit),
            classes = results.Take(limit).Select(ToMinimal).ToList()
        };
    }

    public static object WrapFieldResults(string query, List<(Il2CppClass Class, Il2CppField Field)> results, int limit = 50)
    {
        return new
        {
            query,
            totalFound = results.Count,
            returned = Math.Min(results.Count, limit),
            matches = results.Take(limit).Select(r => new
            {
                className = r.Class.FullName,
                field = ToMinimalField(r.Field)
            }).ToList()
        };
    }

    public static object WrapMethodResults(string query, List<(Il2CppClass Class, Il2CppMethod Method)> results, int limit = 50)
    {
        return new
        {
            query,
            totalFound = results.Count,
            returned = Math.Min(results.Count, limit),
            matches = results.Take(limit).Select(r => new
            {
                className = r.Class.FullName,
                method = ToMinimalMethod(r.Method)
            }).ToList()
        };
    }

    public static object WrapClassDetail(string query, Il2CppClass cls)
    {
        return new { query, type = "class_detail", @class = ToMinimal(cls) };
    }

    public static object WrapSmartSearch(string query, List<Il2CppClass> classes, List<(Il2CppClass Class, Il2CppMethod Method)> methods, List<(Il2CppClass Class, Il2CppField Field)> fields, int limit)
    {
        return new
        {
            query,
            type = "smart_search",
            classes = new { count = classes.Count, items = classes.Take(limit).Select(ToMinimal).ToList() },
            methods = new { count = methods.Count, items = methods.Take(limit).Select(m => new { className = m.Class.FullName, method = ToMinimalMethod(m.Method) }).ToList() },
            fields = new { count = fields.Count, items = fields.Take(limit).Select(f => new { className = f.Class.FullName, field = ToMinimalField(f.Field) }).ToList() }
        };
    }
}
