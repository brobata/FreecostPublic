using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Plugin.Firebase.Firestore;

namespace Freecost
{
    [FirestoreDataType]
    public class UnitConversion
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty("UnitName")]
        public string UnitName { get; set; } = string.Empty;
        [FirestoreProperty("Category")]
        public string Category { get; set; } = string.Empty;
        [FirestoreProperty("ToBaseFactor")]
        public double ToBaseFactor { get; set; }
    }

    [FirestoreDataType]
    public class IngredientCsvRecord
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty("SupplierName")]
        public string? SupplierName { get; set; }
        [FirestoreProperty("ItemName")]
        public string? ItemName { get; set; }
        [FirestoreProperty("AliasName")]
        public string? AliasName { get; set; }
        [FirestoreProperty("CasePrice")]
        public double CasePrice { get; set; }
        [FirestoreProperty("CaseQuantity")]
        public double CaseQuantity { get; set; }
        [FirestoreProperty("Unit")]
        public string? Unit { get; set; }
        [FirestoreProperty("SKU")]
        public string? SKU { get; set; }
    }

    [FirestoreDataType]
    public class Recipe
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty("SKU")]
        public string? SKU { get; set; }
        [FirestoreProperty("Name")]
        public string? Name { get; set; }
        [FirestoreProperty("Yield")]
        public double Yield { get; set; }
        [FirestoreProperty("YieldUnit")]
        public string? YieldUnit { get; set; }
        [FirestoreProperty("Directions")]
        public string? Directions { get; set; }
        [FirestoreProperty("PhotoUrl")]
        public string? PhotoUrl { get; set; }
        [FirestoreProperty("RestaurantId")]
        public string? RestaurantId { get; set; }
        [FirestoreProperty("Allergens")]
        public List<string>? Allergens { get; set; }
        [FirestoreProperty("Ingredients")]
        public List<RecipeIngredient>? Ingredients { get; set; }
        [FirestoreProperty("FoodCost")]
        public double FoodCost { get; set; }
        [FirestoreProperty("Price")]
        public double Price { get; set; }
    }

    [FirestoreDataType]
    public class RecipeIngredient
    {
        [FirestoreProperty("Name")]
        public string? Name { get; set; }
        [FirestoreProperty("Quantity")]
        public double Quantity { get; set; }
        [FirestoreProperty("Unit")]
        public string? Unit { get; set; }
        [FirestoreProperty("IngredientId")]
        public string? IngredientId { get; set; }
        [FirestoreProperty("DisplayQuantity")]
        public double DisplayQuantity { get; set; }
        [FirestoreProperty("DisplayUnit")]
        public string? DisplayUnit { get; set; }
    }

    [FirestoreDataType]
    public class Entree
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty("Name")]
        public string? Name { get; set; }
        [FirestoreProperty("Yield")]
        public double Yield { get; set; }
        [FirestoreProperty("YieldUnit")]
        public string? YieldUnit { get; set; }
        [FirestoreProperty("Directions")]
        public string? Directions { get; set; }
        [FirestoreProperty("PhotoUrl")]
        public string? PhotoUrl { get; set; }
        [FirestoreProperty("RestaurantId")]
        public string? RestaurantId { get; set; }
        [FirestoreProperty("Allergens")]
        public List<string>? Allergens { get; set; }
        [FirestoreProperty("Components")]
        public List<EntreeComponent>? Components { get; set; }
        [FirestoreProperty("FoodCost")]
        public double FoodCost { get; set; }
        [FirestoreProperty("Price")]
        public double Price { get; set; }
        [FirestoreProperty("PlatePrice")]
        public double PlatePrice { get; set; }
    }

    [FirestoreDataType]
    public class EntreeComponent
    {
        [FirestoreProperty("Name")]
        public string? Name { get; set; }
        [FirestoreProperty("Quantity")]
        public double Quantity { get; set; }
        [FirestoreProperty("Unit")]
        public string? Unit { get; set; }
        [FirestoreProperty("ComponentId")]
        public string? ComponentId { get; set; }
        [FirestoreProperty("DisplayQuantity")]
        public double DisplayQuantity { get; set; }
        [FirestoreProperty("DisplayUnit")]
        public string? DisplayUnit { get; set; }
    }

    [FirestoreDataType]
    public class ImportMap
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty("MapName")]
        public string? MapName { get; set; }
        [FirestoreProperty("SupplierName")]
        public string? SupplierName { get; set; }
        [FirestoreProperty("FieldMappings")]
        public Dictionary<string, string>? FieldMappings { get; set; }
        [FirestoreProperty("PackColumn")]
        public string? PackColumn { get; set; }
        [FirestoreProperty("SizeColumn")]
        public string? SizeColumn { get; set; }
        [FirestoreProperty("UnitColumn")]
        public string? UnitColumn { get; set; }
        [FirestoreProperty("CombinedQuantityUnitColumn")]
        public string? CombinedQuantityUnitColumn { get; set; }
        [FirestoreProperty("SplitCharacter")]
        public string? SplitCharacter { get; set; }
        [FirestoreProperty("VendorColumns")]
        public List<string>? VendorColumns { get; set; }
        [FirestoreProperty("HeaderRow")]
        public int HeaderRow { get; set; }
        [FirestoreProperty("Delimiter")]
        public string? Delimiter { get; set; }
    }

    [FirestoreDataType]
    public class Restaurant
    {
        [FirestoreDocumentId]
        public string? Id { get; set; }
        [FirestoreProperty("name")]
        public string? Name { get; set; }
    }

    public class AllData
    {
        public List<IngredientCsvRecord> Ingredients { get; set; } = new();
        public List<Recipe> Recipes { get; set; } = new();
        public List<Entree> Entrees { get; set; } = new();
        public List<ImportMap> ImportMaps { get; set; } = new();
        public List<UnitConversion> UnitConversions { get; set; } = new();
    }

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
    public class RecipeDisplayRecord : Recipe, INotifyPropertyChanged
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
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class EntreeDisplayRecord : Entree, INotifyPropertyChanged
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
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

