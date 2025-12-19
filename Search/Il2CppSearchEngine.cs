using AgentDump.Models;

namespace AgentDump.Search;

public class Il2CppSearchEngine
{
    private readonly List<Il2CppClass> _classes;
    private readonly Dictionary<string, List<Il2CppClass>> _byFullName;
    private readonly Dictionary<int, Il2CppClass> _byTypeDefIndex;
    private readonly Dictionary<string, List<Il2CppClass>> _byNamespace;

    public Il2CppSearchEngine(List<Il2CppClass> classes)
    {
        _classes = classes;
        _byFullName = classes.GroupBy(c => c.FullName).ToDictionary(g => g.Key, g => g.ToList());
        _byTypeDefIndex = classes.GroupBy(c => c.TypeDefIndex).ToDictionary(g => g.Key, g => g.First());
        _byNamespace = classes.GroupBy(c => c.Namespace).ToDictionary(g => g.Key, g => g.ToList());
    }

    public List<Il2CppClass> SearchByClassName(string query, bool exact = false, bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return _classes.Where(c => exact 
            ? c.Name.Equals(query, comparison)
            : c.Name.Contains(query, comparison)).ToList();
    }

    public List<Il2CppClass> SearchByNamespace(string query, bool exact = false)
    {
        if (exact && _byNamespace.TryGetValue(query, out var ns)) return ns;
        return _classes.Where(c => c.Namespace.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public List<Il2CppClass> SearchByFullName(string query, bool exact = false)
    {
        if (exact && _byFullName.TryGetValue(query, out var clsList)) return clsList;
        return _classes.Where(c => c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public Il2CppClass? GetByTypeDefIndex(int index) => _byTypeDefIndex.GetValueOrDefault(index);

    public List<Il2CppClass> SearchByBaseClass(string baseClassName) =>
        _classes.Where(c => c.BaseClass != null && c.BaseClass.Contains(baseClassName, StringComparison.OrdinalIgnoreCase)).ToList();

    public List<Il2CppClass> SearchByInterface(string interfaceName) =>
        _classes.Where(c => c.Interfaces.Any(i => i.Contains(interfaceName, StringComparison.OrdinalIgnoreCase))).ToList();

    public List<Il2CppClass> SearchByType(string type) =>
        _classes.Where(c => c.ClassType.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

    public List<Il2CppClass> GetMonoBehaviours() => _classes.Where(c => c.IsMonoBehaviour).ToList();
    public List<Il2CppClass> GetScriptableObjects() => _classes.Where(c => c.IsScriptableObject).ToList();

    public List<(Il2CppClass Class, Il2CppField Field)> SearchFields(string query, bool exact = false)
    {
        var results = new List<(Il2CppClass, Il2CppField)>();
        foreach (var cls in _classes)
            foreach (var field in cls.Fields)
                if (exact ? field.Name.Equals(query, StringComparison.OrdinalIgnoreCase) : field.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    results.Add((cls, field));
        return results;
    }

    public List<(Il2CppClass Class, Il2CppField Field)> SearchFieldsByType(string fieldType)
    {
        var results = new List<(Il2CppClass, Il2CppField)>();
        foreach (var cls in _classes)
            foreach (var field in cls.Fields)
                if (field.Type.Contains(fieldType, StringComparison.OrdinalIgnoreCase))
                    results.Add((cls, field));
        return results;
    }

    public List<(Il2CppClass Class, Il2CppField Field)> SearchByOffset(string offset)
    {
        var results = new List<(Il2CppClass, Il2CppField)>();
        foreach (var cls in _classes)
            foreach (var field in cls.Fields)
                if (field.Offset != null && field.Offset.Equals(offset, StringComparison.OrdinalIgnoreCase))
                    results.Add((cls, field));
        return results;
    }

    public List<(Il2CppClass Class, Il2CppMethod Method)> SearchMethods(string query, bool exact = false)
    {
        var results = new List<(Il2CppClass, Il2CppMethod)>();
        foreach (var cls in _classes)
            foreach (var method in cls.Methods)
                if (exact ? method.Name.Equals(query, StringComparison.OrdinalIgnoreCase) : method.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    results.Add((cls, method));
        return results;
    }

    public List<(Il2CppClass Class, Il2CppMethod Method)> SearchMethodsByReturnType(string returnType)
    {
        var results = new List<(Il2CppClass, Il2CppMethod)>();
        foreach (var cls in _classes)
            foreach (var method in cls.Methods)
                if (method.ReturnType.Contains(returnType, StringComparison.OrdinalIgnoreCase))
                    results.Add((cls, method));
        return results;
    }

    public List<(Il2CppClass Class, Il2CppMethod Method)> SearchMethodsByParameterType(string paramType)
    {
        var results = new List<(Il2CppClass, Il2CppMethod)>();
        foreach (var cls in _classes)
            foreach (var method in cls.Methods)
                if (method.Parameters.Any(p => p.Type.Contains(paramType, StringComparison.OrdinalIgnoreCase)))
                    results.Add((cls, method));
        return results;
    }

    public List<(Il2CppClass Class, Il2CppMethod Method)> SearchByRVA(string rva)
    {
        var results = new List<(Il2CppClass, Il2CppMethod)>();
        foreach (var cls in _classes)
            foreach (var method in cls.Methods)
                if (method.RVA != null && method.RVA.Equals(rva, StringComparison.OrdinalIgnoreCase))
                    results.Add((cls, method));
        return results;
    }

    public List<(Il2CppClass Class, Il2CppMethod Method)> SearchMethodsByOffset(string offset)
    {
        var results = new List<(Il2CppClass, Il2CppMethod)>();
        foreach (var cls in _classes)
            foreach (var method in cls.Methods)
                if (method.Offset != null && method.Offset.Equals(offset, StringComparison.OrdinalIgnoreCase))
                    results.Add((cls, method));
        return results;
    }

    public List<Il2CppClass> FindClassesWithMethod(string methodName, string? returnType = null, int? paramCount = null) =>
        _classes.Where(c => c.Methods.Any(m =>
            m.Name.Contains(methodName, StringComparison.OrdinalIgnoreCase) &&
            (returnType == null || m.ReturnType.Contains(returnType, StringComparison.OrdinalIgnoreCase)) &&
            (paramCount == null || m.Parameters.Count == paramCount.Value))).ToList();

    public List<Il2CppClass> FindClassesWithField(string fieldName, string? fieldType = null) =>
        _classes.Where(c => c.Fields.Any(f =>
            f.Name.Contains(fieldName, StringComparison.OrdinalIgnoreCase) &&
            (fieldType == null || f.Type.Contains(fieldType, StringComparison.OrdinalIgnoreCase)))).ToList();

    public List<Il2CppClass> GetDerivedClasses(string baseClassName)
    {
        var baseClass = _classes.FirstOrDefault(c => c.Name.Equals(baseClassName, StringComparison.OrdinalIgnoreCase));
        if (baseClass == null) return new List<Il2CppClass>();
        return _classes.Where(c => c.BaseClass?.Contains(baseClassName, StringComparison.OrdinalIgnoreCase) == true).ToList();
    }

    public List<string> GetInheritanceChain(string className)
    {
        var chain = new List<string>();
        var current = _classes.FirstOrDefault(c => c.Name.Equals(className, StringComparison.OrdinalIgnoreCase));
        while (current != null)
        {
            chain.Add(current.FullName);
            if (string.IsNullOrEmpty(current.BaseClass)) break;
            current = _classes.FirstOrDefault(c => c.Name.Equals(current.BaseClass, StringComparison.OrdinalIgnoreCase) ||
                                                    c.FullName.Equals(current.BaseClass, StringComparison.OrdinalIgnoreCase));
        }
        return chain;
    }

    public IEnumerable<string> GetAllNamespaces() => _byNamespace.Keys.OrderBy(n => n);

    public DumpStats GetStats() => new DumpStats
    {
        TotalClasses = _classes.Count,
        TotalMethods = _classes.Sum(c => c.Methods.Count),
        TotalFields = _classes.Sum(c => c.Fields.Count),
        TotalProperties = _classes.Sum(c => c.Properties.Count),
        TotalNamespaces = _byNamespace.Count,
        TotalInterfaces = _classes.Count(c => c.ClassType == "interface"),
        TotalEnums = _classes.Count(c => c.ClassType == "enum"),
        TotalStructs = _classes.Count(c => c.ClassType == "struct"),
        MonoBehaviours = _classes.Count(c => c.IsMonoBehaviour),
        ScriptableObjects = _classes.Count(c => c.IsScriptableObject)
    };

    public int TotalClasses => _classes.Count;
    public int TotalMethods => _classes.Sum(c => c.Methods.Count);
    public int TotalFields => _classes.Sum(c => c.Fields.Count);
}
