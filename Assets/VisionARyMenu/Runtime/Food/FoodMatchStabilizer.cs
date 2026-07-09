using System;
using System.Collections.Generic;

namespace MenuViewer.Food
{
    public sealed class FoodMatchStabilizer
    {
        private readonly Dictionary<string, TrackState> tracks = new Dictionary<string, TrackState>(StringComparer.OrdinalIgnoreCase);
        private readonly int requiredHits;
        private readonly double hitWindowSeconds;
        private readonly double fadeAfterSeconds;

        public FoodMatchStabilizer(int requiredHits = 2, double hitWindowSeconds = 1.5, double fadeAfterSeconds = 1.5)
        {
            this.requiredHits = Math.Max(1, requiredHits);
            this.hitWindowSeconds = hitWindowSeconds;
            this.fadeAfterSeconds = fadeAfterSeconds;
        }

        public IReadOnlyList<FoodMatch> Update(IReadOnlyList<FoodMatch> matches, double now)
        {
            if (matches != null)
            {
                foreach (var match in matches)
                {
                    if (string.IsNullOrWhiteSpace(match.itemId))
                    {
                        continue;
                    }

                    if (!tracks.TryGetValue(match.itemId, out var state) || now - state.LastSeen > hitWindowSeconds)
                    {
                        state = new TrackState();
                    }

                    state.HitCount++;
                    state.LastSeen = now;
                    state.Match = match;
                    tracks[match.itemId] = state;
                }
            }

            var stableMatches = new List<FoodMatch>();
            var expired = new List<string>();

            foreach (var pair in tracks)
            {
                var state = pair.Value;
                var secondsSinceLastSeen = now - state.LastSeen;

                if (secondsSinceLastSeen > fadeAfterSeconds)
                {
                    expired.Add(pair.Key);
                    continue;
                }

                if (state.HitCount < requiredHits)
                {
                    continue;
                }

                var stable = state.Match;
                stable.isStable = true;
                stable.hitCount = state.HitCount;
                stable.visibleAlpha = fadeAfterSeconds <= 0
                    ? 1f
                    : (float)Math.Max(0, Math.Min(1, 1 - secondsSinceLastSeen / fadeAfterSeconds));
                stableMatches.Add(stable);
            }

            foreach (var itemId in expired)
            {
                tracks.Remove(itemId);
            }

            stableMatches.Sort((left, right) => right.score.CompareTo(left.score));
            return stableMatches;
        }

        public void Reset()
        {
            tracks.Clear();
        }

        private sealed class TrackState
        {
            public int HitCount;
            public double LastSeen;
            public FoodMatch Match;
        }
    }
}
