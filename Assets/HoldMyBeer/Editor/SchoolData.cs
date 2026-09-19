using HoldMyBeer.Gameplay.Day;
using UnityEditor;
using UnityEngine;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// The data side of the school: schedules, subjects, menus and progression tiers.
    ///
    /// These are assets rather than fields on <c>DayState</c> so the game can be
    /// retuned — and two rhythms compared — without recompiling, and so that adding a
    /// subject is an asset plus its prefabs rather than a code change.
    /// </summary>
    public readonly struct SchoolData
    {
        private const string Folder = "Assets/HoldMyBeer/Data";

        public SchoolData(DaySchedule comfort, DaySchedule nervous, CourseCatalog courses, CourseCatalog menus,
                          ProgressionTable progression)
        {
            ComfortSchedule = comfort;
            NervousSchedule = nervous;
            Courses = courses;
            Menus = menus;
            Progression = progression;
        }

        /// <summary>~20 min: the durations the design was written with.</summary>
        public DaySchedule ComfortSchedule { get; }

        /// <summary>~16 min: the floor below which a phase no longer has time to exist.</summary>
        public DaySchedule NervousSchedule { get; }

        public CourseCatalog Courses { get; }
        public CourseCatalog Menus { get; }
        public ProgressionTable Progression { get; }

        public static SchoolData Create(GameObject[] eps, GameObject[] techno, GameObject[] general,
                                        GameObject[] science, GameObject[][] menus)
        {
            EnsureFolder();

            var comfort = Schedule("Schedule_Comfort", 20f, 90f, 300f, 420f, 90f);
            var nervous = Schedule("Schedule_Nervous", 20f, 60f, 270f, 300f, 60f);

            // The order is the unlock order: a tier opens the first N of this list, so
            // the general classroom and the gym come before the lab's chain reactions.
            var courses = Catalog("CourseCatalog", new[]
            {
                Course("Cours_Francais", "Français", RoomId.General, general, sharesGeneralRoom: true),
                Course("Cours_EPS", "EPS", RoomId.Gym, eps),
                Course("Cours_Maths", "Maths", RoomId.General, general, sharesGeneralRoom: true),
                Course("Cours_Techno", "Techno", RoomId.Techno, techno),
                Course("Cours_Philo", "Philo", RoomId.General, general, sharesGeneralRoom: true),
                Course("Cours_PhysiqueChimie", "Physique-Chimie", RoomId.Lab, science)
            });

            var menuDefinitions = new CourseDefinition[menus.Length];
            for (var i = 0; i < menus.Length; i++)
            {
                menuDefinitions[i] = Course($"Menu_{i + 1}", $"Menu {i + 1}", RoomId.Cafeteria, menus[i]);
            }

            var menuCatalog = Catalog("MenuCatalog", menuDefinitions);

            var progression = Asset<ProgressionTable>("ProgressionTable");
            Set(progression, s =>
            {
                var tiers = new Object[]
                {
                    Tier("Tier_1", "Les cours généraux et l'EPS", courses: 2, adults: 0, vigilance: 1f, locked: 0.7f),
                    Tier("Tier_2", "La salle de techno ouvre — et son matériel", courses: 4, adults: 1,
                        vigilance: 1.1f, locked: 0.8f),
                    Tier("Tier_3", "Le labo : réactions en chaîne", courses: 6, adults: 1, vigilance: 1.25f,
                        locked: 0.8f),
                    Tier("Tier_4", "Le lycée se méfie de vous", courses: 6, adults: 2, vigilance: 1.4f,
                        locked: 0.9f)
                };

                var array = s.FindProperty("tiers");
                array.arraySize = tiers.Length;
                for (var i = 0; i < tiers.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = tiers[i];
                }

                s.FindProperty("maxVigilanceScale").floatValue = 1.6f;
            });

            AssetDatabase.SaveAssets();
            return new SchoolData(comfort, nervous, courses, menuCatalog, progression);
        }

        private static DaySchedule Schedule(string name, float arrival, float morningBreak, float classTime,
                                            float lunch, float dismissal)
        {
            var schedule = Asset<DaySchedule>(name);
            Set(schedule, s =>
            {
                s.FindProperty("arrival").floatValue = arrival;
                s.FindProperty("morningBreak").floatValue = morningBreak;
                s.FindProperty("classTime").floatValue = classTime;
                s.FindProperty("lunch").floatValue = lunch;
                s.FindProperty("dismissal").floatValue = dismissal;
            });

            return schedule;
        }

        private static CourseDefinition Course(string assetName, string displayName, RoomId room,
                                               GameObject[] items, bool sharesGeneralRoom = false)
        {
            var course = Asset<CourseDefinition>(assetName);
            Set(course, s =>
            {
                s.FindProperty("displayName").stringValue = displayName;
                s.FindProperty("room").enumValueIndex = (int)room;
                s.FindProperty("sharesGeneralRoom").boolValue = sharesGeneralRoom;

                var array = s.FindProperty("items");
                array.arraySize = items.Length;
                for (var i = 0; i < items.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                }
            });

            return course;
        }

        private static CourseCatalog Catalog(string name, CourseDefinition[] courses)
        {
            var catalog = Asset<CourseCatalog>(name);
            Set(catalog, s =>
            {
                var array = s.FindProperty("courses");
                array.arraySize = courses.Length;
                for (var i = 0; i < courses.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = courses[i];
                }
            });

            return catalog;
        }

        private static ProgressionTier Tier(string name, string headline, int courses, int adults, float vigilance,
                                            float locked)
        {
            var tier = Asset<ProgressionTier>(name);
            Set(tier, s =>
            {
                s.FindProperty("headline").stringValue = headline;
                s.FindProperty("availableCourses").intValue = courses;
                s.FindProperty("extraAdults").intValue = adults;
                s.FindProperty("vigilanceScale").floatValue = vigilance;
                s.FindProperty("lockedRoomChance").floatValue = locked;
            });

            return tier;
        }

        /// <summary>
        /// Reused rather than recreated: deleting and recreating these assets would
        /// break every reference a scene or prefab already holds to them.
        /// </summary>
        private static T Asset<T>(string name) where T : ScriptableObject
        {
            var path = $"{Folder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Set(Object target, System.Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/HoldMyBeer", "Data");
            }
        }
    }
}
