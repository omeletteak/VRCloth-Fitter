using System.Collections.Generic;
using UnityEngine;

namespace VRClothDeclipper
{
    /// <summary>
    /// Axial girth profiling for standardized measurement points
    /// (docs/MEASUREMENT_SPEC.md §2, §8). Bins body-surface vertices into thin
    /// bands along an axis, measures each band's perimeter as its outer outline
    /// (per-sector farthest vertex, joined in angular order — robust to
    /// triangulation and to interior/decoration vertices in a way a convex hull is
    /// not, §8), and finds the maxima (bust/hips) and minima (waist) of the
    /// smoothed profile. Pure geometry over world-space vertices — no editor
    /// dependency and nothing kept (No Cache), unit testable like the rest of Core.
    ///
    /// Bust/hips = girth maxima, waist = girth minimum, over the torso band
    /// (Hips→Chest); the smoothing + prominence gate keep a deformed/decorated body
    /// from spawning spurious points where a raw argmax would (§2 留保).
    /// </summary>
    public static class GirthProfile
    {
        public struct Band
        {
            /// <summary>0..1 fraction along start → end (band centre).</summary>
            public float axisT;
            /// <summary>Outer-outline perimeter in meters (0 = too few samples to measure).</summary>
            public float girthM;
            /// <summary>Vertices that fell within this band.</summary>
            public int sampleCount;
        }

        public struct Extremum
        {
            public int bandIndex;
            public float axisT;
            public float girthM;
            /// <summary>true = peak (bust/hips), false = valley (waist).</summary>
            public bool isMaximum;
            /// <summary>Girth gap to the adjacent opposite turn (or the profile end on a side with no turn) — small = noise.</summary>
            public float prominenceM;
        }

        /// <summary>
        /// Anatomical torso points (docs/MEASUREMENT_SPEC.md §2) named from the girth
        /// extrema of a Hips→Chest band. A flag is false when that point could not be
        /// identified (e.g. a single girth maximum cannot be both bust and hips).
        /// </summary>
        public struct TorsoPoints
        {
            public bool hasBust;
            public bool hasWaist;
            public bool hasHips;
            public Extremum bust;
            public Extremum waist;
            public Extremum hips;
        }

        /// <summary>
        /// Perimeter profile along start → end. Each vertex is projected onto the
        /// axis to choose a band and onto that band's perpendicular plane to form an
        /// outline; a band's girth is the angle-ordered length through the farthest
        /// vertex of each angular sector, so interior vertices and triangulation do
        /// not inflate it (§8). Bands with fewer than <paramref name="minSamplesPerBand"/>
        /// vertices (or fewer than 3 occupied sectors) report girthM = 0.
        /// </summary>
        public static List<Band> Compute(
            Vector3 axisStart, Vector3 axisEnd,
            IReadOnlyList<Vector3> vertices,
            int bandCount, int sectorCount, int minSamplesPerBand)
        {
            if (bandCount < 1) bandCount = 1;
            if (sectorCount < 3) sectorCount = 3;

            var bands = new List<Band>(bandCount);
            for (int b = 0; b < bandCount; b++)
            {
                bands.Add(new Band { axisT = (b + 0.5f) / bandCount, girthM = 0f, sampleCount = 0 });
            }

            Vector3 axis = axisEnd - axisStart;
            float axisLen = axis.magnitude;
            if (axisLen < 1e-6f || vertices == null || vertices.Count == 0)
            {
                return bands;
            }
            Vector3 u = axis / axisLen;
            // Any helper not parallel to the axis yields a perpendicular basis.
            Vector3 helper = Mathf.Abs(u.y) < 0.99f ? Vector3.up : Vector3.right;
            Vector3 e1 = Vector3.Cross(u, helper).normalized;
            Vector3 e2 = Vector3.Cross(u, e1); // unit, perpendicular to u and e1

            var sectorFarR = new float[bandCount][];
            var sectorPt = new Vector3[bandCount][];
            var counts = new int[bandCount];
            var bandCentre = new Vector3[bandCount];
            for (int b = 0; b < bandCount; b++)
            {
                sectorFarR[b] = new float[sectorCount];
                sectorPt[b] = new Vector3[sectorCount];
                for (int s = 0; s < sectorCount; s++) sectorFarR[b][s] = -1f;
                bandCentre[b] = axisStart + u * (axisLen * ((b + 0.5f) / bandCount));
            }

            const float twoPi = 2f * Mathf.PI;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 d = vertices[i] - axisStart;
                float axial = Vector3.Dot(d, u);
                float t = axial / axisLen;
                if (t < 0f || t > 1f) continue; // beyond the segment ends
                int b = Mathf.Clamp((int)(t * bandCount), 0, bandCount - 1);
                counts[b]++;

                float x = Vector3.Dot(d, e1);
                float y = Vector3.Dot(d, e2);
                float r = Mathf.Sqrt(x * x + y * y);
                float theta = Mathf.Atan2(y, x);
                if (theta < 0f) theta += twoPi;
                int sec = Mathf.Clamp((int)(theta / twoPi * sectorCount), 0, sectorCount - 1);
                if (r > sectorFarR[b][sec])
                {
                    sectorFarR[b][sec] = r;
                    // The outline point on the band's perpendicular plane (axial
                    // component dropped), so band thickness does not distort length.
                    sectorPt[b][sec] = bandCentre[b] + e1 * x + e2 * y;
                }
            }

