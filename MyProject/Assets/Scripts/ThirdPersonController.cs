using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Movement")]
    [Tooltip("Normal movement speed in metres per second.")]
    [SerializeField] private float walkSpeed = 5f;

    [Tooltip("Speed while the Sprint input is held.")]
    [SerializeField] private float sprintSpeed = 9f;

    [Tooltip("How quickly the character accelerates / decelerates (seconds to reach target speed).")]
    [SerializeField] private float speedSmoothTime = 0.1f;

    [Tooltip("How quickly the character body rotates to face the movement direction.")]
    [SerializeField] private float rotationSmoothTime = 0.12f;

    [Header("Jumping & Gravity")]
    [Tooltip("Upward force applied the instant the player jumps. " +
             "Tweak this one value to control jump height — try 4-6 for a normal feel.")]
    [SerializeField] private float jumpForce = 5f;

    // Gravity is fixed to standard Earth gravity (-9.81 m/s^2).
    // This constant is intentionally not exposed in the Inspector so that
    // the physics feel is consistent and predictable.
    private const float Gravity = -9.81f;

    [Tooltip("Small constant downward velocity applied while grounded. " +
             "Keeps the character pressed into slopes so it doesn't bounce off.")]
    [SerializeField] private float groundedGravity = -2f;

    [Header("Ground Check")]
    [Tooltip("Radius of the sphere used to test for ground contact.")]
    [SerializeField] private float groundCheckRadius = 0.28f;

    [Tooltip("How far below the character origin to place the ground-check sphere.")]
    [SerializeField] private float groundCheckOffset = 0.14f;

    [Tooltip("Which layers count as ground. Default is 'Everything'.")]
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Header("Camera")]
    [Tooltip("The child transform the Cinemachine Camera follows and looks at. " +
             "Create an empty child on the Player (e.g. at Y = 1.6) and assign it here.")]
    [SerializeField] private Transform cameraTarget;

    [Tooltip("Look sensitivity — slide right for a faster camera.\n" +
             "0.05 = slow & precise  |  0.5 = fast & responsive")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float lookSensitivity = 0.15f;

    [Tooltip("Highest upward angle the camera can reach (degrees above horizon).")]
    [SerializeField] private float topClamp = 70f;

    [Tooltip("Lowest downward angle the camera can reach (degrees below horizon).")]
    [SerializeField] private float bottomClamp = -30f;

    // ── Private state ──────────────────────────────────────────────────────────

    // Component references
    private CharacterController _controller;
    private Camera              _mainCamera;

    // Input values — written by New Input System callbacks each frame
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool    _jumpPressed;
    private bool    _sprintHeld;

    // Movement
    private float _currentSpeed;         // smoothed horizontal speed
    private float _speedSmoothVelocity;  // internal ref for Mathf.SmoothDamp
    private float _rotationVelocity;     // internal ref for Mathf.SmoothDampAngle

    // Physics — _verticalVelocity accumulates gravity every frame regardless of
    // grounded state, which is what makes falling feel correct.
    private float _verticalVelocity;

    // Camera
    private float _cameraYaw;    // accumulated horizontal look angle
    private float _cameraPitch;  // accumulated vertical look angle

    // Ground
    private bool _isGrounded;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _mainCamera = Camera.main;

        // Seed the camera angles from wherever the camera currently points
        // so there is no jarring snap on the first frame.
        if (_mainCamera != null)
        {
            _cameraYaw   = _mainCamera.transform.eulerAngles.y;
            _cameraPitch = _mainCamera.transform.eulerAngles.x;
        }
    }

    private void Start()
    {
        // Lock the cursor to the centre of the screen for mouse-look.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void Update()
    {
        GroundCheck();
        HandleGravityAndJump();
        HandleMovement();
        HandleCameraRotation();
    }

    // ── New Input System callbacks ─────────────────────────────────────────────
    // PlayerInput (Behavior = "Send Messages") calls these automatically.
    // The method name must be "On" + the exact Action name.

    private void OnMove(InputValue value)   => _moveInput   = value.Get<Vector2>();
    private void OnLook(InputValue value)   => _lookInput   = value.Get<Vector2>();
    private void OnJump(InputValue value)
    {
        // Only register the press when the character is on the ground.
        // Ignoring airborne presses here is the cleanest gate — it means
        // _jumpPressed can never be true while airborne, so mid-air jumping
        // is impossible no matter how quickly the player taps the button.
        if (value.isPressed && _isGrounded)
            _jumpPressed = true;
    }
    private void OnSprint(InputValue value) => _sprintHeld  = value.isPressed;

    // ── Ground detection ───────────────────────────────────────────────────────

    private void GroundCheck()
    {
        // Cast a small sphere just below the character's feet.
        Vector3 spherePos = new Vector3(
            transform.position.x,
            transform.position.y - groundCheckOffset,
            transform.position.z);

        _isGrounded = Physics.CheckSphere(
            spherePos,
            groundCheckRadius,
            groundLayerMask,
            QueryTriggerInteraction.Ignore);
    }

    // ── Gravity & jumping ──────────────────────────────────────────────────────

    private void HandleGravityAndJump()
    {
        if (_isGrounded)
        {
            // Once grounded, stop the downward accumulation so the character
            // doesn't build up a huge negative velocity while standing.
            // We keep a small negative value so the character stays pressed
            // into slopes rather than floating off them on the next frame.
            if (_verticalVelocity < 0f)
                _verticalVelocity = groundedGravity;

            // Jump — only allowed while grounded.
            if (_jumpPressed)
            {
                // Directly assign the jump force as the upward velocity.
                // This is the simplest and most intuitive approach:
                // increase jumpForce in the Inspector to jump higher.
                _verticalVelocity = jumpForce;
                _jumpPressed = false; // consume so it doesn't re-trigger
            }
        }

        // Gravity is applied EVERY frame — both grounded and airborne.
        //
        // KEY FIX: The previous version only added gravity in the airborne
        // (else) branch. That meant gravity didn't accumulate until the frame
        // AFTER _isGrounded became false. Combined with the large launch
        // velocity from the sqrt formula, the player would float upward
        // indefinitely on some frames.
        //
        // By always accumulating here, the moment _verticalVelocity rises
        // above groundedGravity the character falls naturally under -9.81.
        // When grounded, the groundedGravity reset above prevents the value
        // from growing unboundedly negative across many grounded frames.
        _verticalVelocity += Gravity * Time.deltaTime;
    }

    // ── Horizontal movement ────────────────────────────────────────────────────

    private void HandleMovement()
    {
        float targetSpeed = _moveInput == Vector2.zero
            ? 0f
            : (_sprintHeld ? sprintSpeed : walkSpeed);

        // Smoothly ramp speed toward the target so movement feels responsive
        // without being jerky.
        _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed, targetSpeed,
            ref _speedSmoothVelocity, speedSmoothTime);

        // Kill floating-point creep so the character fully stops.
        if (Mathf.Abs(_currentSpeed) < 0.01f) _currentSpeed = 0f;

        if (_moveInput != Vector2.zero)
        {
            // Convert the 2-D stick/keyboard input into a world-space yaw
            // angle relative to where the camera is currently pointing.
            float inputAngle     = Mathf.Atan2(_moveInput.x, _moveInput.y) * Mathf.Rad2Deg;
            float targetRotation = inputAngle + _cameraYaw;

            // Rotate the character body smoothly toward the movement direction.
            float smoothedRotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetRotation,
                ref _rotationVelocity, rotationSmoothTime);

            transform.rotation = Quaternion.Euler(0f, smoothedRotation, 0f);
        }

        // Combine horizontal movement with the vertical physics velocity and
        // pass it to the CharacterController for collision-aware displacement.
        Vector3 move = transform.forward * _currentSpeed + Vector3.up * _verticalVelocity;
        _controller.Move(move * Time.deltaTime);
    }

    // ── Camera target rotation ─────────────────────────────────────────────────

    private void HandleCameraRotation()
    {
        if (cameraTarget == null) return;

        // lookSensitivity is a single 0.05-0.5 slider in the Inspector.
        // Both axes share the same value for a consistent feel.
        _cameraYaw   += _lookInput.x * lookSensitivity;
        _cameraPitch -= _lookInput.y * lookSensitivity; // subtract = mouse-up -> look up

        // Prevent the camera from flipping past straight up or straight down.
        _cameraPitch = Mathf.Clamp(_cameraPitch, bottomClamp, topClamp);

        // Write the result to the CameraTarget transform.
        // Cinemachine reads this every frame to position and orient the camera.
        cameraTarget.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
    }

    // ── Editor helpers ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw the ground-check sphere: green when grounded, red when airborne.
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 spherePos = new Vector3(
            transform.position.x,
            transform.position.y - groundCheckOffset,
            transform.position.z);
        Gizmos.DrawWireSphere(spherePos, groundCheckRadius);
    }
#endif
}
