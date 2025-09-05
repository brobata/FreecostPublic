using Google.Cloud.Firestore;
using CsvHelper.Configuration.Attributes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace Freecost
{
    [FirestoreData]
    public class IngredientCsvRecord
    {
        [Ignore]
        public string? Id { get; set; }
        [FirestoreProperty]
        public string? SupplierName { get; set; }
        [FirestoreProperty]
        public string? ItemName { get; set; }
        [FirestoreProperty]
        public string? AliasName { get; set; }
        [FirestoreProperty]
        public double CasePrice { get; set; }
        [FirestoreProperty]
        public double CaseQuantity { get; set; }
        [FirestoreProperty]
        public string? Unit { get; set; }
        [FirestoreProperty]
        public string? SKU { get; set; }
        public IngredientCsvRecord() { }
    }

    [FirestoreData]
    public class Recipe
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty]
        public string? SKU { get; set; }
        [FirestoreProperty]
        public string? Name { get; set; }
        [FirestoreProperty]
        public double Yield { get; set; }
        [FirestoreProperty]
        public string? YieldUnit { get; set; }
        [FirestoreProperty]
        public string? Directions { get; set; }
        [FirestoreProperty]
        public string? PhotoUrl { get; set; }
        [FirestoreProperty]
        public string? RestaurantId { get; set; }
        [FirestoreProperty]
        public List<string>? Allergens { get; set; }
        [FirestoreProperty]
        public List<RecipeIngredient>? Ingredients { get; set; }
        [FirestoreProperty]
        public double FoodCost { get; set; }
        [FirestoreProperty]
        public double Price { get; set; }
    }

    [FirestoreData]
    public class RecipeIngredient
    {
        [FirestoreProperty]
        public string? Name { get; set; }
        [FirestoreProperty]
        public double Quantity { get; set; }
        [FirestoreProperty]
        public string? Unit { get; set; }
        [FirestoreProperty]
        public string? IngredientId { get; set; }
    }

    [FirestoreData]
    public class Entree
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty]
        public string? Name { get; set; }
        [FirestoreProperty]
        public double Yield { get; set; }
        [FirestoreProperty]
        public string? YieldUnit { get; set; }
        [FirestoreProperty]
        public string? Directions { get; set; }
        [FirestoreProperty]
        public string? PhotoUrl { get; set; }
        [FirestoreProperty]
        public string? RestaurantId { get; set; }
        [FirestoreProperty]
        public List<string>? Allergens { get; set; }
        [FirestoreProperty]
        public List<EntreeComponent>? Components { get; set; }
        [FirestoreProperty]
        public double FoodCost { get; set; }
        [FirestoreProperty]
        public double Price { get; set; }
        [FirestoreProperty]
        public double PlatePrice { get; set; } // Add this line
    }

    [FirestoreData]
    public class EntreeComponent
    {
        [FirestoreProperty]
        public string? Name { get; set; }
        [FirestoreProperty]
        public double Quantity { get; set; }
        [FirestoreProperty]
        public string? Unit { get; set; }
        [FirestoreProperty]
        public string? ComponentId { get; set; }
    }

    [FirestoreData]
    public class ImportMap
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty]
        public string? MapName { get; set; }
        [FirestoreProperty]
        public string? SupplierName { get; set; }
        [FirestoreProperty]
        public Dictionary<string, string>? FieldMappings { get; set; }
        [FirestoreProperty]
        public string? PackColumn { get; set; }
        [FirestoreProperty]
        public string? SizeColumn { get; set; }
        [FirestoreProperty]
        public string? UnitColumn { get; set; }
        [FirestoreProperty]
        public string? CombinedQuantityUnitColumn { get; set; }
        [FirestoreProperty]
        public string? SplitCharacter { get; set; }
        [FirestoreProperty]
        public List<string>? VendorColumns { get; set; }
        [FirestoreProperty]
        public int HeaderRow { get; set; }
        [FirestoreProperty]
        public string? Delimiter { get; set; }
    }

    public class IngredientDisplay
    {
        public string? DisplayName { get; set; }
        public IngredientCsvRecord? OriginalIngredient { get; set; }
    }

    public class EntreeComponentDisplay
    {
        public string? DisplayName { get; set; }
        public IngredientCsvRecord? OriginalIngredient { get; set; }
    }

    [FirestoreData]
    public class Restaurant
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty("name")]
        public string? Name { get; set; }
    }

    [FirestoreData]
    public class IngredientDisplayRecord : IngredientCsvRecord, INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEven { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [FirestoreData]
    public class RecipeDisplayRecord : Recipe, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [FirestoreData]
    public class EntreeDisplayRecord : Entree, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AllData
    {
        public List<IngredientCsvRecord> Ingredients { get; set; } = new List<IngredientCsvRecord>();
        public List<Recipe> Recipes { get; set; } = new List<Recipe>();
        public List<Entree> Entrees { get; set; } = new List<Entree>();
        public List<ImportMap> ImportMaps { get; set; } = new List<ImportMap>();
    }
}