using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace R1
{
    public class CarHUDView : UIBase<CarHUDViewModel>
    {
        public TMP_Text speedText;
        public TMP_Text gearText;
        public Image rpmGaugeFill;

        public override void Init(CarHUDViewModel vm)
        {
            base.Init(vm);

            viewModel.OnSpeedChanged += v => speedText.text = $"{v:F0} km/h";
            viewModel.OnGearChanged += g => gearText.text = $"G {g}";
            viewModel.OnRpmChanged += rpm => 
            {
                float normalized = Mathf.Clamp01(rpm / viewModel.MaxRpm);
                rpmGaugeFill.fillAmount = normalized;
            };
        }
    }
}
