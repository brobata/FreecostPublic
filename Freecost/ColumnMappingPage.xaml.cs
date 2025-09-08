using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Freecost;

[QueryProperty(nameof(VendorColumns), "vendorColumns")]
public partial class ColumnMappingPage : ContentPage
{
    public List<string> AppFields { get; set; } = new List<string> { "SupplierName", "ItemName", "AliasName", "CasePrice", "CaseQuantity", "Unit", "SKU" };
    public List<string> VendorColumns { get; set; } = new List<string>();
    private Dictionary<string, string> _fieldMappings = new Dictionary<string, string>();
    private string? _draggedItem;

    public ColumnMappingPage()
    {
        InitializeComponent();
        AppFieldsCollection.ItemsSource = AppFields;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        VendorColumnsCollection.ItemsSource = VendorColumns;
        PackColumnPicker.ItemsSource = VendorColumns;
        SizeColumnPicker.ItemsSource = VendorColumns;
        UnitColumnPicker.ItemsSource = VendorColumns;
        CombinedQuantityUnitColumnPicker.ItemsSource = VendorColumns;
    }

    private void OnDragStarting(object sender, DragStartingEventArgs e)
    {
        var label = (sender as Element)?.Parent as Label;
        _draggedItem = label?.Text;
    }

    private void OnDrop(object sender, DropEventArgs e)
    {
        var label = (sender as Element)?.Parent as Label;
        var targetField = label?.Text;

        if (targetField != null && _draggedItem != null)
        {
            _fieldMappings[targetField] = _draggedItem;
            MappingsListView.ItemsSource = null;
            MappingsListView.ItemsSource = _fieldMappings;
        }
    }

    private async void OnSaveMapClicked(object sender, EventArgs e)
    {
        var map = new ImportMap
        {
            MapName = MapNameEntry.Text,
            SupplierName = SupplierNameEntry.Text,
            FieldMappings = _fieldMappings,
            VendorColumns = VendorColumns,
            PackColumn = PackColumnPicker.SelectedItem as string,
            SizeColumn = SizeColumnPicker.SelectedItem as string,
            UnitColumn = UnitColumnPicker.SelectedItem as string,
            CombinedQuantityUnitColumn = CombinedQuantityUnitColumnPicker.SelectedItem as string,
            SplitCharacter = SplitCharacterEntry.Text
        };

        if (SessionService.IsOffline)
        {
            var maps = await LocalStorageService.LoadAsync<ImportMap>();
            maps.Add(map);
            await LocalStorageService.SaveAsync(maps);
        }
        else
        {
            await FirestoreService.AddDocumentAsync("importMaps", map, SessionService.AuthToken);
        }

        await Navigation.PopAsync();
    }
}