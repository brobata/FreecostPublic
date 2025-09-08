using Microsoft.Maui.Controls;
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
        private string? restaurantId;
        private List<IngredientDisplayRecord> _allIngredients = new();
        private List<IngredientDisplayRecord> _ingredients = new();
        private List<IngredientDisplayRecord> _selectedIngredients = new();
        private string _currentSortColumn = "ItemName";
        private bool _isSortAscending = true;

#if WINDOWS
        private IngredientDisplayRecord? _lastSelectedItem;
#endif

        public IngredientsPage()
        {
            InitializeComponent();
            SessionService.StaticPropertyChanged += OnSessionChanged;
        }

        private void OnRowDoubleTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is not IngredientDisplayRecord tappedIngredient) return;
            _selectedIngredients.ForEach(i => i.IsSelected = false);
            _selectedIngredients.Clear();
            tappedIngredient.IsSelected = true;
            _selectedIngredients.Add(tappedIngredient);
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
            Task.Run(LoadIngredients);
        }

        private async Task LoadIngredients()
        {
            restaurantId = SessionService.CurrentRestaurant?.Id;
            if (restaurantId == null) return;

            var ingredientsList = SessionService.IsOffline
                ? await LocalStorageService.LoadAsync<IngredientCsvRecord>(restaurantId)
                : await FirestoreService.GetCollectionAsync<IngredientCsvRecord>($"restaurants/{restaurantId}/ingredients", SessionService.AuthToken);

            if (!SessionService.IsOffline)
            {
                await LocalStorageService.SaveAsync(ingredientsList, restaurantId);
            }

            _allIngredients = ingredientsList.Select(MapToDisplayRecord).ToList();
            SortAndFilter();
        }

        private void PopulateGrid()
        {
            IngredientsGrid.RowDefinitions.Clear();
            IngredientsGrid.Children.Clear();
            for (int i = 0; i < _ingredients.Count; i++)
            {
                var ingredient = _ingredients[i];
                ingredient.IsEven = i % 2 == 0;
                IngredientsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var singleTap = new TapGestureRecognizer { CommandParameter = ingredient };
                singleTap.Tapped += OnRowTapped;
                var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2, CommandParameter = ingredient };
                doubleTap.Tapped += OnRowDoubleTapped;
                var backgroundGrid = new Grid { BindingContext = ingredient, GestureRecognizers = { singleTap, doubleTap } };
                var binding = new Binding("IsSelected") { Converter = new SelectedToColorConverter(), ConverterParameter = backgroundGrid };
                backgroundGrid.SetBinding(BackgroundColorProperty, binding);
                IngredientsGrid.Add(backgroundGrid, 0, i);
                Grid.SetColumnSpan(backgroundGrid, 7);
                IngredientsGrid.Add(CreateDataLabel(ingredient.ItemName, TextAlignment.Start), 0, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.AliasName, TextAlignment.Start), 1, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.SupplierName, TextAlignment.Start), 2, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.SKU, TextAlignment.Start), 3, i);
                IngredientsGrid.Add(CreateDataLabel($"{ingredient.CasePrice:C}", TextAlignment.Start), 4, i);
                IngredientsGrid.Add(CreateDataLabel($"{ingredient.CaseQuantity:F2}", TextAlignment.Start), 5, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.Unit, TextAlignment.Start), 6, i);
                if (Application.Current?.Resources.TryGetValue("BorderColor", out var borderColor) == true)
                {
                    var bottomBorder = new BoxView { HeightRequest = 1, Color = (Color)borderColor, VerticalOptions = LayoutOptions.End };
                    IngredientsGrid.Add(bottomBorder, 0, i);
                    Grid.SetColumnSpan(bottomBorder, 7);
                }
            }
        }

        private Label CreateDataLabel(string? text, TextAlignment alignment) => new()
        {
            Text = text ?? string.Empty,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill,
            HorizontalTextAlignment = alignment,
            Padding = new Thickness(5, 10),
            InputTransparent = true
        };

        private void OnRowTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is not IngredientDisplayRecord tappedIngredient) return;
