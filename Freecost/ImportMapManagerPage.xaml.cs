using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using CsvHelper;
using Microsoft.Maui.Storage;
using OfficeOpenXml;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using Plugin.Firebase.Firestore;
using Plugin.Firebase.Core;

namespace Freecost;

public partial class ImportMapManagerPage : ContentPage
{
    private List<ImportMap> maps = new List<ImportMap>();

    public ImportMapManagerPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMaps();
    }

    private async Task LoadMaps()
    {
        if (SessionService.IsOffline)
        {
            maps = await LocalStorageService.LoadAsync<ImportMap>();
        }
        else
        {
            maps = await FirestoreService.GetImportMapsAsync();
        }
        MapsListView.ItemsSource = maps;
    }

    private async void OnSelectFileClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Please select a csv or excel file",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".csv", ".xlsx" } },
                        { DevicePlatform.macOS, new[] { "csv", "xlsx" } },
                        { DevicePlatform.Android, new[] { "text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } },
                        { DevicePlatform.iOS, new[] { "public.comma-separated-values-text", "org.openxmlformats.spreadsheetml.sheet" } },
                        { DevicePlatform.Tizen, new[] { "*/*" } },
                    })
            });

            if (result != null)
            {
                List<string> headers = new List<string>();
                using (var stream = await result.OpenReadAsync())
                {
                    if (result.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var reader = new StreamReader(stream))
                        using (var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
                        {
                            await csv.ReadAsync();
                            csv.ReadHeader();
                            if (csv.HeaderRecord != null)
                            {
                                headers.AddRange(csv.HeaderRecord);
                            }
                        }
                    }
                    else if (result.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var package = new ExcelPackage(stream))
                        {
                            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                            if (worksheet != null)
                            {
                                for (int i = 1; i <= worksheet.Dimension.End.Column; i++)
                                {
                                    headers.Add(worksheet.Cells[1, i].Text);
                                }
                            }
                        }
                    }
                }

                await Navigation.PushAsync(new ColumnMappingPage
                {
                    VendorColumns = headers
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteMapClicked(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            var map = button.CommandParameter as ImportMap;
            if (map != null)
            {
                bool answer = await DisplayAlert("Confirm Deletion", $"Are you sure you want to delete the map '{map.MapName}'?", "Yes", "No");
                if (answer)
                {
                    if (SessionService.IsOffline)
                    {
                        maps.Remove(map);
                        await LocalStorageService.SaveAsync(maps);
                    }
                    else
                    {
                        if (map.Id != null)
                        {
                            await CrossFirebase.Current.Firestore.Collection("importMaps").Document(map.Id).DeleteAsync();
                        }
                    }
                    await LoadMaps();
                }
            }
        }
    }

    private async void OnAddMapClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AddEditMapPage());
    }

    private async void OnEditMapClicked(object sender, EventArgs e)
    {
        if (sender is Button button)
        {
            var map = button.CommandParameter as ImportMap;
            if (map != null)
            {
                await Navigation.PushAsync(new AddEditMapPage { MapId = map.Id });
            }
        }
    }

    private async void OnExportMapsClicked(object sender, EventArgs e)
    {
        if (maps == null || !maps.Any())
        {
            await DisplayAlert("No Maps", "There are no import maps to export.", "OK");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(maps, new JsonSerializerOptions { WriteIndented = true });
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

            var fileSaverResult = await FileSaver.Default.SaveAsync("import_maps.json", stream, default);
            if (!fileSaverResult.IsSuccessful)
            {
                await DisplayAlert("Export Failed", $"There was an error saving the file: {fileSaverResult.Exception?.Message}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Error", $"An unexpected error occurred: {ex.Message}", "OK");
        }
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}

