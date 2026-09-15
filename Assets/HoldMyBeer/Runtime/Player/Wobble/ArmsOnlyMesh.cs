using System.Collections.Generic;
using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Builds the arms-only copy of a skinned mesh by keeping the triangles that are
    /// mostly weighted to arm bones and dropping the rest.
    ///
    /// Done at runtime rather than as a generated asset on purpose: a mesh created and
    /// referenced inside one editor frame does not reliably survive being serialised
    /// into a prefab, and the failure is silent — a renderer that is enabled, visible,
    /// and made of nothing. Building it here removes that whole class of bug, and the
    /// cache means the work happens once per mesh rather than once per player.
    /// </summary>
    public static class ArmsOnlyMesh
    {
        private static readonly Dictionary<Mesh, Mesh> Cache = new();

        public static Mesh For(Mesh source, Transform[] bones)
        {
            if (source == null || bones == null)
            {
                return null;
            }

            if (Cache.TryGetValue(source, out var cached) && cached != null)
            {
                return cached;
            }

            if (!source.isReadable)
            {
                Debug.LogError(
                    $"[Hold My Beer] '{source.name}' is not readable, so the owner's arms-only " +
                    "view cannot be built. Enable Read/Write on the Y Bot import settings.");
                return null;
            }

            var built = Build(source, bones);
            Cache[source] = built;
            return built;
        }

        private static Mesh Build(Mesh source, Transform[] bones)
        {
            // "Arm" catches upper arm and forearm, "Hand" catches the hand and every
            // finger. Shoulders are deliberately excluded: they read as torso.
            var isArmBone = new bool[bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                var boneName = bones[i] == null ? string.Empty : bones[i].name;
                isArmBone[i] = boneName.Contains("Arm") || boneName.Contains("Hand");
            }

            var weights = source.boneWeights;
            var vertexIsArm = new bool[source.vertexCount];

            for (var v = 0; v < source.vertexCount; v++)
            {
                var w = weights[v];
                var armWeight = 0f;

                if (w.boneIndex0 < isArmBone.Length && isArmBone[w.boneIndex0]) armWeight += w.weight0;
                if (w.boneIndex1 < isArmBone.Length && isArmBone[w.boneIndex1]) armWeight += w.weight1;
                if (w.boneIndex2 < isArmBone.Length && isArmBone[w.boneIndex2]) armWeight += w.weight2;
                if (w.boneIndex3 < isArmBone.Length && isArmBone[w.boneIndex3]) armWeight += w.weight3;

                vertexIsArm[v] = armWeight > 0.5f;
            }

            // Vertices and bind poses are kept as they are: only the index buffer is
            // rebuilt, so the copy deforms exactly like the body it came from.
            var copy = Object.Instantiate(source);
            copy.name = $"{source.name}_ArmsOnly";

            var kept = 0;
            var filtered = new List<int>();

            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                var indices = source.GetTriangles(sub);
                filtered.Clear();

                for (var t = 0; t + 2 < indices.Length; t += 3)
                {
                    if (vertexIsArm[indices[t]] && vertexIsArm[indices[t + 1]] && vertexIsArm[indices[t + 2]])
                    {
                        filtered.Add(indices[t]);
                        filtered.Add(indices[t + 1]);
                        filtered.Add(indices[t + 2]);
                    }
                }

                kept += filtered.Count / 3;
                copy.SetTriangles(filtered, sub);
            }

            if (kept != 0)
            {
                return copy;
            }

            Object.Destroy(copy);
            return null;
        }
    }
}
