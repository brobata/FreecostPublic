using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Plugin.Firebase.Firestore;
using Plugin.Firebase.Core;

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
            _map = new ImportMap
            {
                FieldMappings = new Dictionary<string, string>
                {
                    { "ItemName", "" }, { "AliasName", "" }, { "CasePrice", "" }, { "SKU", "" }
                }
            };
            BuildMappingsUI();
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
            if (string.IsNullOrEmpty(MapId)) return;

            try
            {
                var doc = await CrossFirebase.Current.Firestore.Collection("importMaps").Document(MapId).GetAsync();
                if (doc.Exists)
                {
                    _map = doc.ToObject<ImportMap>();
                    if (_map != null)
                    {
                        _map.Id = doc.Id;
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
            if (int.TryParse(HeaderRowEntry.Text, out int headerRow))
            {
                _map.HeaderRow = headerRow;
            }

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
                var collection = CrossFirebase.Current.Firestore.Collection("importMaps");
                if (string.IsNullOrEmpty(_map.Id))
                {
                    await collection.AddAsync(_map);
                }
                else
                {
                    await collection.Document(_map.Id).SetAsync(_map);
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

