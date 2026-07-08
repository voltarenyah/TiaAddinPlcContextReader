namespace PlcSourceExporter.Core;

public enum ExportCategory
{
    OrganizationBlock,
    Function,
    FunctionBlock,
    DataBlock,
    UserDataType,
    TagTable
}

public sealed class ExportCategoryInfo
{
    public ExportCategoryInfo(ExportCategory category, string folderName, string displayName)
    {
        Category = category;
        FolderName = folderName;
        DisplayName = displayName;
    }

    public ExportCategory Category { get; }

    public string FolderName { get; }

    public string DisplayName { get; }
}

public static class ExportCategories
{
    public static readonly IReadOnlyList<ExportCategoryInfo> All = new ExportCategoryInfo[]
    {
        new ExportCategoryInfo(ExportCategory.OrganizationBlock, "Blocks", "OB"),
        new ExportCategoryInfo(ExportCategory.Function, "Blocks", "FC"),
        new ExportCategoryInfo(ExportCategory.FunctionBlock, "Blocks", "FB"),
        new ExportCategoryInfo(ExportCategory.DataBlock, "DB", "DB"),
        new ExportCategoryInfo(ExportCategory.UserDataType, "UDT", "UDT"),
        new ExportCategoryInfo(ExportCategory.TagTable, "Tags", "Tags")
    };

    public static string GetFolderName(ExportCategory category)
    {
        return All.First(item => item.Category == category).FolderName;
    }

    public static string GetDisplayName(ExportCategory category)
    {
        return All.First(item => item.Category == category).DisplayName;
    }
}
