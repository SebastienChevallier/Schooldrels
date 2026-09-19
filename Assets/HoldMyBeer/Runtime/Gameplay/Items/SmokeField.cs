using System.Collections.Generic;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>
    /// Where the smoke is, for anything that needs to see through it. Clouds add
    /// themselves and take themselves away; the adults ask.
    ///
    /// It lives server-side as plain state rather than being read off the particle
    /// system: what blinds a supervisor must never depend on what a client renders.
    /// </summary>
    public sealed class SmokeField
    {
        private readonly List<SmokeCloud> _clouds = new();

        public void Add(SmokeCloud cloud) => _clouds.Add(cloud);

        public void Remove(SmokeCloud cloud) => _clouds.Remove(cloud);

        /// <summary>True when a cloud sits across the line between the two points.</summary>
        public bool Blocks(Vector3 from, Vector3 to)
        {
            for (var i = _clouds.Count - 1; i >= 0; i--)
            {
                var cloud = _clouds[i];
                if (cloud == null)
                {
                    _clouds.RemoveAt(i);
                    continue;
                }

                if (DistanceToSegment(cloud.transform.position, from, to) <= cloud.Radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            var segment = to - from;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.0001f)
            {
                return Vector3.Distance(point, from);
            }

            var t = Mathf.Clamp01(Vector3.Dot(point - from, segment) / lengthSquared);
            return Vector3.Distance(point, from + segment * t);
        }
    }
}
