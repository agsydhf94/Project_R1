
namespace R1
{
    /// <summary>
    /// Represents an entry for a car in the race.
    /// Tracks the current waypoint index, car name, and whether the car has finished.
    /// </summary>
    public class CarEntry
    {
        /// <summary>
        /// The index of the current waypoint (node) the car has reached.
        /// </summary>
        public int node;

        /// <summary>
        /// The display name of the car.
        /// </summary>
        public string name;

        /// <summary>
        /// Whether the car has finished the race.
        /// </summary>
        public bool hasFinished;

        /// <summary>
        /// Creates a new CarEntry instance with the given waypoint index, name, and finished status.
        /// </summary>
        /// <param name="node">The current waypoint index of the car.</param>
        /// <param name="name">The display name of the car.</param>
        /// <param name="hasFinished">Whether the car has finished the race.</param>
        public CarEntry(int node, string name, bool hasFinished)
        {
            this.node = node;
            this.name = name;
            this.hasFinished = hasFinished;
        }
    }
}
