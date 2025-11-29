using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;


#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
#endif

namespace R1
{
    public class GameManager : MonoBehaviour
    {
        public enum RaceState { PreRace, Countdown, Racing, Finished }

        [Header("Vehicle References")]
        public VehicleList list;
        public CarController carController;
        public GameObject startPosition;

        [Header("RPM Gauge Fill Mapping")]
        public Image rpmGauge;
        public TMP_Text rpmText;
        [Range(0f, 1f)] public float rpmFillAtZero = 0.10f;
        [Range(0f, 1f)] public float rpmFillAtMax = 0.90f;
        public float rpmFillSmoothTime = 0.06f;
        private float rpmFillCurrent, rpmFillVel;

        [Header("Needle Rotation")]
        public Transform rotationPivot;
        public float needleAngleAtZero = 143.5f;
        public float needleAngleAtMax = -143.5f;
        public float needleSmoothTime = 0.06f;
        private float needleVel;

        [Header("UI References")]
        public TMP_Text currentSpeedText;
        public TMP_Text currentPosition;
        public TMP_Text gearNum;
        public Image nitroGauge;

        [Header("Countdown Timer")]
        public float timeLeft = 4f;
        public TMP_Text timeLeftText;

        [Header("Racers List UI")]
        public GameObject uiList;
        public GameObject uiListFolder;
        public GameObject backImage;

        // ---- Private state ----
        private float startPosiziton = 32f, endPosition = -211f;
        private float desiredPosition;
        private GameObject[] presentGameObjectVehicles, fullArray;
        private List<CarEntry> presentVehicles;
        private List<GameObject> temporaryList;
        private GameObject[] temporaryArray;

        private int startPositionXvalue = -50 - 62;
        private bool arrarDisplayed = false;
        private bool countdownFlag = false;

        // ------------------------------
        // Unity Lifecycle
        // ------------------------------
        private void Awake()
        {
            // 플레이어 차량 스폰
            Instantiate(
                list.vehicles[PlayerPrefs.GetInt("pointer")],
                startPosition.transform.position,
                startPosition.transform.rotation
            );

            carController = GameObject.FindGameObjectWithTag("Player")?.GetComponent<CarController>();

            // AI 차량 수집
            presentGameObjectVehicles = SafeFindAIVehicles();
            presentVehicles = new List<CarEntry>();

            // ---- AI 등록: AiInputManager 사용 ----
            foreach (GameObject vehicle in presentGameObjectVehicles)
            {
                var ai = vehicle.GetComponent<AiInputProvider>();
                var cc = vehicle.GetComponent<CarController>();
                if (ai == null || cc == null) continue;

                presentVehicles.Add(new CarEntry(ai.currentNode, cc.carName, cc.hasFinished));
            }

            // ---- 플레이어 등록: InputManager 사용 (currentNode는 사용하지 않음 → 0으로 처리) ----
            if (carController != null)
            {
                var playerIM = carController.gameObject.GetComponent<InputManager>(); // 존재 확인만
                if (playerIM != null)
                {
                    presentVehicles.Add(new CarEntry(0, carController.carName, carController.hasFinished));
                }
            }

            temporaryArray = new GameObject[presentVehicles.Count];

            // fullArray 구성 (AI들 + 플레이어)
            temporaryList = new List<GameObject>();
            foreach (GameObject ai in presentGameObjectVehicles)
                temporaryList.Add(ai);
            if (carController != null)
                temporaryList.Add(carController.gameObject);

            fullArray = temporaryList.ToArray();

            StartCoroutine(timedLoop());
        }

        private void Start()
        {
            rpmFillCurrent = rpmFillAtZero;
            if (rotationPivot) rotationPivot.localEulerAngles = new Vector3(0, 0, needleAngleAtZero);
        }

        private void FixedUpdate()
        {
            if (carController == null) return;

            if (carController.hasFinished) displayArray();

            // UI 업데이트
            currentSpeedText.text = carController.currentSpeed.ToString("0") + " km/h";
            rpmText.text = carController.currentEngineRPM.ToString("0") + " RPM";
            RPMGaugeUpdate();
            NitrusUI();
            CoundDownTimer();
        }

        // ------------------------------
        // Vehicle Collection
        // ------------------------------
        private GameObject[] SafeFindAIVehicles()
        {
            try
            {
                var arr = GameObject.FindGameObjectsWithTag("AI");
                return arr ?? System.Array.Empty<GameObject>();
            }
            catch (UnityException)
            {
                return System.Array.Empty<GameObject>();
            }
        }

        // ------------------------------
        // UI: RPM & Needle
        // ------------------------------
        public void RPMGaugeUpdate()
        {
            if (carController == null) return;

            float maxRpm = Mathf.Max(1f, carController.maxRPM);
            float rpm01 = Mathf.Clamp01(carController.currentEngineRPM / maxRpm);

            // 게이지 fill (0.10 ~ 0.90)
            if (rpmGauge)
            {
                float targetFill = Mathf.Lerp(rpmFillAtZero, rpmFillAtMax, rpm01);
                rpmFillCurrent = Mathf.SmoothDamp(rpmFillCurrent, targetFill, ref rpmFillVel, rpmFillSmoothTime);
                rpmGauge.fillAmount = rpmFillCurrent;
            }

            // 바늘 회전
            if (rotationPivot)
            {
                float targetAngle = Mathf.Lerp(needleAngleAtZero, needleAngleAtMax, rpm01);
                float currentZ = rotationPivot.localEulerAngles.z;
                float newZ = Mathf.SmoothDampAngle(currentZ, targetAngle, ref needleVel, needleSmoothTime);
                rotationPivot.localEulerAngles = new Vector3(0f, 0f, newZ);
            }
        }

