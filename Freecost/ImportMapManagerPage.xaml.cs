using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using CsvHelper;
using Microsoft.Maui.Storage;
using OfficeOpenXml;
using Google.Cloud.Firestore;

namespace Freecost;

public partial class ImportMapManagerPage : ContentPage
{
    private FirestoreDb? db;
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
        db = FirestoreService.Db;
        if (db == null) return;
        var snapshot = await db.Collection("importMaps").GetSnapshotAsync();
        maps = snapshot.Documents.Select(doc => doc.ConvertTo<ImportMap>()).ToList();
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
        db = FirestoreService.Db;
        if (sender is Button button)
        {
            var map = button.CommandParameter as ImportMap;
            if (map != null)
            {
                bool answer = await DisplayAlert("Confirm Deletion", $"Are you sure you want to delete the map '{map.MapName}'?", "Yes", "No");
                if (answer)
                {
                    if (db != null && map.Id != null)
                    {
                        await db.Collection("importMaps").Document(map.Id).DeleteAsync();
                        await LoadMaps();
                    }
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
}
