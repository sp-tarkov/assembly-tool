using ReCodeItLib.Models;
using ReCodeItLib.ReMapper;
using ReCodeItLib.Utils;
using System.Diagnostics;

namespace ReCodeIt.GUI;

public partial class ReCodeItForm : Form
{
    private static ReCodeItRemapper Remapper { get; set; } = new();
    private static Settings AppSettings => DataProvider.Settings;

    private bool _isSearched = false;
    public static Dictionary<TreeNode, RemapModel> RemapNodes = [];

    private int _selectedRemapTreeIndex = 0;
    private int _selectedCCRemapTreeIndex = 0;

    private List<string> _cachedNewTypeNames = [];

    public ReCodeItForm()
    {
        InitializeComponent();


        SubscribeToEvents();
        PopulateDomainUpDowns();
        RefreshSettingsPage();
        LoadMappingFile();

        var remaps = DataProvider.Remaps;

        ReloadRemapTreeView(remaps);
    }

    private void SubscribeToEvents()
    {
        RemapTreeView.NodeMouseDoubleClick += ManualEditSelectedRemap;
        Remapper.OnComplete += ReloadTreeAfterMapping;

        #region MANUAL_REMAPPER

        NewTypeName.GotFocus += (sender, e) =>
        {
            _cachedNewTypeNames.Add(NewTypeName.Text);
        };

        IncludeMethodTextBox.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                MethodIncludeAddButton_Click(sender, e);
            }
        };

        ExcludeMethodTextBox.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                MethodExcludeAddButton_Click(sender, e);
            }
        };

        FieldsIncludeTextInput.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                FIeldIncludeAddButton_Click(sender, e);
            }
        };

        FieldsExcludeTextInput.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                FieldExcludeAddButton_Click(sender, e);
            }
        };

        PropertiesIncludeTextField.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                PropertiesIncludeAddButton_Click(sender, e);
            }
        };

        PropertiesExcludeTextField.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                PropertiesExcludeAddButton_Click(sender, e);
            }
        };

        NestedTypesIncludeTextField.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                NestedTypesAddButton_Click(sender, e);
            }
        };

        NestedTypesExcludeTextField.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                NestedTypesExlcudeAddButton_Click(sender, e);
            }
        };

        EventsIncludeTextField.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                EventsAddButton_Click(sender, e);
            }
        };

        EventsExcludeTextField.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                EventsExcludeAddButton_Click(sender, e);
            }
        };

        #endregion MANUAL_REMAPPER
    }

    #region MANUAL_REMAPPER

    private void LoadMappingFile()
    {
        DataProvider.Remaps = DataProvider.LoadMappingFile(AppSettings.Remapper.MappingPath);
        LoadedMappingFilePath.Text = AppSettings.Remapper.MappingPath;
    }
    
    #region BUTTONS

    #region MAIN_BUTTONS

    private void SearchTreeView(object sender, EventArgs e)
    {
        if (RemapTreeView.Nodes.Count == 0) { return; }
        if (RMSearchBox.Text == string.Empty) { return; }

        bool projectMode = AppSettings.Remapper.UseProjectMappings;

        var remaps = DataProvider.Remaps;

        var matches = remaps
            .Where(x => x.NewTypeName == RMSearchBox.Text
            || x.NewTypeName.StartsWith(RMSearchBox.Text));

        if (!matches.Any()) { return; }

        RemapTreeView.Nodes.Clear();

        foreach (var match in matches)
        {
            RemapTreeView.Nodes.Add(GUIHelpers.GenerateTreeNode(match, this));
        }

        _isSearched = true;
    }

    private void ResetSearchButton_Click(object sender, EventArgs e)
    {
        bool projectMode = AppSettings.Remapper.UseProjectMappings;

        var remaps = DataProvider.Remaps;

        RemapTreeView.Nodes.Clear();
        ReloadRemapTreeView(remaps);

        RMSearchBox.Clear();
        _isSearched = false;
    }

    private RemapModel? CreateRemapFromGUI()
    {
        if (NewTypeName.Text == string.Empty)
        {
            MessageBox.Show("Please enter a new type name", "Invalid data");
            return null;
        }

        var newRemap = new RemapModel
        {
            Succeeded = false,
            NoMatchReasons = [],
            NewTypeName = NewTypeName.Text,
            OriginalTypeName = OriginalTypeName.Text == string.Empty ? null : OriginalTypeName.Text,
            UseForceRename = RemapperUseForceRename.Checked,
            SearchParams = new SearchParams
            {
                IsPublic = bool.Parse(IsPublicComboBox.GetSelectedItem<string>().AsSpan()),

                IsAbstract = IsAbstractComboBox.SelectedItem as string != "Disabled"
                    ? bool.Parse(IsAbstractComboBox.GetSelectedItem<string>().AsSpan())
                    : null,

                IsSealed = IsSealedComboBox.SelectedItem as string != "Disabled"
                    ? bool.Parse(IsSealedComboBox.GetSelectedItem<string>().AsSpan())
                    : null,

                IsInterface = IsInterfaceComboBox.SelectedItem as string != "Disabled"
                    ? bool.Parse(IsInterfaceComboBox.GetSelectedItem<string>().AsSpan())
                    : null,

                IsStruct = IsStructComboBox.SelectedItem as string != "Disabled"
                    ? bool.Parse(IsStructComboBox.GetSelectedItem<string>().AsSpan())
                    : null,

                IsEnum = IsEnumComboBox.SelectedItem as string != "Disabled"
                    ? bool.Parse(IsEnumComboBox.GetSelectedItem<string>().AsSpan())
                    : null,

                HasAttribute = HasAttributeComboBox.SelectedItem as string != "Disabled"
                    ? bool.Parse(HasAttributeComboBox.GetSelectedItem<string>().AsSpan())
                    : null,

                HasGenericParameters = HasGenericParamsComboBox.SelectedItem as string != "Disabled"
                    ? bool.Parse(HasGenericParamsComboBox.GetSelectedItem<string>().AsSpan())
                    : null,

                IsNested = IsNestedUpDown.GetEnabled(),
                IsDerived = IsDerivedUpDown.GetEnabled(),

                NTParentName = NestedTypeParentName.Text == string.Empty
                ? null
                : NestedTypeParentName.Text,

                MatchBaseClass = BaseClassIncludeTextFIeld.Text == string.Empty
                ? null
                : BaseClassIncludeTextFIeld.Text,

                IgnoreBaseClass = BaseClassExcludeTextField.Text == string.Empty
                ? null
                : BaseClassExcludeTextField.Text,

                // Constructor - TODO
                ConstructorParameterCount = ConstructorCountEnabled.GetCount(ConstuctorCountUpDown),
                MethodCount = MethodCountEnabled.GetCount(MethodCountUpDown),
                FieldCount = FieldCountEnabled.GetCount(FieldCountUpDown),
                PropertyCount = PropertyCountEnabled.GetCount(PropertyCountUpDown),
                NestedTypeCount = NestedTypeCountEnabled.GetCount(NestedTypeCountUpDown),
                IncludeMethods = GUIHelpers.GetAllEntriesFromListBox(MethodIncludeBox),
                ExcludeMethods = GUIHelpers.GetAllEntriesFromListBox(MethodExcludeBox),
                IncludeFields = GUIHelpers.GetAllEntriesFromListBox(FieldIncludeBox),
                ExcludeFields = GUIHelpers.GetAllEntriesFromListBox(FieldExcludeBox),
                IncludeProperties = GUIHelpers.GetAllEntriesFromListBox(PropertiesIncludeBox),
                ExcludeProperties = GUIHelpers.GetAllEntriesFromListBox(PropertiesExcludeBox),
                IncludeNestedTypes = GUIHelpers.GetAllEntriesFromListBox(NestedTypesIncludeBox),
                ExcludeNestedTypes = GUIHelpers.GetAllEntriesFromListBox(NestedTypesExcludeBox),
                IncludeEvents = GUIHelpers.GetAllEntriesFromListBox(EventsIncludeBox),
                ExcludeEvents = GUIHelpers.GetAllEntriesFromListBox(EventsExcludeBox)
            }
        };

        return newRemap;
    }

    /// <summary>
    /// Construct a new remap when the button is pressed
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AddRemapButton_Click(object sender, EventArgs e)
    {
        ResetSearchButton_Click(this, e);

        var newRemap = CreateRemapFromGUI();

        if (newRemap is null) return;

        bool projectMode = AppSettings.Remapper.UseProjectMappings;

        Logger.Log(projectMode);

        var remaps = DataProvider.Remaps;

        var existingRemap = remaps
            .FirstOrDefault(remap => remap.NewTypeName == newRemap.NewTypeName);

        if (existingRemap == null)
        {
            existingRemap = remaps
                .FirstOrDefault(remap => _cachedNewTypeNames.Contains(remap.NewTypeName));
        }

        // Handle overwriting an existing remap
        if (existingRemap != null)
        {
            var index = remaps.IndexOf(existingRemap);

            remaps.Remove(existingRemap);
            RemapTreeView.Nodes.RemoveAt(index);

            remaps.Insert(index, newRemap);
            RemapTreeView.Nodes.Insert(index, GUIHelpers.GenerateTreeNode(newRemap, this));

            DataProvider.SaveMapping();

            ReloadRemapTreeView(remaps);

            ResetAllRemapFields();
            return;
        }

        DataProvider.Remaps.Add(newRemap);
        DataProvider.SaveMapping();

        var node = GUIHelpers.GenerateTreeNode(newRemap, this);

        node.Clone();

        //RemapTreeView.Nodes.Remove(node);
        RemapTreeView.Nodes.Add(node);

        _cachedNewTypeNames.Clear();

        ReloadRemapTreeView(remaps);

        ResetAllRemapFields();
    }

    private void RemoveRemapButton_Click(object sender, EventArgs e)
    {
        foreach (var node in RemapNodes.ToArray())
        {
            if (node.Key == RemapTreeView.SelectedNode)
            {
                bool projectMode = AppSettings.Remapper.UseProjectMappings;

                var remaps = DataProvider.Remaps;

                remaps.Remove(node.Value);
                RemapNodes.Remove(node.Key);
                RemapTreeView.Nodes.Remove(node.Key);
            }
        }

        ResetAllRemapFields();

        DataProvider.SaveMapping();
    }

    private void RunRemapButton_Click(object sender, EventArgs e)
    {
        if (ReCodeItRemapper.IsRunning) { return; }

        if (string.IsNullOrEmpty(AppSettings.Remapper.AssemblyPath))
        {
            MessageBox.Show("Please select an assembly path", "Assembly not loaded");
            return;
        }

        Remapper.InitializeRemap(
            DataProvider.LoadMappingFile(AppSettings.Remapper.MappingPath),
            AppSettings.Remapper.AssemblyPath,
            AppSettings.Remapper.OutputPath);

        ReloadRemapTreeView(DataProvider.Remaps);
    }

    private void ValidateRemapButton_Click(object sender, EventArgs e)
    {
        List<RemapModel> validation = [];

        var remapToValidate = CreateRemapFromGUI();

        if (remapToValidate is null) return;

        validation.Add(remapToValidate);

        Remapper.InitializeRemap(
            validation,
            AppSettings.Remapper.AssemblyPath,
            AppSettings.Remapper.OutputPath,
            validate: true);
    }

    /// <summary>
    /// Only used by the manual remap process, not apart of the cross compiler process
    /// </summary>
    private void ReloadTreeAfterMapping()
    {
        ReloadRemapTreeView(DataProvider.Remaps);
    }

    private void SaveMappingFileButton_Click(object sender, EventArgs e)
    {
        DataProvider.SaveMapping();
    }

    private void LoadMappingFileButton_Click(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFileDialog("Select a mapping file",
                "JSON Files (*.json)|*.json|JSONC Files (*.jsonc)|*.jsonc|All Files (*.*)|*.*");

        if (result == string.Empty) { return; }

        DataProvider.Remaps = DataProvider.LoadMappingFile(result);
        AppSettings.Remapper.MappingPath = result;
        AppSettings.Remapper.UseProjectMappings = false;

        LoadedMappingFilePath.Text = result;

        RemapTreeView.Nodes.Clear();

        foreach (var remap in DataProvider.Remaps)
        {
            RemapTreeView.Nodes.Add(GUIHelpers.GenerateTreeNode(remap, this));
        }
    }

    private void PickAssemblyPathButton_Click_1(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFileDialog("Select a DLL file",
                "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*");

        if (result != string.Empty)
        {
            AppSettings.Remapper.AssemblyPath = result;
            TargetAssemblyPath.Text = result;
        }
    }

    private void OutputDirectoryButton_Click_1(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFolderDialog("Select an output directory");

        if (result != string.Empty)
        {
            AppSettings.Remapper.OutputPath = result;
            RemapperOutputDirectoryPath.Text = result;
        }
    }
    
    #endregion MAIN_BUTTONS

    #region LISTBOX_BUTTONS

    private void MethodIncludeAddButton_Click(object sender, EventArgs e)
    {
        if (IncludeMethodTextBox.Text == string.Empty) return;

        if (!MethodIncludeBox.Items.Contains(IncludeMethodTextBox.Text))
        {
            MethodIncludeBox.Items.Add(IncludeMethodTextBox.Text);
            IncludeMethodTextBox.Clear();
        }
    }

    private void MethodIncludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (MethodIncludeBox.SelectedItem != null)
        {
            MethodIncludeBox.Items.Remove(MethodIncludeBox.SelectedItem);
        }
    }

    private void MethodExcludeAddButton_Click(object sender, EventArgs e)
    {
        if (ExcludeMethodTextBox.Text == string.Empty) return;

        if (!MethodExcludeBox.Items.Contains(ExcludeMethodTextBox.Text))
        {
            MethodExcludeBox.Items.Add(ExcludeMethodTextBox.Text);
            ExcludeMethodTextBox.Clear();
        }
    }

    private void MethodExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (MethodExcludeBox.SelectedItem != null)
        {
            MethodExcludeBox.Items.Remove(MethodExcludeBox.SelectedItem);
        }
    }

    private void FIeldIncludeAddButton_Click(object sender, EventArgs e)
    {
        if (FieldsIncludeTextInput.Text == string.Empty) return;

        if (!FieldIncludeBox.Items.Contains(FieldsIncludeTextInput.Text))
        {
            FieldIncludeBox.Items.Add(FieldsIncludeTextInput.Text);
            FieldsIncludeTextInput.Clear();
        }
    }

    private void FieldIncludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (FieldIncludeBox.SelectedItem != null)
        {
            FieldIncludeBox.Items.Remove(FieldIncludeBox.SelectedItem);
        }
    }

    private void FieldExcludeAddButton_Click(object sender, EventArgs e)
    {
        if (FieldsExcludeTextInput.Text == string.Empty) return;

        if (!FieldExcludeBox.Items.Contains(FieldsExcludeTextInput.Text))
        {
            FieldExcludeBox.Items.Add(FieldsExcludeTextInput.Text);
            FieldsExcludeTextInput.Clear();
        }
    }

    private void FieldExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (FieldExcludeBox.SelectedItem != null)
        {
            FieldExcludeBox.Items.Remove(FieldExcludeBox.SelectedItem);
        }
    }

    private void PropertiesIncludeAddButton_Click(object sender, EventArgs e)
    {
        if (PropertiesIncludeTextField.Text == string.Empty) return;

        if (!PropertiesIncludeBox.Items.Contains(PropertiesIncludeTextField.Text))
        {
            PropertiesIncludeBox.Items.Add(PropertiesIncludeTextField.Text);
            PropertiesIncludeTextField.Clear();
        }
    }

    private void PropertiesIncludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (PropertiesIncludeBox.SelectedItem != null)
        {
            PropertiesIncludeBox.Items.Remove(PropertiesIncludeBox.SelectedItem);
        }
    }

    private void PropertiesExcludeAddButton_Click(object sender, EventArgs e)
    {
        if (PropertiesExcludeTextField.Text == string.Empty) return;

        if (!PropertiesExcludeBox.Items.Contains(PropertiesExcludeTextField.Text))
        {
            PropertiesExcludeBox.Items.Add(PropertiesExcludeTextField.Text);
            PropertiesExcludeTextField.Clear();
        }
    }

    private void PropertiesExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (PropertiesExcludeBox.SelectedItem != null)
        {
            PropertiesExcludeBox.Items.Remove(PropertiesExcludeBox.SelectedItem);
        }
    }

    private void NestedTypesAddButton_Click(object sender, EventArgs e)
    {
        if (NestedTypesIncludeTextField.Text == string.Empty) return;

        if (!NestedTypesIncludeBox.Items.Contains(NestedTypesIncludeTextField.Text))
        {
            NestedTypesIncludeBox.Items.Add(NestedTypesIncludeTextField.Text);
            NestedTypesIncludeTextField.Clear();
        }
    }

    private void NestedTypesRemoveButton_Click(object sender, EventArgs e)
    {
        if (NestedTypesIncludeBox.SelectedItem != null)
        {
            NestedTypesIncludeBox.Items.Remove(NestedTypesIncludeBox.SelectedItem);
        }
    }

    private void NestedTypesExlcudeAddButton_Click(object sender, EventArgs e)
    {
        if (NestedTypesExcludeTextField.Text == string.Empty) return;

        if (!NestedTypesExcludeBox.Items.Contains(NestedTypesExcludeTextField.Text))
        {
            NestedTypesExcludeBox.Items.Add(NestedTypesExcludeTextField.Text);
            NestedTypesExcludeTextField.Clear();
        }
    }

    private void NestedTypesExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (NestedTypesExcludeBox.SelectedItem != null)
        {
            NestedTypesExcludeBox.Items.Remove(NestedTypesExcludeBox.SelectedItem);
        }
    }


    private void EventsAddButton_Click(object sender, EventArgs e)
    {
        if (EventsIncludeTextField.Text == string.Empty) return;

        if (!EventsIncludeBox.Items.Contains(EventsIncludeTextField.Text))
        {
            EventsIncludeBox.Items.Add(EventsIncludeTextField.Text);
            EventsIncludeTextField.Clear();
        }
    }

    private void EventsRemoveButton_Click(object sender, EventArgs e)
    {
        if (EventsIncludeBox.SelectedItem != null)
        {
            EventsIncludeBox.Items.Remove(EventsIncludeBox.SelectedItem);
        }
    }

    private void EventsExcludeAddButton_Click(object sender, EventArgs e)
    {
        if (EventsExcludeTextField.Text == string.Empty) return;

        if (!EventsExcludeBox.Items.Contains(EventsExcludeTextField.Text))
        {
            EventsExcludeBox.Items.Add(EventsExcludeTextField.Text);
            EventsExcludeTextField.Clear();
        }
    }

    private void EventsExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (EventsExcludeBox.SelectedItem != null)
        {
            EventsExcludeBox.Items.Remove(EventsExcludeBox.SelectedItem);
        }
    }

    private void AutoMapperExcludeAddButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Feature has been removed from this build.", "Feature Removed");
    }

    private void AutoMapperExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Feature has been removed from this build.", "Feature Removed");
    }

    private void RunAutoRemapButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Feature has been removed from this build.", "Feature Removed");
    }

    #endregion LISTBOX_BUTTONS

    #region CHECKBOX

    private void RemapperUnseal_CheckedChanged(object sender, EventArgs e)
    {
        AppSettings.Remapper.MappingSettings.Unseal = RemapperUnseal.Checked;
    }

    private void RemapperPublicicize_CheckedChanged(object sender, EventArgs e)
    {
        AppSettings.Remapper.MappingSettings.Publicize = RemapperPublicicize.Checked;
    }

    private void RenameFieldsCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        AppSettings.Remapper.MappingSettings.RenameFields = RenameFieldsCheckbox.Checked;
    }

    private void RenamePropertiesCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        AppSettings.Remapper.MappingSettings.RenameProperties = RenamePropertiesCheckbox.Checked;
    }

    #endregion CHECKBOX

    #endregion BUTTONS

    #endregion MANUAL_REMAPPER

    #region SETTINGS_TAB

    public void RefreshSettingsPage()
    {
        // Settings page
        DebugLoggingCheckbox.Checked = AppSettings.AppSettings.Debug;
        SilentModeCheckbox.Checked = AppSettings.AppSettings.SilentMode;

        // Remapper page
        TargetAssemblyPath.Text = AppSettings.Remapper.AssemblyPath;
        RemapperOutputDirectoryPath.Text = AppSettings.Remapper.OutputPath;
        RenameFieldsCheckbox.Checked = AppSettings.Remapper.MappingSettings.RenameFields;
        RenamePropertiesCheckbox.Checked = AppSettings.Remapper.MappingSettings.RenameProperties;
        RemapperPublicicize.Checked = AppSettings.Remapper.MappingSettings.Publicize;
        RemapperUnseal.Checked = AppSettings.Remapper.MappingSettings.Unseal;
    }

    #region CHECKBOXES

    private void DebugLoggingCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AppSettings.Debug = DebugLoggingCheckbox.Checked;
    }

    private void SilentModeCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AppSettings.SilentMode = SilentModeCheckbox.Checked;
    }

    #endregion CHECKBOXES
    
    #endregion SETTINGS_TAB

    // Reset All UI elements to default
    private void ResetAllRemapFields()
    {
        PopulateDomainUpDowns();

        // Text fields

        NewTypeName.Clear();
        OriginalTypeName.Clear();
        BaseClassIncludeTextFIeld.Clear();
        BaseClassExcludeTextField.Clear();
        NestedTypeParentName.Clear();
        BaseClassExcludeTextField.Clear();
        IncludeMethodTextBox.Clear();
        ExcludeMethodTextBox.Clear();
        FieldsIncludeTextInput.Clear();
        FieldsExcludeTextInput.Clear();
        PropertiesIncludeTextField.Clear();
        PropertiesExcludeTextField.Clear();
        NestedTypesIncludeTextField.Clear();
        NestedTypesExcludeTextField.Clear();
        EventsIncludeTextField.Clear();
        EventsExcludeTextField.Clear();

        // Numeric UpDowns

        ConstuctorCountUpDown.Value = 0;
        MethodCountUpDown.Value = 0;
        FieldCountUpDown.Value = 0;
        PropertyCountUpDown.Value = 0;
        NestedTypeCountUpDown.Value = 0;

        // Check boxes

        RemapperUseForceRename.Checked = false;
        ConstructorCountEnabled.Checked = false;
        MethodCountEnabled.Checked = false;
        FieldCountEnabled.Checked = false;
        PropertyCountEnabled.Checked = false;
        NestedTypeCountEnabled.Checked = false;

        // List boxes

        MethodIncludeBox.Items.Clear();
        MethodExcludeBox.Items.Clear();
        FieldIncludeBox.Items.Clear();
        FieldExcludeBox.Items.Clear();
        PropertiesIncludeBox.Items.Clear();
        PropertiesExcludeBox.Items.Clear();
        NestedTypesIncludeBox.Items.Clear();
        NestedTypesExcludeBox.Items.Clear();
        EventsIncludeBox.Items.Clear();
        EventsExcludeBox.Items.Clear();
    }

    private void ManualEditSelectedRemap(object? sender, TreeNodeMouseClickEventArgs e)
    {
        EditSelectedRemap(this, e);
    }

    private void EditSelectedRemap(
        object? sender,
        TreeNodeMouseClickEventArgs e,
        bool isComingFromOtherTab = false)
    {
        if (e?.Node.Level != 0 || RemapTreeView?.SelectedNode?.Index < 0 || RemapTreeView?.SelectedNode?.Index == null)
        {
            return;
        }

        RemapModel remap = null;

        foreach (var node in RemapNodes.ToArray())
        {
            if (node.Key == RemapTreeView.SelectedNode)
            {
                bool projectMode = AppSettings.Remapper.UseProjectMappings;

                var remaps = DataProvider.Remaps;

                remap = remaps.FirstOrDefault(x => x.NewTypeName == node.Value.NewTypeName);

                break;
            }
        }

        if (remap == null)
        {
            return;
        }

        _selectedRemapTreeIndex = RemapTreeView.SelectedNode.Index;

        ResetAllRemapFields();

        NewTypeName.Text = remap.NewTypeName;
        OriginalTypeName.Text = remap.OriginalTypeName;
        RemapperUseForceRename.Checked = remap.UseForceRename;

        BaseClassIncludeTextFIeld.Text = remap.SearchParams.MatchBaseClass;
        BaseClassExcludeTextField.Text = remap.SearchParams.IgnoreBaseClass;
        NestedTypeParentName.Text = remap.SearchParams.NTParentName;

        ConstructorCountEnabled.Checked = remap.SearchParams.ConstructorParameterCount is not null
            ? remap.SearchParams.ConstructorParameterCount > 0
            : false;

        MethodCountEnabled.Checked = remap.SearchParams.MethodCount is not null
            ? remap.SearchParams.MethodCount >= 0
            : false;

        FieldCountEnabled.Checked = remap.SearchParams.FieldCount is not null
            ? remap.SearchParams.FieldCount >= 0
            : false;

        PropertyCountEnabled.Checked = remap.SearchParams.PropertyCount is not null
            ? remap.SearchParams.PropertyCount >= 0
            : false;

        NestedTypeCountEnabled.Checked = remap.SearchParams.NestedTypeCount is not null
            ? remap.SearchParams.NestedTypeCount >= 0
            : false;

        ConstuctorCountUpDown.Value = (decimal)((remap.SearchParams.ConstructorParameterCount != null
            ? remap.SearchParams.ConstructorParameterCount
            : 0));

        MethodCountUpDown.Value = (decimal)(remap.SearchParams.MethodCount != null
            ? remap.SearchParams.MethodCount
            : 0);

        FieldCountUpDown.Value = (decimal)(remap.SearchParams.FieldCount != null
            ? remap.SearchParams.FieldCount
            : 0);

        PropertyCountUpDown.Value = (decimal)(remap.SearchParams.PropertyCount != null
            ? remap.SearchParams.PropertyCount
            : 0);

        NestedTypeCountUpDown.Value = (decimal)(remap.SearchParams.NestedTypeCount != null
            ? remap.SearchParams.NestedTypeCount
            : 0);

        IsPublicComboBox.SelectedItem = remap.SearchParams.IsPublic.ToString();

        IsAbstractComboBox.SelectedItem = remap.SearchParams.IsAbstract is not null
            ? remap.SearchParams.IsAbstract.ToString()
            : "Disabled";

        IsSealedComboBox.SelectedItem = remap.SearchParams.IsSealed is not null
            ? remap.SearchParams.IsSealed.ToString()
            : "Disabled";

        IsInterfaceComboBox.SelectedItem = remap.SearchParams.IsInterface is not null
            ? remap.SearchParams.IsInterface.ToString()
            : "Disabled";

        IsStructComboBox.SelectedItem = remap.SearchParams.IsStruct is not null
            ? remap.SearchParams.IsStruct.ToString()
            : "Disabled";

        IsEnumComboBox.SelectedItem = remap.SearchParams.IsEnum is not null
            ? remap.SearchParams.IsEnum.ToString()
            : "Disabled";

        HasAttributeComboBox.SelectedItem = remap.SearchParams.HasAttribute is not null
            ? remap.SearchParams.HasAttribute.ToString()
            : "Disabled";

        HasGenericParamsComboBox.SelectedItem = remap.SearchParams.HasGenericParameters is not null
            ? remap.SearchParams.HasGenericParameters.ToString()
            : "Disabled";

        IsNestedUpDown.BuildStringList("IsNested", false, remap.SearchParams.IsNested);
        IsDerivedUpDown.BuildStringList("IsDerived", false, remap.SearchParams.IsDerived);

        foreach (var method in remap.SearchParams.IncludeMethods)
        {
            MethodIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeMethods)
        {
            MethodExcludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.IncludeFields)
        {
            FieldIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeFields)
        {
            FieldExcludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.IncludeProperties)
        {
            PropertiesIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeProperties)
        {
            PropertiesExcludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.IncludeNestedTypes)
        {
            NestedTypesIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeNestedTypes)
        {
            NestedTypesExcludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.IncludeEvents)
        {
            EventsIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeEvents)
        {
            EventsExcludeBox.Items.Add(method);
        }
    }

    private void PopulateDomainUpDowns()
    {
        // Clear them all first just incase
        IsPublicComboBox.AddItemsToComboBox(["True", "False"]);
        IsPublicComboBox.SelectedItem = "True";

        IsAbstractComboBox.AddItemsToComboBox(["Disabled", "True", "False"]);
        IsAbstractComboBox.SelectedItem = "Disabled";

        IsSealedComboBox.AddItemsToComboBox(["Disabled", "True", "False"]);
        IsSealedComboBox.SelectedItem = "Disabled";

        IsInterfaceComboBox.AddItemsToComboBox(["Disabled", "True", "False"]);
        IsInterfaceComboBox.SelectedItem = "Disabled";

        IsStructComboBox.AddItemsToComboBox(["Disabled", "True", "False"]);
        IsStructComboBox.SelectedItem = "Disabled";

        IsEnumComboBox.AddItemsToComboBox(["Disabled", "True", "False"]);
        IsEnumComboBox.SelectedItem = "Disabled";

        HasAttributeComboBox.AddItemsToComboBox(["Disabled", "True", "False"]);
        HasAttributeComboBox.SelectedItem = "Disabled";

        HasGenericParamsComboBox.AddItemsToComboBox(["Disabled", "True", "False"]);
        HasGenericParamsComboBox.SelectedItem = "Disabled";

        IsNestedUpDown.BuildStringList("IsNested", false);
        IsDerivedUpDown.BuildStringList("IsDerived", false);
    }

    /// <summary>
    /// Subscribes the the remappers OnComplete event
    /// </summary>
    /// <param name="remaps"></param>
    private void ReloadRemapTreeView(List<RemapModel>? remaps)
    {
        RemapTreeView.Nodes.Clear();
        RemapNodes.Clear();

        if (remaps is null)
        {
            return;
        }

        foreach (var remap in remaps)
        {
            RemapTreeView.Nodes.Add(GUIHelpers.GenerateTreeNode(remap, this));
        }
    }

    private void GithubLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = GithubLinkLabel.Text,
            UseShellExecute = true
        });
    }

}