#if WINDOWS
            var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            var isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            var isShiftPressed = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            if (isCtrlPressed)
            {
                tappedIngredient.IsSelected = !tappedIngredient.IsSelected;
                if (tappedIngredient.IsSelected) _selectedIngredients.Add(tappedIngredient); else _selectedIngredients.Remove(tappedIngredient);
                _lastSelectedItem = tappedIngredient;
            }
            else if (isShiftPressed && _lastSelectedItem != null)
            {
                var lastIndex = _ingredients.IndexOf(_lastSelectedItem);
                var currentIndex = _ingredients.IndexOf(tappedIngredient);
                if (lastIndex == -1 || currentIndex == -1) return;
                var startIndex = Math.Min(lastIndex, currentIndex);
                var endIndex = Math.Max(lastIndex, currentIndex);
                foreach (var item in _selectedIngredients) item.IsSelected = false;
                _selectedIngredients.Clear();
                for (int i = startIndex; i <= endIndex; i++)
                {
                    _ingredients[i].IsSelected = true;
                    _selectedIngredients.Add(_ingredients[i]);
                }
            }
            else
#endif
            {
                bool isCurrentlySelected = tappedIngredient.IsSelected;
                foreach (var item in _selectedIngredients) item.IsSelected = false;
                _selectedIngredients.Clear();
                if (!isCurrentlySelected)
                {
                    tappedIngredient.IsSelected = true;
                    _selectedIngredients.Add(tappedIngredient);
                }
#if WINDOWS
                _lastSelectedItem = tappedIngredient;
#endif
            }
        }

        private async void OnAddIngredientClicked(object? sender, EventArgs e) => await Navigation.PushAsync(new AddIngredientPage());

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
                _allIngredients.RemoveAll(ing => _selectedIngredients.Contains(ing));
                await LocalStorageService.SaveAsync(_allIngredients.Cast<IngredientCsvRecord>().ToList(), restaurantId);
                if (!SessionService.IsOffline && restaurantId != null)
                {
                    foreach (var ingredient in _selectedIngredients)
                    {
                        if (ingredient.Id != null)
                            await FirestoreService.DeleteDocumentAsync($"restaurants/{restaurantId}/ingredients/{ingredient.Id}", SessionService.AuthToken);
                    }
                }
                _selectedIngredients.Clear();
                await LoadIngredients();
            }
        }

        private void SortAndFilter()
        {
            var searchTerm = IngredientsSearchBar.Text;
            IEnumerable<IngredientDisplayRecord> filtered = _allIngredients;
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filtered = _allIngredients.Where(i =>
                    (i.ItemName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (i.AliasName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (i.SupplierName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var sorted = _isSortAscending
                ? filtered.OrderBy(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null))
                : filtered.OrderByDescending(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null));
            _ingredients = sorted.ToList();
            MainThread.BeginInvokeOnMainThread(PopulateGrid);
        }

        private void OnSortClicked(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is string newSortColumn)
            {
                if (_currentSortColumn == newSortColumn) _isSortAscending = !_isSortAscending;
                else _isSortAscending = true;
                _currentSortColumn = newSortColumn;
                SortAndFilter();
            }
        }

        private void OnSearchBarTextChanged(object? sender, TextChangedEventArgs e) => SortAndFilter();

        private IngredientDisplayRecord MapToDisplayRecord(IngredientCsvRecord i) => new()
        {
            Id = i.Id,
            SupplierName = i.SupplierName,
            ItemName = i.ItemName,
            AliasName = i.AliasName,
            CasePrice = i.CasePrice,
            CaseQuantity = i.CaseQuantity,
            Unit = i.Unit,
            SKU = i.SKU
        };

        #region Bulk Import Logic
        private async void OnBulkImportClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Please select a csv or excel file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { { DevicePlatform.WinUI, new[] { ".csv", ".xlsx" } }, { DevicePlatform.macOS, new[] { "csv", "xlsx" } }, })
                });

                if (result == null || restaurantId == null) return;
                var records = new List<IngredientCsvRecord>();

                List<ImportMap> maps = SessionService.IsOffline
                    ? await LocalStorageService.LoadAsync<ImportMap>()
                    : await FirestoreService.GetCollectionAsync<ImportMap>("importMaps", SessionService.AuthToken);

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
                        using var package = new ExcelPackage(stream);
                        var (selectedMap, headerRow, headers) = FindBestMapForExcel(package, maps);
                        if (selectedMap == null) { await DisplayAlert("No Matching Map", "Could not determine an import map for this Excel file.", "OK"); return; }
                        selectedMap.HeaderRow = headerRow;
                        records = ProcessExcelPackage(package, headers, selectedMap);
                    }
                }

                if (records.Any())
                {
                    List<IngredientCsvRecord> existingIngredients = SessionService.IsOffline
                        ? await LocalStorageService.LoadAsync<IngredientCsvRecord>(restaurantId)
                        : await FirestoreService.GetCollectionAsync<IngredientCsvRecord>($"restaurants/{restaurantId}/ingredients", SessionService.AuthToken);

                    var existingIngredientsBySku = existingIngredients.Where(i => !string.IsNullOrEmpty(i.SKU)).ToDictionary(i => i.SKU!, i => i);
                    var ingredientsToUpdate = new List<IngredientCsvRecord>();
                    var ingredientsToAdd = new List<IngredientCsvRecord>();

                    foreach (var record in records)
                    {
                        if (!string.IsNullOrEmpty(record.SKU) && existingIngredientsBySku.TryGetValue(record.SKU, out var existingRecord))
                        {
                            existingRecord.CasePrice = record.CasePrice;
                            ingredientsToUpdate.Add(existingRecord);
                        }
                        else
                        {
                            record.Id = Guid.NewGuid().ToString();
                            ingredientsToAdd.Add(record);
                        }
                    }

                    var combinedList = existingIngredients.Concat(ingredientsToAdd).ToList();
                    await LocalStorageService.SaveAsync(combinedList, restaurantId);

                    if (!SessionService.IsOffline)
                    {
                        foreach (var item in ingredientsToUpdate)
                        {
                            await FirestoreService.SetDocumentAsync($"restaurants/{restaurantId}/ingredients/{item.Id}", item, SessionService.AuthToken);
                        }
                        foreach (var item in ingredientsToAdd)
                        {
                            await FirestoreService.AddDocumentAsync($"restaurants/{restaurantId}/ingredients", item, SessionService.AuthToken);
                        }
                    }

                    await DisplayAlert("Import Complete", $"{ingredientsToAdd.Count} ingredients created.\n{ingredientsToUpdate.Count} ingredients updated.", "OK");
                    await LoadIngredients();
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
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, config);
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
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("ConvertedCsv");
            for (int i = 0; i < headers.Count; i++) { worksheet.Cells[selectedMap.HeaderRow, i + 1].Value = headers[i]; }
            for (int i = 0; i < dataRows.Count; i++)
            {
                var recordDict = dataRows[i];
                for (int j = 0; j < headers.Count; j++) { worksheet.Cells[selectedMap.HeaderRow + i + 1, j + 1].Value = recordDict[headers[j]]; }
            }
            return ProcessExcelPackage(package, headers, selectedMap);
        }

        private (ImportMap? map, int headerRow, List<string> headers) FindBestMap(Stream stream, List<ImportMap> maps)
        {
            ImportMap? bestMap = null;
            int bestHeaderRow = -1;
            int bestMatchCount = 0;
            List<string> bestHeaders = new();
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
                    using var reader = new StringReader(lines[i]);
                    using var csv = new CsvReader(reader, config);
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
            List<string> excelBestHeaders = new();
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

    public class SelectedToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is bool isSelected && Application.Current?.Resources != null)
            {
                if (parameter is Grid grid && grid.BindingContext is IngredientDisplayRecord item)
                {
                    bool isEven = item.IsEven;
                    if (isSelected) return Application.Current.Resources["Accent"];
                    return isEven ? Application.Current.Resources["RowColorEven"] : Application.Current.Resources["RowColorOdd"];
                }
            }
            return Colors.Transparent;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture) => throw new NotImplementedException();
    }
}