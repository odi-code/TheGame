using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;

    [Header("Rotation")]
    [SerializeField] private float rotationSmoothTime = 0.1f;

    [Header("Jumping & Gravity")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.8f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Vector3 moveInput;
    private Vector3 velocity;
    private float currentRotationVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Auto-assign the main camera if none is set in the Inspector
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);

        if (move.magnitude > 0.01f)
        {
            // Get the camera's flat forward and right (ignore any tilt/pitch)
            Vector3 cameraForward = Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 cameraRight   = Vector3.Scale(cameraTransform.right,   new Vector3(1, 0, 1)).normalized;

            // Rebuild move direction relative to where the camera is facing
            Vector3 relativeMove = (cameraForward * move.z) + (cameraRight * move.x);

            // Rotate player to face movement direction
            float targetAngle = Mathf.Atan2(relativeMove.x, relativeMove.z) * Mathf.Rad2Deg;
            float smoothedAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref currentRotationVelocity,
                rotationSmoothTime
            );
            transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

            // Move in the camera-relative direction
            controller.Move(relativeMove * speed * Time.deltaTime);
        }

        // Jumping & gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}