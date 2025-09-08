using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Freecost
{
    public static class UnitConverter
    {
        private static List<UnitConversion> _conversions = new List<UnitConversion>();

        public static async Task InitializeAsync()
        {
            // Always fetch from Firestore. The client SDK may cache this.
            // This call will work even if the user is not logged in because of our security rules.
            _conversions = await FirestoreService.GetUnitConversionsAsync();

            // Also save to local storage for offline use
            await LocalStorageService.SaveAsync(_conversions);
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
            return _conversions.Where(c => c.Category == category).Select(c => c.UnitName).ToList();
        }

        public static List<string> GetAllUnitNames()
        {
            return _conversions.Select(c => c.UnitName).Distinct().ToList();
        }
    }
}