using UnityEditor;
using UnityEngine;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// Creates the ragdoll collision layer and unchecks it against itself.
    /// Done in code for the same reason scenes and prefabs are: so that deleting
    /// ProjectSettings and regenerating gets you back to a known-good state.
    /// </summary>
    public static class RagdollLayerSetup
    {
        public const string LayerName = "PlayerRagdoll";

        private const string TagManagerPath = "ProjectSettings/TagManager.asset";
        private const string DynamicsManagerPath = "ProjectSettings/DynamicsManager.asset";

        // 0-7 are Unity's built-in layers.
        private const int FirstUserLayer = 8;

        [MenuItem("Tools/Hold My Beer/Setup Ragdoll Layer", priority = 40)]
        public static void Setup()
        {
            var layer = EnsureLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogError(
                    $"[Hold My Beer] No free user layer left to create '{LayerName}'. " +
                    "Free one in Project Settings > Tags and Layers.");
                return;
            }

            IgnoreSelfCollision(layer);

            // Physics.IgnoreLayerCollision and SerializedObject edits to ProjectSettings
            // both live in memory until the database is flushed. Without this the
            // settings survive the session and vanish on the next Editor restart.
            AssetDatabase.SaveAssets();

            Debug.Log($"[Hold My Beer] Layer '{LayerName}' ready on index {layer}, " +
                      "self-collision disabled and written to ProjectSettings.");
        }

        public static int LayerIndex => LayerMask.NameToLayer(LayerName);

        private static int EnsureLayer(string name)
        {
            var existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);
            var layers = tagManager.FindProperty("layers");

            for (var i = FirstUserLayer; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Bones of one body must not hit each other, or the character convulses.
        /// Everything else the ragdoll must still hit — the ground above all — so this
        /// clears exactly one bit and leaves the rest of the row set.
        ///
        /// The matrix is a hidden serialized property, one bitmask per layer, and has
        /// to be written through SerializedObject: Physics.IgnoreLayerCollision only
        /// ever touches the live simulation and is lost on the next Editor restart.
        /// </summary>
        private static void IgnoreSelfCollision(int layer)
        {
            var dynamicsManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath(DynamicsManagerPath)[0]);
            var matrix = dynamicsManager.FindProperty("m_LayerCollisionMatrix");

            if (matrix == null || layer >= matrix.arraySize)
            {
                Debug.LogError(
                    "[Hold My Beer] Could not reach 'm_LayerCollisionMatrix' in " +
                    $"{DynamicsManagerPath}. The ragdoll will convulse: uncheck " +
                    $"'{LayerName}' against itself by hand in Project Settings > Physics.");
                return;
            }

            // The rows are UInt32. Writing them through intValue clamps any value with
            // the sign bit set down to 0, which would make the layer collide with
            // nothing at all and drop the ragdoll through the floor.
            var row = matrix.GetArrayElementAtIndex(layer);
            row.uintValue = uint.MaxValue & ~(1u << layer);
            dynamicsManager.ApplyModifiedPropertiesWithoutUndo();

            Physics.IgnoreLayerCollision(layer, layer, true);
        }
    }
}
