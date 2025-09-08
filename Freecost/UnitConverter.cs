using System;
using System.Collections.Generic;
using System.Linq;

namespace Freecost
{
    public class UnitConversion
    {
        public string UnitName { get; set; }
        public string Category { get; set; }
        public double ToBaseFactor { get; set; }

        public UnitConversion()
        {
            UnitName = string.Empty;
            Category = string.Empty;
        }
    }

    public static class UnitConverter
    {
        private static readonly List<UnitConversion> _conversions;
        private static readonly List<string> displayUnits = new List<string> { "g", "kg", "oz", "lb", "ml", "l", "fl oz", "cup", "pt", "qt", "gal", "ea", "dz", "#AVG" };

        static UnitConverter()
        {
            _conversions = new List<UnitConversion>
            {
                new UnitConversion { UnitName = "gram", Category = "Weight", ToBaseFactor = 1 },
                new UnitConversion { UnitName = "g", Category = "Weight", ToBaseFactor = 1 },
                new UnitConversion { UnitName = "kilogram", Category = "Weight", ToBaseFactor = 1000 },
                new UnitConversion { UnitName = "kg", Category = "Weight", ToBaseFactor = 1000 },
                new UnitConversion { UnitName = "ounce", Category = "Weight", ToBaseFactor = 28.3495 },
                new UnitConversion { UnitName = "oz", Category = "Weight", ToBaseFactor = 28.3495 },
                new UnitConversion { UnitName = "pound", Category = "Weight", ToBaseFactor = 453.592 },
                new UnitConversion { UnitName = "lb", Category = "Weight", ToBaseFactor = 453.592 },
                new UnitConversion { UnitName = "lbs", Category = "Weight", ToBaseFactor = 453.592 },
                new UnitConversion { UnitName = "#AVG", Category = "Weight", ToBaseFactor = 453.592 },
                new UnitConversion { UnitName = "milliliter", Category = "Volume", ToBaseFactor = 1 },
                new UnitConversion { UnitName = "ml", Category = "Volume", ToBaseFactor = 1 },
                new UnitConversion { UnitName = "liter", Category = "Volume", ToBaseFactor = 1000 },
                new UnitConversion { UnitName = "l", Category = "Volume", ToBaseFactor = 1000 },
                new UnitConversion { UnitName = "fluid ounce", Category = "Volume", ToBaseFactor = 29.5735 },
                new UnitConversion { UnitName = "fl oz", Category = "Volume", ToBaseFactor = 29.5735 },
                new UnitConversion { UnitName = "cup", Category = "Volume", ToBaseFactor = 236.588 },
                new UnitConversion { UnitName = "pint", Category = "Volume", ToBaseFactor = 473.176 },
                new UnitConversion { UnitName = "pt", Category = "Volume", ToBaseFactor = 473.176 },
                new UnitConversion { UnitName = "quart", Category = "Volume", ToBaseFactor = 946.353 },
                new UnitConversion { UnitName = "qt", Category = "Volume", ToBaseFactor = 946.353 },
                new UnitConversion { UnitName = "gallon", Category = "Volume", ToBaseFactor = 3785.41 },
                new UnitConversion { UnitName = "gal", Category = "Volume", ToBaseFactor = 3785.41 },
                new UnitConversion { UnitName = "each", Category = "Each", ToBaseFactor = 1 },
                new UnitConversion { UnitName = "ea", Category = "Each", ToBaseFactor = 1 },
                new UnitConversion { UnitName = "dozen", Category = "Each", ToBaseFactor = 12 },
                new UnitConversion { UnitName = "dz", Category = "Each", ToBaseFactor = 12 }
            };
        }

        public static double Convert(double value, string fromUnit, string toUnit)
        {
            if (string.Equals(fromUnit, toUnit, StringComparison.OrdinalIgnoreCase)) return value;
            var from = _conversions.FirstOrDefault(c => string.Equals(c.UnitName, fromUnit, StringComparison.OrdinalIgnoreCase));
            var to = _conversions.FirstOrDefault(c => string.Equals(c.UnitName, toUnit, StringComparison.OrdinalIgnoreCase));
            if (from == null || to == null || from.Category != to.Category) throw new ArgumentException($"Invalid unit conversion from {fromUnit} to {toUnit}.");
            return (value * from.ToBaseFactor) / to.ToBaseFactor;
        }

        public static double Convert(double value, string fromUnit, double caseQuantity, string caseUnit, double casePrice)
        {
            double convertedValue = Convert(value, fromUnit, caseUnit);
            if (caseQuantity == 0) return 0;
            return (convertedValue / caseQuantity) * casePrice;
        }

        public static string? GetCategoryForUnit(string unitName)
        {
            var unit = _conversions.FirstOrDefault(c => string.Equals(c.UnitName, unitName, StringComparison.OrdinalIgnoreCase));
            return unit?.Category;
        }

        public static List<string> GetUnitsForCategory(string category)
        {
            var categoryUnits = _conversions.Where(c => c.Category == category).Select(c => c.UnitName).ToList();
            return displayUnits.Where(du => categoryUnits.Contains(du)).ToList();
        }

        public static List<string> GetAllUnitNames()
        {
            return displayUnits;
        }
    }
}