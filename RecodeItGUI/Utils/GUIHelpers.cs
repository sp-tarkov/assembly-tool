using ReCodeItLib.Models;
using ReCodeItLib.Utils;

namespace ReCodeIt.GUI;

internal static class GUIHelpers
{
    /// <summary>
    /// Returns the value of the count or null if disabled
    /// </summary>
    /// <param name="box"></param>
    /// <returns></returns>
    public static int? GetCount(this CheckBox box, NumericUpDown upDown)
    {
        if (box.Checked)
        {
            return (int?)upDown.Value;
        }

        return null;
    }

    public static bool? GetEnabled(this DomainUpDown domainUpDown)
    {
        if (domainUpDown.Text == "True")
        {
            return true;
        }
        else if (domainUpDown.Text == "False")
        {
            return false;
        }

        return null;
    }

    /// <summary>
    /// Builds the name list for the this updown
    /// </summary>
    /// <param name="domainUpDown"></param>
    /// <param name="name"></param>
    public static void BuildStringList(this DomainUpDown domainUpDown, string name, bool required, bool? update = null)
    {
        domainUpDown.Items.Clear();

        domainUpDown.Text = required
            ? name + @" (Required)"
            : name + @" (Disabled)";

        domainUpDown.ReadOnly = true;

        var list = new List<string>
        {
            name + " (Disabled)",
            "True",
            "False",
        };

        if (required)
        {
            list.RemoveAt(0);
        }

        if (update != null)
        {
            domainUpDown.Text = update.ToString();

            if (update.ToString() == "True")
            {
                Logger.Log("Updating!");
                domainUpDown.SelectedItem = "True";
            }
            else
            {
                domainUpDown.SelectedItem = "False";
            }
        }

        domainUpDown.Items.AddRange(list);
    }

    public static void AddItemsToComboBox(this ComboBox cb, List<string> items)
    {
        cb.Items.Clear();

        foreach (var item in items)
        {
            cb.Items.Add(item);
        }
    }

    public static T? GetSelectedItem<T>(this ComboBox cb)
    {
        return (T)cb.SelectedItem;
    }