        public void ChangeGear()
        {
            if (carController == null) return;
            gearNum.text = (!carController.reverse) ? (carController.gearNum + 1).ToString() : "R";
        }

        public void NitrusUI()
        {
            if (carController == null) return;
            nitroGauge.fillAmount = carController.nitrusValue / 45f;
        }

        // ------------------------------
        // Racers Sorting & Position
        // ------------------------------
        private void sortArray()
        {
            for (int i = 0; i < fullArray.Length; i++)
            {
                var go = fullArray[i];
                var cc = go.GetComponent<CarController>();

                int node = 0;
                if (go.CompareTag("AI"))
                {
                    // AI는 AiInputManager의 currentNode 사용
                    var aiInputProvider = go.GetComponent<AiInputProvider>();
                    if (aiInputProvider != null) node = aiInputProvider.currentNode;
                }
                else if (go.CompareTag("Player"))
                {
                    // 플레이어는 InputManager만 사용 → 현재 구조에서는 0으로 유지
                    // (원하면 Player용 트래커를 별도로 붙여 node를 계산할 수 있음)
                    node = 0;
                }

                presentVehicles[i].hasFinished = cc.hasFinished;
                presentVehicles[i].name = cc.carName;
                presentVehicles[i].node = node;
            }

            // Node 기준 정렬
            if (!carController.hasFinished)
            {
                for (int i = 0; i < presentVehicles.Count; i++)
                {
                    for (int j = i + 1; j < presentVehicles.Count; j++)
                    {
                        if (presentVehicles[j].node < presentVehicles[i].node)
                        {
                            CarEntry vehicle = presentVehicles[i];
                            presentVehicles[i] = presentVehicles[j];
                            presentVehicles[j] = vehicle;
                        }
                    }
                }
            }

            // UI 갱신
            if (arrarDisplayed)
            {
                for (int i = 0; i < temporaryArray.Length; i++)
                {
                    var entry = temporaryArray[i].transform;
                    entry.Find("vehicle node").gameObject.GetComponent<Text>().text =
                        (presentVehicles[i].hasFinished) ? "finished" : "";
                    entry.Find("vehicle name").gameObject.GetComponent<Text>().text =
                        presentVehicles[i].name.ToString();
                }
            }

            // 플레이어 현재 순위 표시
            presentVehicles.Reverse();
            for (int i = 0; i < temporaryArray.Length; i++)
            {
                if (carController.carName == presentVehicles[i].name)
                {
                    currentPosition.text = ((i + 1) + "/" + presentVehicles.Count).ToString();
                }
            }
        }

        private void displayArray()
        {
            if (arrarDisplayed) return;
            uiList.SetActive(true);

            for (int i = 0; i < presentGameObjectVehicles.Length + 1; i++)
            {
                generateList(i, presentVehicles[i].hasFinished, presentVehicles[i].name);
            }

            startPositionXvalue = -50;
            arrarDisplayed = true;
            backImage.SetActive(true);
        }

        private void generateList(int index, bool num, string nameValue)
        {
            temporaryArray[index] = Instantiate(uiList, uiListFolder.transform);
            temporaryArray[index].GetComponent<RectTransform>().anchoredPosition = new Vector2(0, startPositionXvalue);
            temporaryArray[index].transform.Find("vehicle name").gameObject.GetComponent<Text>().text = nameValue;
            temporaryArray[index].transform.Find("vehicle node").gameObject.GetComponent<Text>().text = (num) ? "finished" : "";
            startPositionXvalue += 50;
        }

        private IEnumerator timedLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.7f);
                sortArray();
            }
        }

        // ------------------------------
        // Countdown & Freeze
        // ------------------------------
        private void CoundDownTimer()
        {
            if (timeLeft <= -5) return;

            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0) unfreezePlayers();
            else freezePlayers();

            if (timeLeft > 1) timeLeftText.text = timeLeft.ToString("0");
            else if (timeLeft >= -1 && timeLeft <= 1) timeLeftText.text = "GO!";
            else timeLeftText.text = "";
        }

        private void freezePlayers()
        {
            if (countdownFlag) return;
            foreach (GameObject D in fullArray)
                D.GetComponent<Rigidbody>().isKinematic = true;

            countdownFlag = true;
        }

        private void unfreezePlayers()
        {
            if (!countdownFlag) return;
            foreach (GameObject D in fullArray)
                D.GetComponent<Rigidbody>().isKinematic = false;

            countdownFlag = false;
        }

        // ------------------------------
        // Scene Management
        // ------------------------------
        public void loadAwakeScene()
        {
            SceneManager.LoadScene("awakeScene");
        }
    }
}
