namespace AgentDump.Models;

public class Il2CppClass
{
    public string Namespace { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
    public int TypeDefIndex { get; set; }
    public string? BaseClass { get; set; }
    public List<string> Interfaces { get; set; } = new();
    public List<Il2CppField> Fields { get; set; } = new();
    public List<Il2CppProperty> Properties { get; set; } = new();
    public List<Il2CppMethod> Methods { get; set; } = new();
    public List<Il2CppClass> NestedClasses { get; set; } = new();
    public string Modifiers { get; set; } = "";
    public string ClassType { get; set; } = "class";
    public int StartLine { get; set; }
    public int EndLine { get; set; }

    public bool IsStatic => Modifiers.Contains("static");
    public bool IsAbstract => Modifiers.Contains("abstract");
    public bool IsSealed => Modifiers.Contains("sealed");
    public bool IsPublic => Modifiers.Contains("public");
    public bool IsMonoBehaviour => BaseClass?.Contains("MonoBehaviour") == true;
    public bool IsScriptableObject => BaseClass?.Contains("ScriptableObject") == true;
    public int FieldCount => Fields.Count;
    public int MethodCount => Methods.Count;
    public int PropertyCount => Properties.Count;
}

public class Il2CppField
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Modifiers { get; set; } = "";
    public string? Offset { get; set; }
    public string? DefaultValue { get; set; }

    public bool IsStatic => Modifiers.Contains("static");
    public bool IsPublic => Modifiers.Contains("public");
    public bool IsPrivate => Modifiers.Contains("private");
    public bool IsReadOnly => Modifiers.Contains("readonly");
    public bool IsConst => Modifiers.Contains("const");
}

public class Il2CppProperty
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Modifiers { get; set; } = "";
    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
}

public class Il2CppMethod
{
    public string Name { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public string Modifiers { get; set; } = "";
    public List<Il2CppParameter> Parameters { get; set; } = new();
    public string? RVA { get; set; }
    public string? Offset { get; set; }
    public int? Slot { get; set; }

    public bool IsStatic => Modifiers.Contains("static");
    public bool IsVirtual => Modifiers.Contains("virtual");
    public bool IsAbstract => Modifiers.Contains("abstract");
    public bool IsPublic => Modifiers.Contains("public");
    public bool IsPrivate => Modifiers.Contains("private");
    public bool IsOverride => Modifiers.Contains("override");
    public int ParameterCount => Parameters.Count;
    public string Signature => $"{ReturnType} {Name}({string.Join(", ", Parameters.Select(p => $"{p.Type} {p.Name}"))})";
}

public class Il2CppParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public class DumpStats
{
    public int TotalClasses { get; set; }
    public int TotalMethods { get; set; }
    public int TotalFields { get; set; }
    public int TotalProperties { get; set; }
    public int TotalNamespaces { get; set; }
    public int TotalInterfaces { get; set; }
    public int TotalEnums { get; set; }
    public int TotalStructs { get; set; }
    public int MonoBehaviours { get; set; }
    public int ScriptableObjects { get; set; }
}
