using UnityEngine;

namespace Darkmatter.TrafficSystem.Test
{
    [RequireComponent(typeof(Rigidbody))]
    public class SimplePlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 20f;
        public float turnSpeed = 120f;

        private Rigidbody rb;
        private float moveInput;
        private float turnInput;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            
            // Set up rigidbody for smooth and stable player movement
            rb.freezeRotation = true; // Prevent the player from tipping over
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        void Update()
        {
            // Gather input from standard WASD or Arrow Keys
            moveInput = Input.GetAxis("Vertical");   // W/S or Up/Down
            turnInput = Input.GetAxis("Horizontal"); // A/D or Left/Right
        }

        void FixedUpdate()
        {
            // 1. Move Forward / Backward
            Vector3 moveForce = transform.forward * (moveInput * moveSpeed);
            
            // Apply movement while preserving gravity (Y axis velocity)
            rb.linearVelocity = new Vector3(moveForce.x, rb.linearVelocity.y, moveForce.z);

            // 2. Rotate Left / Right
            Quaternion turnRotation = Quaternion.Euler(0f, turnInput * turnSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }
}
