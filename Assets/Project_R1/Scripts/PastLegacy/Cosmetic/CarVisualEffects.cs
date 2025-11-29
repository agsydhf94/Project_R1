using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class CarVisualEffects : MonoBehaviour
    {
        
        public Material brakeLights;
        public AudioSource skildAudioSource;
        public TrailRenderer[] tireMarks;
        public ParticleSystem[] smoke;
        public ParticleSystem[] nitrusFlame;
        //public GameObject lights;
        private CarController carController;
        private InputManager inputManager;
        private bool smokeFlag = false;
        private bool lightsFlag = false;
        private bool tireMarksFlag = false;

        //do lights in here 
        private void Start()
        {
            if (gameObject.tag == "AI") return;
            carController = GetComponent<CarController>();
            inputManager = GetComponent<InputManager>();

        }

        private void FixedUpdate()
        {
            if (gameObject.tag == "AI") return;

            chectDrift();
            activateSmoke();
            activateLights();
        }

        private void activateSmoke()
        {
            if (carController.playPauseSmoke) startSmoke();
            else stopSmoke();

            if (smokeFlag)
            {
                for (int i = 0; i < smoke.Length; i++)
                {
                    var emission = smoke[i].emission;
                    emission.rateOverTime = ((int)carController.currentSpeed * 10 <= 2000) ? (int)carController.currentSpeed * 10 : 2000;
                }
            }
        }

        public void startSmoke()
        {
            if (smokeFlag) return;
            for (int i = 0; i < smoke.Length; i++)
            {
                var emission = smoke[i].emission;
                emission.rateOverTime = ((int)carController.currentSpeed * 2 >= 2000) ? (int)carController.currentSpeed * 2 : 2000;
                smoke[i].Play();
            }
            smokeFlag = true;

        }

        public void stopSmoke()
        {
            if (!smokeFlag) return;
            for (int i = 0; i < smoke.Length; i++)
            {
                smoke[i].Stop();
            }
            smokeFlag = false;
        }

        public void startNitrusEmitter()
        {
            if (carController.nitrusFlag) return;
            for (int i = 0; i < nitrusFlame.Length; i++)
            {
                nitrusFlame[i].Play();
            }

            carController.nitrusFlag = true;
        }
        public void stopNitrusEmitter()
        {
            if (!carController.nitrusFlag) return;
            for (int i = 0; i < nitrusFlame.Length; i++)
            {
                nitrusFlame[i].Stop();
            }
            carController.nitrusFlag = false;

        }

        private void activateLights()
        {
            if (inputManager.vertical < 0 || carController.currentSpeed <= 1) turnLightsOn();
            else turnLightsOff();
        }

        private void turnLightsOn()
        {
            if (lightsFlag) return;
            brakeLights.SetColor("_EmissionColor", Color.red * 5);
            lightsFlag = true;
            //lights.SetActive(true);
        }

        private void turnLightsOff()
        {
            if (!lightsFlag) return;
            brakeLights.SetColor("_EmissionColor", Color.black);
            lightsFlag = false;
            //lights.SetActive(false);
        }

        private void chectDrift()
        {
            if (inputManager.handbrake) startEmitter();
            else stopEmitter();

        }

        private void startEmitter()
        {
            if (tireMarksFlag) return;
            foreach (TrailRenderer T in tireMarks)
            {
                T.emitting = true;
            }
            skildAudioSource.Play();
            tireMarksFlag = true;
        }
        private void stopEmitter()
        {
            if (!tireMarksFlag) return;
            foreach (TrailRenderer T in tireMarks)
            {
                T.emitting = false;
            }
            skildAudioSource.Stop();
            tireMarksFlag = false;
        }
        
    }
}
