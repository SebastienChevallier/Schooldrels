using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// The ordered list of subjects. What travels on the wire is an <b>index into this
    /// list</b>, never the object: both peers run the same build, so the index is
    /// enough — and a client that drew its own course would be playing another game.
    /// </summary>
    [CreateAssetMenu(menuName = "Hold My Beer/Course Catalog", fileName = "CourseCatalog")]
    public sealed class CourseCatalog : ScriptableObject
    {
        [SerializeField] private CourseDefinition[] courses = System.Array.Empty<CourseDefinition>();

        public int Count => courses.Length;

        public CourseDefinition this[int index] =>
            index >= 0 && index < courses.Length ? courses[index] : null;

        public bool TryGet(int index, out CourseDefinition course)
        {
            course = this[index];
            return course != null;
        }
    }
}
