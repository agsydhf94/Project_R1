using UnityEngine;

namespace R1
{
    /// <summary>
    /// Handles player input mapping for vehicle control.
    /// Captures movement axes, handbrake, and boosting states.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        /// <summary>
        /// Forward/backward movement input.
        /// </summary>
        public float vertical;

        /// <summary>
        /// Left/right steering input.
        /// </summary>
        public float horizontal;

        /// <summary>
        /// Whether the handbrake input is active (Jump axis).
        /// </summary>
        public bool handbrake;

        /// <summary>
        /// Whether the boost key (Left Shift) is pressed.
        /// </summary>

        public bool boosting;


        /// <summary>
        /// Runs input polling if the object is tagged as Player.
        /// </summary>
        private void FixedUpdate()
        {
            if (gameObject.tag == "Player")
            {
                PlayerInput();
            }
        }


        /// <summary>
        /// Reads player input and updates movement, handbrake, and boosting state.
        /// </summary>
        private void PlayerInput()
        {
            vertical = Input.GetAxis("Vertical");
            horizontal = Input.GetAxis("Horizontal");
            handbrake = Input.GetAxis("Jump") != 0;

            if (Input.GetKey(KeyCode.LeftShift))
            {
                boosting = true;
            }
            else
            {
                boosting = false;
            }
        }
    }
}
