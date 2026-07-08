namespace PlcSourceExporter.Core;

public static class BlockCategoryResolver
{
    public static ExportCategory ResolveBlockCategory(string siemensTypeName, string objectName)
    {
        var typeName = siemensTypeName ?? string.Empty;
        var name = objectName ?? string.Empty;

        if (IsTypeName(typeName, "OB") || ContainsAny(typeName, "OrganizationBlock") || HasPrefix(name, "OB"))
        {
            return ExportCategory.OrganizationBlock;
        }

        if (IsTypeName(typeName, "DB") || ContainsAny(typeName, "DataBlock", "GlobalDB", "InstanceDB", "ArrayDB") || HasPrefix(name, "DB"))
        {
            return ExportCategory.DataBlock;
        }

        if (IsTypeName(typeName, "FB") || ContainsAny(typeName, "FunctionBlock") || HasPrefix(name, "FB"))
        {
            return ExportCategory.FunctionBlock;
        }

        if (IsTypeName(typeName, "FC") || ContainsAny(typeName, "Function") || HasPrefix(name, "FC"))
        {
            return ExportCategory.Function;
        }

        return ExportCategory.Function;
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool IsTypeName(string value, string typeName)
    {
        return string.Equals(value, typeName, StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
