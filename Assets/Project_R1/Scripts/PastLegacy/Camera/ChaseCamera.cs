using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace R1
{
    public class ChaseCamera : MonoBehaviour
    {
        public Transform target;             // 차량
        public Vector3 offset = new Vector3(0f, 4f, -8f); // 기본 카메라 위치
        public float positionLerpSpeed = 5f; // 위치 따라오는 속도
        public float rotationLerpSpeed = 3f; // 회전 따라오는 속도

        public float rotationInfluence = 0.4f; // 차량 회전이 카메라에 얼마나 반영될지

        private Vector3 velocity = Vector3.zero;

        void LateUpdate()
        {
            if (target == null) return;

            // 목표 위치 계산 (차량 기준 offset)
            Vector3 targetPosition = target.TransformPoint(offset);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, 1f / positionLerpSpeed);

            // 차량의 회전 각도 기반으로 카메라 방향 보간
            Quaternion targetRotation = Quaternion.LookRotation(target.forward + target.right * rotationInfluence);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
        }
    }
}
