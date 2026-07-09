using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MenuViewer.Food
{
    public static class FoodTextNormalizer
    {
        private static readonly Regex PricePattern = new Regex(@"(?:[$€£]\s*)?\b\d{1,3}(?:[.,]\d{2})\b");
        private static readonly Regex PunctuationPattern = new Regex(@"[^\p{L}\p{Nd}\s]");
        private static readonly Regex WhitespacePattern = new Regex(@"\s+");

        public static string Normalize(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return string.Empty;
            }

            var withoutPrices = PricePattern.Replace(rawText, " ");
            var lower = withoutPrices.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(lower.Length);

            foreach (var character in lower)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            var withoutPunctuation = PunctuationPattern.Replace(builder.ToString(), " ");
            return WhitespacePattern.Replace(withoutPunctuation, " ").Trim();
        }

        public static bool IsLikelyDescription(string rawText)
        {
            var normalized = Normalize(rawText);

            if (normalized.Length == 0)
            {
                return true;
            }

            var words = normalized.Split(' ');
            return words.Length > 8 || normalized.Length > 72;
        }
    }
}
