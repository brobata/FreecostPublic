using System;
using System.Collections.Generic;
using System.Linq;

namespace Freecost
{
    [QueryProperty(nameof(MapId), "mapId")]
    public partial class AddEditMapPage : ContentPage
    {
        private ImportMap? _map;
        private string? _mapId;

        public string? MapId
        {
            get => _mapId;
            set
            {
                _mapId = value;
                if (!string.IsNullOrEmpty(_mapId))
                {
                    LoadMap();
                }
            }
        }

        public AddEditMapPage()
        {
            InitializeComponent();
        }

        private void BuildMappingsUI()
        {
            MappingsLayout.Clear();
            var standardMappings = new List<string> { "ItemName", "AliasName", "CasePrice", "SKU" };
            foreach (var key in standardMappings)
            {
                MappingsLayout.Children.Add(new Label { Text = key, FontAttributes = FontAttributes.Bold });
                var entry = new Entry { Placeholder = $"Enter column name for {key}" };
                if (_map?.FieldMappings != null && _map.FieldMappings.TryGetValue(key, out var value))
                {
                    entry.Text = value;
                }
                MappingsLayout.Children.Add(entry);
            }
        }
        private async void LoadMap()
        {
            if (string.IsNullOrEmpty(MapId))
            {
                _map = new ImportMap { FieldMappings = new Dictionary<string, string>() };
                BuildMappingsUI();
                return;
            }

            try
            {
                if (SessionService.IsOffline)
                {
                    var maps = await LocalStorageService.LoadAsync<ImportMap>();
                    _map = maps.FirstOrDefault(m => m.Id == MapId);
                }
                else
                {
                    _map = await FirestoreService.GetDocumentAsync<ImportMap>($"importMaps/{MapId}", SessionService.AuthToken);
                }

                if (_map != null)
                {
                    _map.Id = MapId;
                    MapNameEntry.Text = _map.MapName;
                    SupplierNameEntry.Text = _map.SupplierName;
                    HeaderRowEntry.Text = _map.HeaderRow.ToString();
                    BuildMappingsUI();
                    PackColumnEntry.Text = _map.PackColumn;
                    SizeColumnEntry.Text = _map.SizeColumn;
                    UnitColumnEntry.Text = _map.UnitColumn;
                    CombinedQuantityUnitColumnEntry.Text = _map.CombinedQuantityUnitColumn;
                    SplitCharacterEntry.Text = _map.SplitCharacter;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load map: {ex.Message}", "OK");
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            _map ??= new ImportMap();

            _map.MapName = MapNameEntry.Text;
            _map.SupplierName = SupplierNameEntry.Text;
            if (int.TryParse(HeaderRowEntry.Text, out int headerRow)) _map.HeaderRow = headerRow;

            _map.FieldMappings = new Dictionary<string, string>();
            for (int i = 0; i < MappingsLayout.Children.Count; i += 2)
            {
                if (MappingsLayout.Children[i] is Label label && MappingsLayout.Children[i + 1] is Entry entry)
                {
                    _map.FieldMappings[label.Text] = entry.Text ?? string.Empty;
                }
            }

            _map.PackColumn = PackColumnEntry.Text;
            _map.SizeColumn = SizeColumnEntry.Text;
            _map.UnitColumn = UnitColumnEntry.Text;
            _map.CombinedQuantityUnitColumn = CombinedQuantityUnitColumnEntry.Text;
            _map.SplitCharacter = SplitCharacterEntry.Text;

            try
            {
                if (SessionService.IsOffline)
                {
                    var maps = await LocalStorageService.LoadAsync<ImportMap>();
                    if (string.IsNullOrEmpty(_map.Id)) _map.Id = Guid.NewGuid().ToString();
                    maps.RemoveAll(m => m.Id == _map.Id);
                    maps.Add(_map);
                    await LocalStorageService.SaveAsync(maps);
                }
                else
                {
                    if (string.IsNullOrEmpty(_map.Id))
                    {
                        await FirestoreService.AddDocumentAsync("importMaps", _map, SessionService.AuthToken);
                    }
                    else
                    {
                        await FirestoreService.SetDocumentAsync($"importMaps/{_map.Id}", _map, SessionService.AuthToken);
                    }
                }
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save map: {ex.Message}", "OK");
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}