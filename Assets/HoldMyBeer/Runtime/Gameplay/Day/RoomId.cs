namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// The rooms of the school, by role rather than by name. Replicated as a byte and
    /// used as the key between a course, its room, its door and its keys — so, like
    /// <see cref="DayPhase"/>, the numbers are part of the contract.
    /// </summary>
    public enum RoomId : byte
    {
        None = 0,
        Corridor = 1,

        /// <summary>French, maths, philosophy: one room, fixed for the whole cycle.</summary>
        General = 2,

        Techno = 3,
        Lab = 4,
        Gym = 5,
        Cafeteria = 6,
        Toilets = 7,

        /// <summary>The CPE's office, where the caught are kept.</summary>
        Detention = 8,

        /// <summary>The staff room: where keys are left lying around.</summary>
        Staff = 9
    }
}
