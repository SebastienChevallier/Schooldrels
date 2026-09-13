using System.Collections.Generic;
using System.IO;
using HoldMyBeer.Player.Wobble;
using UnityEditor;
using UnityEngine;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// Builds the physical ragdoll prefab from the Mixamo rig. Never spawned by NGO:
    /// each client simulates every avatar's ragdoll locally, so it must NOT be added
    /// to the NetworkPrefabsList.
    /// </summary>
    public static class PlayerRagdollBuilder
    {
        private const string ModelPath = "Assets/HoldMyBeer/_ART/Player/Models/Y Bot.fbx";
        private const string PrefabPath = "Assets/HoldMyBeer/Prefabs/PlayerRagdoll.prefab";

        private readonly struct BoneSpec
        {
            private BoneSpec(string bone, string parent, float mass, float radius,
                             Vector3 boxSize, Vector3 boxCenter, bool isBox, float stiffness)
            {
                Bone = bone;
                Parent = parent;
                Mass = mass;
                Radius = radius;
                BoxSize = boxSize;
                BoxCenter = boxCenter;
                IsBox = isBox;
                Stiffness = stiffness;
            }

            public string Bone { get; }
            public string Parent { get; }
            public float Mass { get; }
            public float Radius { get; }
            public Vector3 BoxSize { get; }
            public Vector3 BoxCenter { get; }
            public bool IsBox { get; }

            /// <summary>Multiplies the rig-wide spring. High = holds its pose, low = wobbles.</summary>
            public float Stiffness { get; }

            /// <summary>A limb: a capsule whose length is measured from the child bone.</summary>
            public static BoneSpec Capsule(string bone, string parent, float mass, float radius,
                                           float stiffness)
                => new(bone, parent, mass, radius, Vector3.zero, Vector3.zero, false, stiffness);

            /// <summary>A torso or foot: a box, since no single axis describes it.</summary>
            public static BoneSpec Box(string bone, string parent, float mass,
                                       Vector3 size, Vector3 center, float stiffness)
                => new(bone, parent, mass, 0f, size, center, true, stiffness);
        }

        // The classic 13-body humanoid ragdoll. Masses total roughly 70 units;
        // absolute values matter less than their ratios.
        private static readonly BoneSpec[] Bones =
        {
            BoneSpec.Box("mixamorig:Hips", null, 12f,
                new Vector3(0.28f, 0.20f, 0.22f), new Vector3(0f, 0.04f, 0f), 1f),
            BoneSpec.Box("mixamorig:Spine1", "mixamorig:Hips", 16f,
                new Vector3(0.34f, 0.28f, 0.22f), new Vector3(0f, 0.12f, 0f), 5f),
            BoneSpec.Capsule("mixamorig:Head", "mixamorig:Spine1", 5f, 0.10f, 3f),
            BoneSpec.Capsule("mixamorig:LeftUpLeg", "mixamorig:Hips", 7f, 0.09f, 2f),
            BoneSpec.Capsule("mixamorig:LeftLeg", "mixamorig:LeftUpLeg", 4f, 0.07f, 1.6f),
            BoneSpec.Box("mixamorig:LeftFoot", "mixamorig:LeftLeg", 1f,
                new Vector3(0.09f, 0.19f, 0.10f), new Vector3(0f, 0.08f, 0f), 1.2f),
            BoneSpec.Capsule("mixamorig:RightUpLeg", "mixamorig:Hips", 7f, 0.09f, 2f),
            BoneSpec.Capsule("mixamorig:RightLeg", "mixamorig:RightUpLeg", 4f, 0.07f, 1.6f),
            BoneSpec.Box("mixamorig:RightFoot", "mixamorig:RightLeg", 1f,
                new Vector3(0.09f, 0.19f, 0.10f), new Vector3(0f, 0.08f, 0f), 1.2f),

            // The arms stay the loosest bones on the body — six times softer than the
            // spine — because they are what the player actually watches. Softer than
            // this and they hang so far below their IK goal that they leave the frame.
            BoneSpec.Capsule("mixamorig:LeftArm", "mixamorig:Spine1", 2.5f, 0.06f, 0.9f),
            BoneSpec.Capsule("mixamorig:LeftForeArm", "mixamorig:LeftArm", 1.5f, 0.05f, 0.8f),
            BoneSpec.Capsule("mixamorig:RightArm", "mixamorig:Spine1", 2.5f, 0.06f, 0.9f),
            BoneSpec.Capsule("mixamorig:RightForeArm", "mixamorig:RightArm", 1.5f, 0.05f, 0.8f),

            // Hands earn a body of their own so a carried item hangs off something
            // physical, and so there is one more segment wobbling at the end of the arm.
            BoneSpec.Box("mixamorig:LeftHand", "mixamorig:LeftForeArm", 0.5f,
                new Vector3(0.05f, 0.11f, 0.09f), new Vector3(0f, 0.05f, 0f), 0.8f),
            BoneSpec.Box("mixamorig:RightHand", "mixamorig:RightForeArm", 0.5f,
                new Vector3(0.05f, 0.11f, 0.09f), new Vector3(0f, 0.05f, 0f), 0.8f)
        };

        [MenuItem("Tools/Hold My Beer/Rebuild Player Ragdoll", priority = 41)]
        public static GameObject Build()
        {
            RagdollLayerSetup.Setup();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new FileNotFoundException(
                    $"Player model not found at '{ModelPath}'. Re-import the Y Bot FBX.");
            }

            RequireUnoptimizedHierarchy();

            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = "PlayerRagdoll";

            // Unpack before adding physics: components added to a model-prefab instance
            // would be saved as overrides, which makes the result fragile to re-import.
            // This prefab is regenerated from the model anyway, so nothing is lost.
            PrefabUtility.UnpackPrefabInstance(
                root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            SetLayerRecursively(root.transform, RagdollLayerSetup.LayerIndex);

            var bodies = new Dictionary<string, Rigidbody>();

            foreach (var spec in Bones)
            {
                var bone = FindBone(root.transform, spec.Bone);
                if (bone == null)
                {
                    Object.DestroyImmediate(root);
                    throw new InvalidDataException(
                        $"Bone '{spec.Bone}' is missing from '{ModelPath}'. " +
                        "The rig was renamed or re-exported differently; " +
                        "update PlayerRagdollBuilder.Bones to match.");
                }

                var body = bone.gameObject.AddComponent<Rigidbody>();
                body.mass = spec.Mass;
                // FixedUpdate physics rendered in Update judders at 50 Hz without this.
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                bodies[spec.Bone] = body;

                AddCollider(bone, spec);

                bone.gameObject.AddComponent<WobbleBoneTuning>();
                var tuningSerialized = new SerializedObject(bone.GetComponent<WobbleBoneTuning>());
                tuningSerialized.FindProperty("stiffness").floatValue = spec.Stiffness;
                tuningSerialized.ApplyModifiedPropertiesWithoutUndo();

                if (spec.Parent == null)
                {
                    continue;
                }

                var joint = bone.gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = bodies[spec.Parent];
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor =
                    bodies[spec.Parent].transform.InverseTransformPoint(bone.position);

                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;

                // Deliberately unlimited: the slerp drive tension is the only thing
                // holding the body together, which is what makes a limp ragdoll at
                // tension 0 possible. Knees bending backwards is the price, and the
                // reason this is a party game rather than a simulation.
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;

                // Slerp drive, because per-axis drives fight each other on a rig
                // whose bone axes are not aligned with the world.
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.enablePreprocessing = false;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[Hold My Beer] Ragdoll prefab rebuilt at '{PrefabPath}' " +
                      $"with {Bones.Length} bodies.");
            return prefab;
        }

        /// <summary>
        /// With Optimize Game Objects on, Unity strips the bone Transforms and exposes
        /// only the ones listed in the importer, so FindBone would fail on almost
        /// everything. Better to say so once than to fail bone by bone.
        /// </summary>
        private static void RequireUnoptimizedHierarchy()
        {
            if (AssetImporter.GetAtPath(ModelPath) is ModelImporter { optimizeGameObjects: true })
            {
                throw new InvalidDataException(
                    $"'{ModelPath}' has Optimize Game Objects enabled, which strips the " +
                    "bone hierarchy the ragdoll needs. Turn it off in the model Rig " +
                    "import settings and re-import.");
            }
        }

        private static void AddCollider(Transform bone, BoneSpec spec)
        {
            if (spec.IsBox)
            {
                var box = bone.gameObject.AddComponent<BoxCollider>();
                box.size = spec.BoxSize;
                box.center = spec.BoxCenter;
                return;
            }

            // Measure the bone rather than deriving its length from its radius: a
            // uniform ratio gives a 0.65 m head on a 0.20 m skull. Measuring also
            // survives a re-export at a different scale.
            var length = BoneLength(bone);

            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = spec.Radius;
            capsule.height = Mathf.Max(length, spec.Radius * 2.05f);
            // Mixamo limb bones run along their local Y, verified on this rig.
            capsule.direction = 1;
            capsule.center = new Vector3(0f, length * 0.5f, 0f);
        }

        private static float BoneLength(Transform bone)
        {
            return bone.childCount > 0
                ? Vector3.Distance(bone.position, bone.GetChild(0).position)
                : 0f;
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindBone(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SetLayerRecursively(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (var i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursively(transform.GetChild(i), layer);
            }
        }
    }
}
