using UnityEngine;

namespace R1
{
    public class CarHUDHandler : MonoBehaviour
    {
        public RealisticGearbox gearbox;
        public CarHUDView carHUDView; // 씬에서 직접 연결

        void Start()
        {
            var viewModel = new CarHUDViewModel();
            carHUDView.Init(viewModel);

            var updater = gearbox.gameObject.AddComponent<CarHUDUpdater>();
            updater.gearbox = gearbox;
            updater.viewModel = viewModel;

            carHUDView.Open(); // 처음부터 보이게 하고 싶다면 생략해도 됨
        }
    }
}
