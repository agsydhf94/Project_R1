using UnityEngine;

namespace R1
{
    public class CarHUDUpdater : MonoBehaviour
    {
        public CarHUDViewModel viewModel;

        public RealisticGearbox gearbox;


        void Update()
        {
            float speed = gearbox.GetCurrentSpeed();
            float rpm = gearbox.GetCurrentRPM(); 
            int gear = gearbox.GetCurrentGear(); 

            viewModel.Update(speed, gear, rpm);
        }
    }
}
