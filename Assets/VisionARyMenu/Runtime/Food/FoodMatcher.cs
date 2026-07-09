using System;
using System.Collections.Generic;
using MenuViewer.Ocr;
using UnityEngine;

namespace MenuViewer.Food
{
    public sealed class FoodMatcher
    {
        public const float DefaultMatchThreshold = 0.82f;

        private readonly float matchThreshold;

        public FoodMatcher(float matchThreshold = DefaultMatchThreshold)
        {
            this.matchThreshold = Mathf.Clamp01(matchThreshold);
        }

        public IReadOnlyList<FoodMatch> Match(IReadOnlyList<RecognizedTextLine> lines, FoodCatalog catalog)
        {
            var matchesByItem = new Dictionary<string, FoodMatch>(StringComparer.OrdinalIgnoreCase);

            if (lines == null || catalog == null)
            {
                return Array.Empty<FoodMatch>();
            }

            foreach (var line in lines)
            {
                var normalizedLine = FoodTextNormalizer.Normalize(line.text);
                if (normalizedLine.Length == 0)
                {
                    continue;
                }

                foreach (var item in catalog.Items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    var score = ScoreItem(normalizedLine, item);
                    if (score < matchThreshold)
                    {
                        continue;
                    }

                    var candidate = new FoodMatch(
                        item.Id,
                        item.DisplayName,
                        line.text,
                        score,
                        line.normalizedRect,
                        line.timestamp,
                        line.frameId);

                    if (!matchesByItem.TryGetValue(item.Id, out var current) || candidate.score > current.score)
                    {
                        matchesByItem[item.Id] = candidate;
                    }
                }
            }

            var matches = new List<FoodMatch>(matchesByItem.Values);
            matches.Sort((left, right) => right.score.CompareTo(left.score));
            return matches;
        }

        public static bool TryMatchOne(RecognizedTextLine line, FoodCatalog catalog, float threshold, out FoodMatch match)
        {
            match = default;
            if (catalog == null)
            {
                return false;
            }

            var normalizedLine = FoodTextNormalizer.Normalize(line.text);
            if (normalizedLine.Length == 0)
            {
                return false;
            }

            FoodItemDefinition bestItem = null;
            var bestScore = 0f;

            foreach (var item in catalog.Items)
            {
                if (item == null)
                {
                    continue;
                }

                var score = ScoreItem(normalizedLine, item);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestItem = item;
                }
            }

            if (bestItem == null || bestScore < Mathf.Clamp01(threshold))
            {
                return false;
            }

            match = new FoodMatch(
                bestItem.Id,
                bestItem.DisplayName,
                line.text,
                bestScore,
                line.normalizedRect,
                line.timestamp,
                line.frameId);
            return true;
        }

        private static float ScoreItem(string normalizedLine, FoodItemDefinition item)
        {
            var best = 0f;

            foreach (var alias in item.Aliases)
            {
                var normalizedAlias = FoodTextNormalizer.Normalize(alias);
                if (normalizedAlias.Length == 0)
                {
                    continue;
                }

                if (normalizedLine == normalizedAlias || ContainsWholePhrase(normalizedLine, normalizedAlias))
                {
                    return 1f;
                }

                best = Mathf.Max(best, ScoreAlias(normalizedLine, normalizedAlias));
            }

            return best;
        }

        private static float ScoreAlias(string normalizedLine, string normalizedAlias)
        {
            var lineTokens = normalizedLine.Split(' ');
            var aliasTokens = normalizedAlias.Split(' ');
            var aliasTokenCount = aliasTokens.Length;
            var bestDistanceScore = NormalizedLevenshtein(normalizedLine, normalizedAlias);

            if (lineTokens.Length >= aliasTokenCount)
            {
                for (var start = 0; start <= lineTokens.Length - aliasTokenCount; start++)
                {
                    var candidate = string.Join(" ", lineTokens, start, aliasTokenCount);
                    bestDistanceScore = Mathf.Max(bestDistanceScore, NormalizedLevenshtein(candidate, normalizedAlias));
                }
            }

            var overlap = TokenOverlap(lineTokens, aliasTokens);
            var fuzzyOverlap = FuzzyTokenOverlap(lineTokens, aliasTokens);
            return Mathf.Max(bestDistanceScore, overlap, fuzzyOverlap);
        }

        private static bool ContainsWholePhrase(string normalizedLine, string normalizedAlias)
        {
            return normalizedLine.StartsWith(normalizedAlias + " ", StringComparison.Ordinal)
                || normalizedLine.EndsWith(" " + normalizedAlias, StringComparison.Ordinal)
                || normalizedLine.Contains(" " + normalizedAlias + " ");
        }

        private static float TokenOverlap(string[] lineTokens, string[] aliasTokens)
        {
            if (aliasTokens.Length == 0)
            {
                return 0f;
            }

            var lineSet = new HashSet<string>(lineTokens, StringComparer.Ordinal);
            var hits = 0;

            foreach (var token in aliasTokens)
            {
                if (lineSet.Contains(token))
                {
                    hits++;
                }
            }

            return hits / (float)aliasTokens.Length;
        }

        private static float FuzzyTokenOverlap(string[] lineTokens, string[] aliasTokens)
        {
            if (lineTokens.Length == 0 || aliasTokens.Length == 0)
            {
                return 0f;
            }

            var total = 0f;
            foreach (var aliasToken in aliasTokens)
            {
                var best = 0f;
                foreach (var lineToken in lineTokens)
                {
                    best = Mathf.Max(best, NormalizedLevenshtein(lineToken, aliasToken));
                }

                total += best;
            }

            return total / aliasTokens.Length;
        }

        private static float NormalizedLevenshtein(string left, string right)
        {
            if (left == right)
            {
                return 1f;
            }

            if (left.Length == 0 || right.Length == 0)
            {
                return 0f;
            }

            var distance = LevenshteinDistance(left, right);
            var maxLength = Mathf.Max(left.Length, right.Length);
            return 1f - distance / (float)maxLength;
        }

        private static int LevenshteinDistance(string left, string right)
        {
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];

            for (var column = 0; column <= right.Length; column++)
            {
                previous[column] = column;
            }

            for (var row = 1; row <= left.Length; row++)
            {
                current[0] = row;

                for (var column = 1; column <= right.Length; column++)
                {
                    var substitutionCost = left[row - 1] == right[column - 1] ? 0 : 1;
                    current[column] = Math.Min(
                        Math.Min(current[column - 1] + 1, previous[column] + 1),
                        previous[column - 1] + substitutionCost);
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[right.Length];
        }
    }
}
