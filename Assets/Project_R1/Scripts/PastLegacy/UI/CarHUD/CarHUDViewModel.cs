
namespace R1
{
    public class CarHUDViewModel
    {
        public float Speed { get; private set; }
        public int Gear { get; private set; }
        public float Rpm { get; private set; }

        public float MaxRpm = 8000f;

        public event System.Action<float> OnSpeedChanged;
        public event System.Action<int> OnGearChanged;
        public event System.Action<float> OnRpmChanged;

        public void Update(float speed, int gear, float rpm)
        {
            if (Speed != speed)
            {
                Speed = speed;
                OnSpeedChanged?.Invoke(speed);
            }

            if (Gear != gear)
            {
                Gear = gear;
                OnGearChanged?.Invoke(gear);
            }

            if (Rpm != rpm)
            {
                Rpm = rpm;
                OnRpmChanged?.Invoke(rpm);
            }
        }

    }
}
