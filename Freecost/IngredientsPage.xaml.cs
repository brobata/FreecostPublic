using Microsoft.Maui.Controls;
using Google.Cloud.Firestore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using CsvHelper;
using OfficeOpenXml;
using System.Text.RegularExpressions;
using System.ComponentModel;

#if WINDOWS
using Microsoft.UI.Input;
using Windows.System;
#endif

namespace Freecost
{
    public partial class IngredientsPage : ContentPage
    {
        private FirestoreDb? db;
        private string? restaurantId;
        private List<IngredientDisplayRecord> _allIngredients = new List<IngredientDisplayRecord>();
        private List<IngredientDisplayRecord> _ingredients = new List<IngredientDisplayRecord>();
        private List<IngredientDisplayRecord> _selectedIngredients = new List<IngredientDisplayRecord>();
        private IngredientDisplayRecord? _lastSelectedItem;
        private string _currentSortColumn = "ItemName";
        private bool _isSortAscending = true;

        public IngredientsPage()
        {
            InitializeComponent();
            SessionService.StaticPropertyChanged += OnSessionChanged;
            if (!SessionService.IsOffline)
            {
                CreateInitialMaps();
            }
        }

        private void OnRowDoubleTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is not IngredientDisplayRecord tappedIngredient) return;

            // Clear existing selections and select the double-tapped item
            foreach (var item in _selectedIngredients)
            {
                item.IsSelected = false;
            }
            _selectedIngredients.Clear();

            tappedIngredient.IsSelected = true;
            _selectedIngredients.Add(tappedIngredient);

            // Call the existing edit method
            OnEditIngredientClicked(this, EventArgs.Empty);
        }

        private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionService.CurrentRestaurant))
            {
                LoadData();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadData();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            SessionService.StaticPropertyChanged -= OnSessionChanged;
        }

        private void LoadData()
        {
            restaurantId = SessionService.CurrentRestaurant?.Id;
            Task.Run(async () => await LoadIngredients());
        }

        private async Task LoadIngredients()
        {
            restaurantId = SessionService.CurrentRestaurant?.Id;
            if (restaurantId == null) return;

            if (SessionService.IsOffline)
            {
                var ingredientsList = await LocalStorageService.LoadAsync<IngredientCsvRecord>(restaurantId);
                _allIngredients = ingredientsList.Select(i => new IngredientDisplayRecord
                {
                    Id = i.Id,
                    SupplierName = i.SupplierName,
                    ItemName = i.ItemName,
                    AliasName = i.AliasName,
                    CasePrice = i.CasePrice,
                    CaseQuantity = i.CaseQuantity,
                    Unit = i.Unit,
                    SKU = i.SKU
                }).ToList();
            }
            else
            {
                db = FirestoreService.Db;
                if (db == null) return;

                var ingredientsList = new List<IngredientDisplayRecord>();
                var query = db.Collection("restaurants").Document(restaurantId).Collection("ingredients");
                var snapshot = await query.GetSnapshotAsync();
                var documents = snapshot.Documents.ToList();

                for (int i = 0; i < documents.Count; i++)
                {
                    var document = documents[i];
                    var ingredient = document.ConvertTo<IngredientCsvRecord>();
                    var displayRecord = new IngredientDisplayRecord
                    {
                        Id = document.Id,
                        SupplierName = ingredient.SupplierName,
                        ItemName = ingredient.ItemName,
                        AliasName = ingredient.AliasName,
                        CasePrice = ingredient.CasePrice,
                        CaseQuantity = ingredient.CaseQuantity,
                        Unit = ingredient.Unit,
                        SKU = ingredient.SKU
                    };
                    ingredientsList.Add(displayRecord);
                }
                _allIngredients = ingredientsList;
                await LocalStorageService.SaveAsync(_allIngredients.Cast<IngredientCsvRecord>().ToList(), restaurantId);
            }

            _ingredients = new List<IngredientDisplayRecord>(_allIngredients);

            SortIngredients();
            MainThread.BeginInvokeOnMainThread(PopulateGrid);
        }

        private void PopulateGrid()
        {
            IngredientsGrid.RowDefinitions.Clear();
            IngredientsGrid.Children.Clear();

            for (int i = 0; i < _ingredients.Count; i++)
            {
                var ingredient = _ingredients[i];
                ingredient.IsEven = i % 2 == 0;

                IngredientsGrid.RowDefinitions.Add(new RowDefinition { Height = Microsoft.Maui.GridLength.Auto });

                var singleTapGesture = new TapGestureRecognizer();
                singleTapGesture.Tapped += OnRowTapped;
                singleTapGesture.CommandParameter = ingredient;

                var doubleTapGesture = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
                doubleTapGesture.Tapped += OnRowDoubleTapped;
                doubleTapGesture.CommandParameter = ingredient;

                Color backgroundColor;
#if ANDROID || IOS || MACCATALYST || WINDOWS
                if (Microsoft.Maui.Controls.Application.Current != null && Microsoft.Maui.Controls.Application.Current.Resources != null)
                {
                    backgroundColor = ingredient.IsSelected ? (Color)Microsoft.Maui.Controls.Application.Current.Resources["Accent"] : (ingredient.IsEven ? (Color)Microsoft.Maui.Controls.Application.Current.Resources["RowColorEven"] : (Color)Microsoft.Maui.Controls.Application.Current.Resources["RowColorOdd"]);
                }
                else
#endif
                {
                    backgroundColor = ingredient.IsSelected ? Colors.Blue : (ingredient.IsEven ? Colors.White : Colors.LightGray);
                }

                var backgroundGrid = new Grid
                {
                    BackgroundColor = backgroundColor,
                    GestureRecognizers = { singleTapGesture, doubleTapGesture },
                    BindingContext = ingredient
                };

                IngredientsGrid.Add(backgroundGrid, 0, i);
                Grid.SetColumnSpan(backgroundGrid, 7);

                IngredientsGrid.Add(CreateDataLabel(ingredient.ItemName, Microsoft.Maui.TextAlignment.Start), 0, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.AliasName, Microsoft.Maui.TextAlignment.Start), 1, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.SupplierName, Microsoft.Maui.TextAlignment.Start), 2, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.SKU, Microsoft.Maui.TextAlignment.Start), 3, i);
                IngredientsGrid.Add(CreateDataLabel(string.Format("{0:C}", ingredient.CasePrice), Microsoft.Maui.TextAlignment.Start), 4, i);
                IngredientsGrid.Add(CreateDataLabel(string.Format("{0:F2}", ingredient.CaseQuantity), Microsoft.Maui.TextAlignment.Start), 5, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.Unit, Microsoft.Maui.TextAlignment.Start), 6, i);