            for (int b = 0; b < bandCount; b++)
            {
                Band band = bands[b];
                band.sampleCount = counts[b];
                if (counts[b] >= minSamplesPerBand)
                {
                    var pts = new List<Vector3>(sectorCount);
                    for (int s = 0; s < sectorCount; s++)
                    {
                        if (sectorFarR[b][s] >= 0f) pts.Add(sectorPt[b][s]);
                    }
                    if (pts.Count >= 3)
                    {
                        float per = 0f;
                        for (int k = 0; k < pts.Count; k++)
                        {
                            per += Vector3.Distance(pts[k], pts[(k + 1) % pts.Count]);
                        }
                        band.girthM = per;
                    }
                }
                bands[b] = band;
            }
            return bands;
        }

        /// <summary>
        /// Moving-average smoothing of the girth series. Bands with girthM == 0 are
        /// treated as gaps (skipped, not counted as zero) so missing bands do not
        /// drag neighbours down.
        /// </summary>
        public static float[] Smooth(IReadOnlyList<Band> bands, int window)
        {
            int n = bands != null ? bands.Count : 0;
            var outv = new float[n];
            if (n == 0) return outv;
            if (window < 1) window = 1;
            int half = window / 2;
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                int cnt = 0;
                for (int j = i - half; j <= i + half; j++)
                {
                    if (j < 0 || j >= n) continue;
                    if (bands[j].girthM <= 0f) continue;
                    sum += bands[j].girthM;
                    cnt++;
                }
                outv[i] = cnt > 0 ? sum / cnt : 0f;
            }
            return outv;
        }

        /// <summary>
        /// Maxima (bust/hips) and minima (waist) of the smoothed girth profile, in
        /// axis order. Smoothing first (§2: a raw argmax misfires on a deformed
        /// body); a turn whose prominence (girth gap to the adjacent opposite turn,
        /// or to the profile end on a side with no turn) is below
        /// <paramref name="minProminenceM"/> is dropped as noise.
        /// </summary>
        public static List<Extremum> FindExtrema(IReadOnlyList<Band> bands, int smoothWindow, float minProminenceM)
        {
            var result = new List<Extremum>();
            int n = bands != null ? bands.Count : 0;
            if (n < 3) return result;
            float[] g = Smooth(bands, smoothWindow);

            var valid = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (g[i] > 0f) valid.Add(i);
            }
            if (valid.Count < 3) return result;

            // Local turns over the valid (gap-free) sequence.
            var turnIdx = new List<int>();
            var turnMax = new List<bool>();
            for (int k = 1; k < valid.Count - 1; k++)
            {
                float prev = g[valid[k - 1]], cur = g[valid[k]], next = g[valid[k + 1]];
                if (cur > prev && cur >= next) { turnIdx.Add(valid[k]); turnMax.Add(true); }
                else if (cur < prev && cur <= next) { turnIdx.Add(valid[k]); turnMax.Add(false); }
            }

            for (int m = 0; m < turnIdx.Count; m++)
            {
                float cur = g[turnIdx[m]];
                // Reference on each side: the adjacent opposite turn if there is one,
                // else the profile endpoint on that side. A lone peak/valley is thus
                // measured against the surrounding baseline (the profile ends), not
                // dropped for want of a neighbouring turn.
                float leftRef = m > 0 ? g[turnIdx[m - 1]] : g[valid[0]];
                float rightRef = m < turnIdx.Count - 1 ? g[turnIdx[m + 1]] : g[valid[valid.Count - 1]];
                float prom = Mathf.Min(Mathf.Abs(cur - leftRef), Mathf.Abs(cur - rightRef));
                if (prom < minProminenceM) continue;
                result.Add(new Extremum
                {
                    bandIndex = turnIdx[m],
                    axisT = bands[turnIdx[m]].axisT,
                    girthM = bands[turnIdx[m]].girthM,
                    isMaximum = turnMax[m],
                    prominenceM = prom,
                });
            }
            return result;
        }

        /// <summary>
        /// Labels girth extrema (from <see cref="FindExtrema"/> over a Hips→Chest band,
        /// so axis order runs hips end at t=0 → chest end at t=1) as the anatomical
        /// points of docs/MEASUREMENT_SPEC.md §2: hips = the lowest-t girth maximum,
        /// bust = the highest-t maximum, waist = the tightest minimum lying between
        /// them. With a single maximum it is assigned to hips or bust by which half of
        /// the axis it sits in; with no minimum strictly between hips and bust the
        /// tightest minimum overall becomes the waist. Pure — no editor dependency.
        /// </summary>
        public static TorsoPoints ClassifyTorso(IReadOnlyList<Extremum> extrema)
        {
            var tp = new TorsoPoints();
            if (extrema == null || extrema.Count == 0) return tp;

            // extrema are already in axis order (FindExtrema walks bands low→high t).
            int firstMax = -1, lastMax = -1;
            for (int i = 0; i < extrema.Count; i++)
            {
                if (!extrema[i].isMaximum) continue;
                if (firstMax < 0) firstMax = i;
                lastMax = i;
            }
            if (firstMax >= 0 && firstMax != lastMax)
            {
                tp.hips = extrema[firstMax]; tp.hasHips = true;
                tp.bust = extrema[lastMax]; tp.hasBust = true;
            }
            else if (firstMax >= 0)
            {
                // A lone maximum can't be both bust and hips; place it by axis half.
                if (extrema[firstMax].axisT < 0.5f) { tp.hips = extrema[firstMax]; tp.hasHips = true; }
                else { tp.bust = extrema[firstMax]; tp.hasBust = true; }
            }

            // Waist = tightest minimum, preferring one between hips and bust.
            bool bounded = tp.hasHips && tp.hasBust;
            float lo = tp.hasHips ? tp.hips.axisT : float.NegativeInfinity;
            float hi = tp.hasBust ? tp.bust.axisT : float.PositiveInfinity;
            int waistIdx = FindTightestMin(extrema, bounded, lo, hi);
            if (waistIdx < 0 && bounded)
            {
                waistIdx = FindTightestMin(extrema, false, 0f, 0f); // none strictly between — fall back to global
            }
            if (waistIdx >= 0) { tp.waist = extrema[waistIdx]; tp.hasWaist = true; }
            return tp;
        }

        static int FindTightestMin(IReadOnlyList<Extremum> extrema, bool bounded, float lo, float hi)
        {
            int best = -1;
            for (int i = 0; i < extrema.Count; i++)
            {
                if (extrema[i].isMaximum) continue;
                if (bounded && (extrema[i].axisT <= lo || extrema[i].axisT >= hi)) continue;
                if (best < 0 || extrema[i].girthM < extrema[best].girthM) best = i;
            }
            return best;
        }
    }
}
