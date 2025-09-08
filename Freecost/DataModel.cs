using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Freecost
{

    public class UnitConversion
    {
        public string? Id { get; set; }
        public string UnitName { get; set; }
        public string Category { get; set; }
        public double ToBaseFactor { get; set; }

        public UnitConversion()
        {
            UnitName = string.Empty;
            Category = string.Empty;
        }
    }

    public class IngredientCsvRecord
    {
        public string? Id { get; set; }
        public string? SupplierName { get; set; }
        public string? ItemName { get; set; }
        public string? AliasName { get; set; }
        public double CasePrice { get; set; }
        public double CaseQuantity { get; set; }
        public string? Unit { get; set; }
        public string? SKU { get; set; }
        public IngredientCsvRecord() { }
    }

    public class Recipe
    {
        public string? Id { get; set; }
        public string? SKU { get; set; }
        public string? Name { get; set; }
        public double Yield { get; set; }
        public string? YieldUnit { get; set; }
        public string? Directions { get; set; }
        public string? PhotoUrl { get; set; }
        public string? RestaurantId { get; set; }
        public List<string>? Allergens { get; set; }
        public List<RecipeIngredient>? Ingredients { get; set; }
        public double FoodCost { get; set; }
        public double Price { get; set; }
    }

    public class RecipeIngredient
    {
        public string? Name { get; set; }
        public double Quantity { get; set; }
        public string? Unit { get; set; }
        public string? IngredientId { get; set; }
        public double DisplayQuantity { get; set; }
        public string? DisplayUnit { get; set; }
    }

    public class Entree
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public double Yield { get; set; }
        public string? YieldUnit { get; set; }
        public string? Directions { get; set; }
        public string? PhotoUrl { get; set; }
        public string? RestaurantId { get; set; }
        public List<string>? Allergens { get; set; }
        public List<EntreeComponent>? Components { get; set; }
        public double FoodCost { get; set; }
        public double Price { get; set; }
        public double PlatePrice { get; set; }
    }

    public class EntreeComponent
    {
        public string? Name { get; set; }
        public double Quantity { get; set; }
        public string? Unit { get; set; }
        public string? ComponentId { get; set; }
        public double DisplayQuantity { get; set; }
        public string? DisplayUnit { get; set; }
    }

    public class ImportMap
    {
        public string? Id { get; set; }
        public string? MapName { get; set; }
        public string? SupplierName { get; set; }
        public Dictionary<string, string>? FieldMappings { get; set; }
        public string? PackColumn { get; set; }
        public string? SizeColumn { get; set; }
        public string? UnitColumn { get; set; }
        public string? CombinedQuantityUnitColumn { get; set; }
        public string? SplitCharacter { get; set; }
        public List<string>? VendorColumns { get; set; }
        public int HeaderRow { get; set; }
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

    public class Restaurant
    {
        public string? Id { get; set; }
        [JsonProperty("name")]
        public string? Name { get; set; }
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

    public class AllData
    {
        public List<IngredientCsvRecord> Ingredients { get; set; } = new List<IngredientCsvRecord>();
        public List<Recipe> Recipes { get; set; } = new List<Recipe>();
        public List<Entree> Entrees { get; set; } = new List<Entree>();
        public List<ImportMap> ImportMaps { get; set; } = new List<ImportMap>();
        public List<UnitConversion> UnitConversions { get; set; } = new List<UnitConversion>();
    }
}