#if ANDROID || IOS || MACCATALYST || WINDOWS
                if (Microsoft.Maui.Controls.Application.Current != null && Microsoft.Maui.Controls.Application.Current.Resources != null)
                {
                    var bottomBorder = new BoxView { HeightRequest = 1, Color = (Color)Microsoft.Maui.Controls.Application.Current.Resources["BorderColor"], VerticalOptions = LayoutOptions.End };
                    IngredientsGrid.Add(bottomBorder, 0, i);
                    Grid.SetColumnSpan(bottomBorder, 7);
                }
#endif
            }
        }


        private Label CreateDataLabel(string? text, Microsoft.Maui.TextAlignment alignment)
        {
            return new Label
            {
                Text = text ?? string.Empty,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                HorizontalTextAlignment = alignment,
                Padding = new Microsoft.Maui.Thickness(5, 10),
                InputTransparent = true
            };
        }

        private void OnRowTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is not IngredientDisplayRecord tappedIngredient) return;

            var previouslySelected = _selectedIngredients.ToList();

            bool isCtrlPressed = false;
            bool isShiftPressed = false;

#if WINDOWS
            var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            isShiftPressed = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
#endif

            if (isCtrlPressed)
            {
                tappedIngredient.IsSelected = !tappedIngredient.IsSelected;
                if (tappedIngredient.IsSelected)
                {
                    _selectedIngredients.Add(tappedIngredient);
                }
                else
                {
                    _selectedIngredients.Remove(tappedIngredient);
                }
                _lastSelectedItem = tappedIngredient;
            }
            else if (isShiftPressed && _lastSelectedItem != null)
            {
                var lastIndex = _ingredients.IndexOf(_lastSelectedItem);
                var currentIndex = _ingredients.IndexOf(tappedIngredient);

                var startIndex = Math.Min(lastIndex, currentIndex);
                var endIndex = Math.Max(lastIndex, currentIndex);

                foreach (var item in _selectedIngredients)
                {
                    item.IsSelected = false;
                }
                _selectedIngredients.Clear();

                for (int i = startIndex; i <= endIndex; i++)
                {
                    _ingredients[i].IsSelected = true;
                    _selectedIngredients.Add(_ingredients[i]);
                }
            }
            else
            {
                foreach (var item in _selectedIngredients)
                {
                    item.IsSelected = false;
                }
                _selectedIngredients.Clear();

                tappedIngredient.IsSelected = true;
                _selectedIngredients.Add(tappedIngredient);
                _lastSelectedItem = tappedIngredient;
            }

            var changedIngredients = previouslySelected.Union(_selectedIngredients).Distinct();
            foreach (var ingredient in changedIngredients)
            {
                var backgroundGrid = IngredientsGrid.Children
                    .OfType<Grid>()
                    .FirstOrDefault(g => g.BindingContext == ingredient);

                if (backgroundGrid != null)
                {
                    Color newBackgroundColor;
#if ANDROID || IOS || MACCATALYST || WINDOWS
                    if (Microsoft.Maui.Controls.Application.Current?.Resources != null)
                    {
                        newBackgroundColor = ingredient.IsSelected
                            ? (Color)Microsoft.Maui.Controls.Application.Current.Resources["Accent"]
                            : (ingredient.IsEven ? (Color)Microsoft.Maui.Controls.Application.Current.Resources["RowColorEven"] : (Color)Microsoft.Maui.Controls.Application.Current.Resources["RowColorOdd"]);
                    }
                    else
#endif
                    {
                        newBackgroundColor = ingredient.IsSelected ? Colors.Blue : (ingredient.IsEven ? Colors.White : Colors.LightGray);
                    }
                    backgroundGrid.BackgroundColor = newBackgroundColor;
                }
            }
        }


        private async void OnAddIngredientClicked(object? sender, EventArgs e)
        {
            await Navigation.PushAsync(new AddIngredientPage());
        }

        private async void OnEditIngredientClicked(object? sender, EventArgs e)
        {
            if (_selectedIngredients.Count != 1)
            {
                await DisplayAlert("Selection Error", "Please select exactly one ingredient to edit.", "OK");
                return;
            }
            await Navigation.PushAsync(new AddIngredientPage(_selectedIngredients.First()));
        }

        private async void OnDeleteIngredientClicked(object? sender, EventArgs e)
        {
            if (_selectedIngredients.Count == 0)
            {
                await DisplayAlert("No Ingredients Selected", "Please select one or more ingredients to delete.", "OK");
                return;
            }

            bool answer = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete {_selectedIngredients.Count} ingredient(s)?", "Yes", "No");
            if (answer)
            {
                if (SessionService.IsOffline)
                {
                    _ingredients.RemoveAll(ing => _selectedIngredients.Contains(ing));
                    await LocalStorageService.SaveAsync(_ingredients.Cast<IngredientCsvRecord>().ToList(), restaurantId);
                }
                else
                {
                    if (db == null || restaurantId == null) return;
                    var batch = db.StartBatch();
                    foreach (var ingredient in _selectedIngredients)
                    {
                        if (ingredient.Id != null)
                        {
                            var docRef = db.Collection("restaurants").Document(restaurantId).Collection("ingredients").Document(ingredient.Id);
                            batch.Delete(docRef);
                        }
                    }
                    await batch.CommitAsync();
                }

                _selectedIngredients.Clear();
                LoadData();
            }
        }


        private void SortIngredients()
        {
            if (_ingredients == null) return;

            var sorted = _isSortAscending
                ? _ingredients.OrderBy(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList()
                : _ingredients.OrderByDescending(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList();

            _ingredients = sorted;
        }

        private void OnSortClicked(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is string newSortColumn)
            {
                if (_currentSortColumn == newSortColumn)
                {
                    _isSortAscending = !_isSortAscending;
                }
                else
                {
                    _currentSortColumn = newSortColumn;
                    _isSortAscending = true;
                }

                SortIngredients();
                PopulateGrid();
            }
        }

        private void OnSearchBarTextChanged(object? sender, TextChangedEventArgs e)
        {
            var searchTerm = e.NewTextValue;

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _ingredients = new List<IngredientDisplayRecord>(_allIngredients);
            }
            else
            {
                _ingredients = _allIngredients
                    .Where(i => (i.ItemName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (i.AliasName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (i.SupplierName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            PopulateGrid();
        }

        #region Bulk Import Logic
        private async void CreateInitialMaps()
        {
            db = FirestoreService.Db;
            if (db == null) return;

            var mapsCollection = db.Collection("importMaps");

            var syscoQuery = await mapsCollection.WhereEqualTo("MapName", "Sysco").GetSnapshotAsync();
            if (syscoQuery.Documents.Count == 0)
            {
                var syscoMap = new ImportMap { MapName = "Sysco", SupplierName = "Sysco", FieldMappings = new Dictionary<string, string> { { "ItemName", "Desc" }, { "CasePrice", "Case $" }, { "SKU", "SUPC" } }, PackColumn = "Pack", SizeColumn = "Size", HeaderRow = 2, Delimiter = "\t" };
                await mapsCollection.AddAsync(syscoMap);
            }

            var benEKeithQuery = await mapsCollection.WhereEqualTo("MapName", "Ben E. Keith").GetSnapshotAsync();
            if (benEKeithQuery.Documents.Count == 0)
            {
                var benEKeithMap = new ImportMap { MapName = "Ben E. Keith", SupplierName = "Ben E. Keith", FieldMappings = new Dictionary<string, string> { { "ItemName", "Item Name" }, { "CasePrice", "Price" }, { "SKU", "Item #" } }, CombinedQuantityUnitColumn = "Pack / Size", SplitCharacter = "/", HeaderRow = 1, Delimiter = "," };
                await mapsCollection.AddAsync(benEKeithMap);
            }

            var usFoodsQuery = await mapsCollection.WhereEqualTo("MapName", "US Foods").GetSnapshotAsync();
            if (usFoodsQuery.Documents.Count == 0)
            {
                var usFoodsMap = new ImportMap { MapName = "US Foods", SupplierName = "US Foods", FieldMappings = new Dictionary<string, string> { { "ItemName", "Product Description" }, { "CasePrice", "Product Price" }, { "SKU", "Product Number" } }, CombinedQuantityUnitColumn = "Product Package Size", SplitCharacter = " ", HeaderRow = 1, Delimiter = "," };
                await mapsCollection.AddAsync(usFoodsMap);
            }
        }

        private async void OnBulkImportClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Please select a csv or excel file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { { DevicePlatform.WinUI, new[] { ".csv", ".xlsx" } }, { DevicePlatform.macOS, new[] { "csv", "xlsx" } }, })
                });

                if (result == null) return;
                var records = new List<IngredientCsvRecord>();

                if (SessionService.IsOffline)
                {
                    var maps = await LocalStorageService.LoadAsync<ImportMap>();
                    if (!maps.Any())
                    {
                        maps.Add(new ImportMap { MapName = "Sysco", SupplierName = "Sysco", FieldMappings = new Dictionary<string, string> { { "ItemName", "Desc" }, { "CasePrice", "Case $" }, { "SKU", "SUPC" } }, PackColumn = "Pack", SizeColumn = "Size", HeaderRow = 2, Delimiter = "\t" });
                        maps.Add(new ImportMap { MapName = "Ben E. Keith", SupplierName = "Ben E. Keith", FieldMappings = new Dictionary<string, string> { { "ItemName", "Item Name" }, { "CasePrice", "Price" }, { "SKU", "Item #" } }, CombinedQuantityUnitColumn = "Pack / Size", SplitCharacter = "/", HeaderRow = 1, Delimiter = "," });
                        maps.Add(new ImportMap { MapName = "US Foods", SupplierName = "US Foods", FieldMappings = new Dictionary<string, string> { { "ItemName", "Product Description" }, { "CasePrice", "Product Price" }, { "SKU", "Product Number" } }, CombinedQuantityUnitColumn = "Product Package Size", SplitCharacter = " ", HeaderRow = 1, Delimiter = "," });
                        await LocalStorageService.SaveAsync(maps);
                    }
                    using (var stream = await result.OpenReadAsync())
                    {
                        if (result.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            var (selectedMap, headerRow, headers) = FindBestMap(stream, maps);
                            if (selectedMap == null) { await DisplayAlert("No Matching Map", "Could not determine an import map for this file.", "OK"); return; }
                            selectedMap.HeaderRow = headerRow;
                            records = ProcessCsvStream(stream, selectedMap, headers);
                        }
                        else if (result.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var package = new ExcelPackage(stream))
                            {
                                var (selectedMap, headerRow, headers) = FindBestMapForExcel(package, maps);
                                if (selectedMap == null) { await DisplayAlert("No Matching Map", "Could not determine an import map for this Excel file.", "OK"); return; }
                                selectedMap.HeaderRow = headerRow;
                                records = ProcessExcelPackage(package, headers, selectedMap);
                            }
                        }
                    }

                    if (records.Any())
                    {
                        var existingIngredients = await LocalStorageService.LoadAsync<IngredientCsvRecord>(restaurantId);
                        var existingIngredientsBySku = existingIngredients.Where(i => !string.IsNullOrEmpty(i.SKU)).ToDictionary(i => i.SKU!, i => i);
                        int createdCount = 0;
                        int updatedCount = 0;

                        foreach (var record in records)
                        {
                            if (!string.IsNullOrEmpty(record.SKU) && existingIngredientsBySku.TryGetValue(record.SKU, out var existingRecord))
                            {
                                existingRecord.CasePrice = record.CasePrice;
                                updatedCount++;
                            }
                            else
                            {
                                record.Id = Guid.NewGuid().ToString();
                                existingIngredients.Add(record);
                                createdCount++;
                            }
                        }
                        await LocalStorageService.SaveAsync(existingIngredients, restaurantId);
                        await DisplayAlert("Import Complete", $"{createdCount} ingredients created.\n{updatedCount} ingredients updated.", "OK");
                        LoadData();
                    }
                }
                else
                {
                    if (db == null || restaurantId == null) return;
                    using (var stream = await result.OpenReadAsync())
                    {
                        var mapsSnapshot = await db.Collection("importMaps").GetSnapshotAsync();
                        var maps = mapsSnapshot.Documents.Select(doc => { var map = doc.ConvertTo<ImportMap>(); map.Id = doc.Id; return map; }).ToList();

                        if (result.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            var (selectedMap, headerRow, headers) = FindBestMap(stream, maps);
                            if (selectedMap == null) { await DisplayAlert("No Matching Map", "Could not determine an import map for this file.", "OK"); return; }
                            selectedMap.HeaderRow = headerRow;
                            records = ProcessCsvStream(stream, selectedMap, headers);
                        }
                        else if (result.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var package = new ExcelPackage(stream))
                            {
                                var (selectedMap, headerRow, headers) = FindBestMapForExcel(package, maps);
                                if (selectedMap == null) { await DisplayAlert("No Matching Map", "Could not determine an import map for this Excel file.", "OK"); return; }
                                selectedMap.HeaderRow = headerRow;
                                records = ProcessExcelPackage(package, headers, selectedMap);
                            }
                        }
                    }

                    if (records.Any())
                    {
                        var collection = db.Collection("restaurants").Document(restaurantId).Collection("ingredients");
                        var existingIngredientsSnapshot = await collection.GetSnapshotAsync();
                        var existingIngredientsBySku = existingIngredientsSnapshot.Documents.Where(doc => doc.ContainsField("SKU") && !string.IsNullOrEmpty(doc.GetValue<string>("SKU"))).ToDictionary(doc => doc.GetValue<string>("SKU")!, doc => doc.Reference);
                        int createdCount = 0;
                        int updatedCount = 0;
                        const int batchSize = 250;
                        for (int i = 0; i < records.Count; i += batchSize)
                        {
                            var batch = db.StartBatch();
                            var chunk = records.Skip(i).Take(batchSize);
                            foreach (var record in chunk)
                            {
                                if (!string.IsNullOrEmpty(record.SKU) && existingIngredientsBySku.TryGetValue(record.SKU, out var existingDocRef))
                                {
                                    batch.Update(existingDocRef, "CasePrice", record.CasePrice);
                                    updatedCount++;
                                }
                                else
                                {
                                    var newDocRef = collection.Document();
                                    batch.Set(newDocRef, record);
                                    createdCount++;
                                }
                            }
                            await batch.CommitAsync();
                        }
                        await DisplayAlert("Import Complete", $"{createdCount} ingredients created.\n{updatedCount} ingredients updated.", "OK");
                        LoadData();
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Import Error", $"An unexpected error occurred: {ex.Message}", "OK");
            }
        }

        private List<IngredientCsvRecord> ProcessCsvStream(Stream stream, ImportMap selectedMap, List<string> headers)
        {
            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, Delimiter = selectedMap.Delimiter ?? "," };
            using (var reader = new StreamReader(stream))
            using (var csv = new CsvReader(reader, config))
            {
                for (int i = 1; i < selectedMap.HeaderRow; i++) { csv.Read(); }
                var dataRows = new List<IDictionary<string, object>>();
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var dict = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;
                    foreach (var header in headers) { dict[header] = csv.GetField(header) ?? string.Empty; }
                    dataRows.Add(dict);
                }
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("ConvertedCsv");
                    for (int i = 0; i < headers.Count; i++) { worksheet.Cells[selectedMap.HeaderRow, i + 1].Value = headers[i]; }
                    for (int i = 0; i < dataRows.Count; i++)
                    {
                        var recordDict = dataRows[i];
                        for (int j = 0; j < headers.Count; j++) { worksheet.Cells[selectedMap.HeaderRow + i + 1, j + 1].Value = recordDict[headers[j]]; }
                    }
                    return ProcessExcelPackage(package, headers, selectedMap);
                }
            }
        }

        private (ImportMap? map, int headerRow, List<string> headers) FindBestMap(Stream stream, List<ImportMap> maps)
        {
            ImportMap? bestMap = null;
            int bestHeaderRow = -1;
            int bestMatchCount = 0;
            List<string> bestHeaders = new List<string>();
            const int rowsToScan = 5;
            var lines = new List<string>();
            using (var reader = new StreamReader(stream, leaveOpen: true))
            {
                for (int i = 0; i < rowsToScan && !reader.EndOfStream; i++) { lines.Add(reader.ReadLine() ?? string.Empty); }
            }
            for (int i = 0; i < lines.Count; i++)
            {
                foreach (var map in maps)
                {
                    var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false, Delimiter = map.Delimiter ?? ",", TrimOptions = CsvHelper.Configuration.TrimOptions.Trim, };
                    using (var reader = new StringReader(lines[i]))
                    using (var csv = new CsvReader(reader, config))
                    {
                        if (csv.Read())
                        {
                            try
                            {
                                var potentialHeaders = new List<string>();
                                for (int j = 0; j < csv.Parser.Count; j++) { potentialHeaders.Add(csv.GetField(j) ?? string.Empty); }
                                var lowerCaseHeaders = potentialHeaders.Select(h => h.ToLowerInvariant()).ToList();
                                if (map.FieldMappings != null && map.FieldMappings.Any())
                                {
                                    int matchCount = map.FieldMappings.Values.Count(v => !string.IsNullOrEmpty(v) && lowerCaseHeaders.Contains(v.Trim().ToLowerInvariant()));
                                    if (matchCount > bestMatchCount) { bestMatchCount = matchCount; bestMap = map; bestHeaderRow = i + 1; bestHeaders = potentialHeaders; }
                                }
                            }
                            catch (CsvHelper.BadDataException) { }
                        }
                    }
                }
            }
            stream.Position = 0;
            return bestMatchCount < 2 ? (null, -1, new List<string>()) : (bestMap, bestHeaderRow, bestHeaders);
        }

        private (ImportMap? map, int headerRow, List<string> headers) FindBestMapForExcel(ExcelPackage package, List<ImportMap> maps)
        {
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) return (null, -1, new List<string>());

            ImportMap? excelBestMap = null;
            int excelBestHeaderRow = -1;
            int excelBestMatchCount = 0;
            List<string> excelBestHeaders = new List<string>();
            const int rowsToScan = 5;
            for (int i = 1; i <= rowsToScan && i <= worksheet.Dimension.End.Row; i++)
            {
                var potentialHeaders = new List<string>();
                for (int j = 1; j <= worksheet.Dimension.End.Column; j++) { potentialHeaders.Add(worksheet.Cells[i, j].Text?.Trim() ?? string.Empty); }
                var lowerCaseHeaders = potentialHeaders.Select(h => h.ToLowerInvariant()).ToList();
                foreach (var map in maps)
                {
                    if (map.FieldMappings != null && map.FieldMappings.Any())
                    {
                        int matchCount = map.FieldMappings.Values.Count(v => !string.IsNullOrEmpty(v) && lowerCaseHeaders.Contains(v.Trim().ToLowerInvariant()));
                        if (matchCount > excelBestMatchCount) { excelBestMatchCount = matchCount; excelBestMap = map; excelBestHeaderRow = i; excelBestHeaders = potentialHeaders; }
                    }
                }
            }
            return (excelBestMap, excelBestHeaderRow, excelBestHeaders);
        }

        private List<IngredientCsvRecord> ProcessExcelPackage(ExcelPackage package, List<string> headers, ImportMap selectedMap)
        {
            var records = new List<IngredientCsvRecord>();
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) return records;

            var startRow = selectedMap.HeaderRow > 0 ? selectedMap.HeaderRow + 1 : 2;

            for (int row = startRow; row <= worksheet.Dimension.End.Row; row++)
            {
                var record = new IngredientCsvRecord { SupplierName = selectedMap.SupplierName };
                if (selectedMap.FieldMappings != null)
                {
                    if (selectedMap.FieldMappings.TryGetValue("ItemName", out var itemNameHeader) && itemNameHeader != null)
                        record.ItemName = worksheet.Cells[row, headers.IndexOf(GetActualHeader(itemNameHeader, headers)) + 1].Text;
                    if (selectedMap.FieldMappings.TryGetValue("AliasName", out var aliasNameHeader) && !string.IsNullOrEmpty(aliasNameHeader) && headers.IndexOf(GetActualHeader(aliasNameHeader, headers)) != -1)
                        record.AliasName = worksheet.Cells[row, headers.IndexOf(GetActualHeader(aliasNameHeader, headers)) + 1].Text;
                    else
                        record.AliasName = string.Empty;
                    if (selectedMap.FieldMappings.TryGetValue("CasePrice", out var casePriceHeader) && casePriceHeader != null)
                    {
                        if (double.TryParse(worksheet.Cells[row, headers.IndexOf(GetActualHeader(casePriceHeader, headers)) + 1].Text, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"), out double price))
                            record.CasePrice = price;
                    }
                    if (selectedMap.FieldMappings.TryGetValue("SKU", out var skuHeader) && skuHeader != null)
                        record.SKU = worksheet.Cells[row, headers.IndexOf(GetActualHeader(skuHeader, headers)) + 1].Text;
                }
                if (!string.IsNullOrEmpty(selectedMap.CombinedQuantityUnitColumn) && !string.IsNullOrEmpty(selectedMap.SplitCharacter))
                {
                    var combined = worksheet.Cells[row, headers.IndexOf(GetActualHeader(selectedMap.CombinedQuantityUnitColumn, headers)) + 1].Text;
                    if (combined != null)
                    {
                        var parts = combined.Split(new[] { selectedMap.SplitCharacter }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            var quantityMatch = Regex.Match(parts[0], @"[\d\.]+");
                            if (quantityMatch.Success && double.TryParse(quantityMatch.Value, out double quantity))
                                record.CaseQuantity = quantity;
                            record.Unit = Regex.Replace(parts[1], @"[\d\.]+", "").Trim() ?? string.Empty;
                        }
                        else
                        {
                            var quantityMatch = Regex.Match(combined, @"[\d\.]+");
                            if (quantityMatch.Success && double.TryParse(quantityMatch.Value, out double quantity))
                                record.CaseQuantity = quantity;
                            record.Unit = Regex.Replace(combined, @"[\d\.]+", "").Trim() ?? string.Empty;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(selectedMap.PackColumn) && !string.IsNullOrEmpty(selectedMap.SizeColumn))
                {
                    if (double.TryParse(worksheet.Cells[row, headers.IndexOf(GetActualHeader(selectedMap.PackColumn, headers)) + 1].Text, out double pack))
                    {
                        var size = worksheet.Cells[row, headers.IndexOf(GetActualHeader(selectedMap.SizeColumn, headers)) + 1].Text;
                        if (size != null)
                        {
                            var sizeMatch = Regex.Match(size, @"[\d\.]+");
                            if (sizeMatch.Success && double.TryParse(sizeMatch.Value, out double sizeQuantity))
                            {
                                record.CaseQuantity = pack * sizeQuantity;
                                record.Unit = Regex.Replace(size, @"[\d\.]+", "").Trim() ?? string.Empty;
                            }
                        }
                    }
                }
                records.Add(record);
            }
            return records;
        }

        private string GetActualHeader(string headerName, List<string> headers)
        {
            return headers.FirstOrDefault(h => h.Trim().Equals(headerName, StringComparison.OrdinalIgnoreCase)) ?? headerName;
        }
        #endregion
    }
}