    /// <summary>
    /// Generates a tree node to display on the GUI
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public static TreeNode GenerateTreeNode(RemapModel model, ReCodeItForm gui)
    {
        var isPublic = model.SearchParams.GenericParams.IsPublic;
        var isAbstract = model.SearchParams.GenericParams.IsAbstract == null ? null : model.SearchParams.GenericParams.IsAbstract;
        var isInterface = model.SearchParams.GenericParams.IsInterface == null ? null : model.SearchParams.GenericParams.IsInterface;
        var isStruct = model.SearchParams.GenericParams.IsStruct == null ? null : model.SearchParams.GenericParams.IsStruct;
        var isEnum = model.SearchParams.GenericParams.IsEnum == null ? null : model.SearchParams.GenericParams.IsEnum;
        var isNested = model.SearchParams.NestedTypes.IsNested == null ? null : model.SearchParams.NestedTypes.IsNested;
        var isSealed = model.SearchParams.GenericParams.IsSealed == null ? null : model.SearchParams.GenericParams.IsSealed;
        var HasAttribute = model.SearchParams.GenericParams.HasAttribute == null ? null : model.SearchParams.GenericParams.HasAttribute;
        var IsDerived = model.SearchParams.GenericParams.IsDerived == null ? null : model.SearchParams.GenericParams.IsDerived;
        var HasGenericParameters = model.SearchParams.GenericParams.HasGenericParameters == null ? null : model.SearchParams.GenericParams.HasGenericParameters;

        var remapTreeItem = new TreeNode($"{model.NewTypeName}");

        var originalTypeName = new TreeNode($"Original Name: {model.OriginalTypeName}");

        remapTreeItem.Nodes.Add(originalTypeName);

        if (model.UseForceRename)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"Force Rename: {model.UseForceRename}"));
        }

        remapTreeItem.Nodes.Add(new TreeNode($"IsPublic: {isPublic}"));

        if (isAbstract is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"IsAbstract: {isAbstract}"));
        }

        if (isInterface is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"IsInterface: {isInterface}"));
        }

        if (isStruct is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"IsStruct: {isStruct}"));
        }

        if (isEnum is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"IsEnum: {isEnum}"));
        }

        if (isNested is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"IsNested: {isNested}"));
        }

        if (isSealed is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"IsSealed: {isSealed}"));
        }

        if (HasAttribute is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"HasAttribute: {HasAttribute}"));
        }

        if (IsDerived is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"IsDerived: {IsDerived}"));
        }

        if (HasGenericParameters is not null)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"HasGenericParameters: {HasGenericParameters}"));
        }

        if (model.SearchParams.Methods.ConstructorParameterCount > 0)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"Constructor Parameter Count: {model.SearchParams.Methods.ConstructorParameterCount}"));
        }

        if (model.SearchParams.Methods.MethodCount >= 0)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"Method Count: {model.SearchParams.Methods.MethodCount}"));
        }

        if (model.SearchParams.Fields.FieldCount >= 0)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"Field Count: {model.SearchParams.Fields.FieldCount}"));
        }

        if (model.SearchParams.Properties.PropertyCount >= 0)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"Property Count: {model.SearchParams.Properties.PropertyCount}"));
        }

        if (model.SearchParams.NestedTypes.NestedTypeCount >= 0)
        {
            remapTreeItem.Nodes.Add(new TreeNode($"Nested OriginalTypeRef Count: {model.SearchParams.NestedTypes.NestedTypeCount}"));
        }

        if (model.SearchParams.Methods.IncludeMethods.Count > 0)
        {
            var includeMethodsNode =
                GenerateNodeFromList(model.SearchParams.Methods.IncludeMethods, "Include Methods");

            remapTreeItem.Nodes.Add(includeMethodsNode);
        }

        if (model.SearchParams.Methods.ExcludeMethods?.Count > 0)
        {
            var excludeMethodsNode =
                GenerateNodeFromList(model.SearchParams.Methods.ExcludeMethods, "Exclude Methods");

            remapTreeItem.Nodes.Add(excludeMethodsNode);
        }

        if (model.SearchParams.Fields.IncludeFields?.Count > 0)
        {
            var includeFieldsNode =
                GenerateNodeFromList(model.SearchParams.Fields.IncludeFields, "Include Fields");

            remapTreeItem.Nodes.Add(includeFieldsNode);
        }

        if (model.SearchParams.Fields.ExcludeFields?.Count > 0)
        {
            var excludeFieldsNode =
                GenerateNodeFromList(model.SearchParams.Fields.ExcludeFields, "Exclude Fields");

            remapTreeItem.Nodes.Add(excludeFieldsNode);
        }

        if (model.SearchParams.Properties.IncludeProperties?.Count > 0)
        {
            var includeProperties =
                GenerateNodeFromList(model.SearchParams.Properties.IncludeProperties, "Include Properties");

            remapTreeItem.Nodes.Add(includeProperties);
        }

        if (model.SearchParams.Properties.ExcludeProperties?.Count > 0)
        {
            var excludeProperties =
                GenerateNodeFromList(model.SearchParams.Properties.ExcludeProperties, "Exclude Properties");

            remapTreeItem.Nodes.Add(excludeProperties);
        }

        if (model.SearchParams.NestedTypes.IncludeNestedTypes?.Count > 0)
        {
            var includeNestedTypes =
                GenerateNodeFromList(model.SearchParams.NestedTypes.IncludeNestedTypes, "Include Nested Types");

            remapTreeItem.Nodes.Add(includeNestedTypes);
        }

        if (model.SearchParams.NestedTypes.ExcludeNestedTypes?.Count > 0)
        {
            var excludeNestedTypes =
                GenerateNodeFromList(model.SearchParams.NestedTypes.ExcludeNestedTypes, "Exclude Nested Types");

            remapTreeItem.Nodes.Add(excludeNestedTypes);
        }

        if (model.SearchParams.Events.IncludeEvents?.Count > 0)
        {
            var includeEvents =
                GenerateNodeFromList(model.SearchParams.Events.IncludeEvents, "Include Events");

            remapTreeItem.Nodes.Add(includeEvents);
        }

        if (model.SearchParams.Events.ExcludeEvents?.Count > 0)
        {
            var excludeEvents =
                GenerateNodeFromList(model.SearchParams.Events.ExcludeEvents, "Exclude Events");

            remapTreeItem.Nodes.Add(excludeEvents);
        }

        ReCodeItForm.RemapNodes.Add(remapTreeItem, model);

        return remapTreeItem;
    }

    /// <summary>
    /// Generates a new node from a list of strings
    /// </summary>
    /// <param name="items"></param>
    /// <param name="name"></param>
    /// <returns>A new tree node, or null if the provided list is empty</returns>
    private static TreeNode GenerateNodeFromList(HashSet<string> items, string name)
    {
        var node = new TreeNode(name);

        foreach (var item in items)
        {
            node.Nodes.Add(item);
        }

        return node;
    }

    /// <summary>
    /// Buils a list of strings from list box entries
    /// </summary>
    /// <param name="lb"></param>
    /// <returns></returns>
    public static List<string> GetAllEntriesFromListBox(ListBox lb)
    {
        var tmp = new List<string>();

        foreach (var entry in lb.Items)
        {
            tmp.Add((string)entry);
        }

        return tmp;
    }

    /// <summary>
    /// Opens and returns a path from a file dialogue
    /// </summary>
    /// <param name="title"></param>
    /// <param name="filter"></param>
    /// <returns>Path if selected, or empty string</returns>
    public static string OpenFileDialog(string title, string filter)
    {
        OpenFileDialog fDialog = new()
        {
            Title = title,
            Filter = filter,
            Multiselect = false
        };

        if (fDialog.ShowDialog() == DialogResult.OK)
        {
            return fDialog.FileName;
        }

        return string.Empty;
    }

    /// <summary>
    /// Opens and returns a path from a folder dialogue
    /// </summary>
    /// <param name="description"></param>
    /// <returns>Path if selected, or empty string</returns>
    public static string OpenFolderDialog(string description)
    {
        using FolderBrowserDialog fDialog = new();

        fDialog.Description = description;
        fDialog.ShowNewFolderButton = true;

        if (fDialog.ShowDialog() == DialogResult.OK)
        {
            return fDialog.SelectedPath;
        }

        return string.Empty;
    }
}