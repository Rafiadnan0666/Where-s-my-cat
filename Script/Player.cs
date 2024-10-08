using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    //some shit
    public bool IsPause;
    // Player Stats
    public float health;

    // UI
    public Text healthap;
    public Canvas canvasMain;
    public Canvas canvasPause;

    // Assignables
    public Transform playerCam;
    public Transform orientation;
    public GameObject forceFieldPrefab;

    // Other
    private Rigidbody rb;

    // Rotation and look
    private float xRotation;
    private float yRotation; // Added yRotation for horizontal camera movement
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;

    // Movement
    public float walkSpeed = 7f; // Reduced walk speed for better control
    public float runSpeed = 15f; // Reduced run speed for better control
    public float maxSpeed = 20;
    public bool grounded;
    public LayerMask whatIsGround;

    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    // Crouch & Slide
    private Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 playerScale;

    // Jumping
    private bool readyToJump = true;
    private float jumpCooldown = 0.25f;
    public float jumpForce = 550f;

    // Camera Shake
    private float walkShakeAmount = 0.02f;
    private float runShakeAmount = 0.08f; 
    private float shakeFrequency = 1.5f;
    private Vector3 initialCamPosition;

    // Input
    float x, y;
    bool jumping, crouching, sprinting;

    // Sliding
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;
    private bool isSliding = false; 

    // Step Sound
    public AudioClip stepSound;
    private AudioSource audioSource;
    private float stepInterval = 0.5f;
    private float nextStepTime = 0f;
    private bool isPlayingStepSound = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        playerScale = transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        playerCam.gameObject.SetActive(true);
        canvasPause.gameObject.SetActive(false);
        audioSource = GetComponent<AudioSource>();
        initialCamPosition = playerCam.localPosition; 
        IsPause = false;
    }

    private void FixedUpdate()
    {
        Movement();
    }

    private void Update()
    {
        MyInput();
        Look();
        SmoothCameraShake();
        PlayStepSound();
        healthap.text = health.ToString("F0");
    }

    private void MyInput()
    {
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        jumping = Input.GetButton("Jump");
        crouching = Input.GetKey(KeyCode.LeftControl);
        sprinting = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetKey(KeyCode.Escape))
        {
            if (IsPause == false)
            {
                canvasMain.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
                canvasPause.gameObject.SetActive(true);
                if (IsPause == false && Input.GetKey(KeyCode.Escape)) { 
                    IsPause = true;
                }
            }
            else
            {
                canvasMain.gameObject.SetActive(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                canvasPause.gameObject.SetActive(false);
            }
        }

        

        // Crouching
        if (Input.GetKeyDown(KeyCode.LeftControl))
            StartCrouch();
        if (Input.GetKeyUp(KeyCode.LeftControl))
            StopCrouch();
    }

    private void StartCrouch()
    {
        if (sprinting && grounded)
        {
            isSliding = true;
            rb.AddForce(orientation.transform.forward * runSpeed, ForceMode.VelocityChange); 
        }
        transform.localScale = crouchScale;
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
    }

    private void StopCrouch()
    {
        isSliding = false;
        transform.localScale = playerScale;
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    private void Movement()
    {
        if (isSliding)
        {
            // Decrease friction when sliding to make it more fun
            rb.drag = 0.5f;
        }
        else
        {
            rb.drag = 3f; // Set drag to slow down the player when not sliding
        }

        rb.AddForce(Vector3.down * Time.deltaTime * 10);

        // Adjust movement speed based on whether the player is sprinting or sliding
        float speed = isSliding ? runSpeed : (sprinting ? runSpeed : walkSpeed);

        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        // Counteract sliding and sloppy movement
        CounterMovement(x, y, mag);

        // If holding jump && ready to jump, then jump
        if (readyToJump && jumping) Jump();

        // Some multipliers
        float multiplier = 1f, multiplierV = 1f;

        // Movement in air
        if (!grounded)
        {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }

        // Apply forces to move player
        rb.AddForce(orientation.transform.forward * y * speed * Time.deltaTime * multiplier * multiplierV);
        rb.AddForce(orientation.transform.right * x * speed * Time.deltaTime * multiplier);
    }

    private void Jump()
    {
        if (grounded && readyToJump)
        {
            readyToJump = false;

            // Add jump forces
            rb.AddForce(Vector2.up * jumpForce * 1.5f);
            rb.AddForce(normalVector * jumpForce * 0.5f);

            // If jumping while falling, reset y velocity.
            Vector3 vel = rb.velocity;
            if (rb.velocity.y < 0.5f)
                rb.velocity = new Vector3(vel.x, 0, vel.z);
            else if (rb.velocity.y > 0)
                rb.velocity = new Vector3(vel.x, vel.y / 2, vel.z);

            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    private void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        yRotation += mouseX; // Accumulate yRotation for left-right movement

        playerCam.localRotation = Quaternion.Euler(xRotation, yRotation, 0); // Apply both x and y rotation
        orientation.localRotation = Quaternion.Euler(0, yRotation, 0); // Rotate the player's orientation based on mouse movement
    }

    private void CounterMovement(float x, float y, Vector2 mag)
    {
        if (!grounded || jumping) return;

        // Slow down sliding movement
        if (Mathf.Abs(mag.x) > threshold && Mathf.Abs(x) < 0.05f)
        {
            rb.AddForce(orientation.transform.right * -mag.x * counterMovement);
        }
        if (Mathf.Abs(mag.y) > threshold && Mathf.Abs(y) < 0.05f)
        {
            rb.AddForce(orientation.transform.forward * -mag.y * counterMovement);
        }

        // Limit diagonal running. This fixes a somewhat exploit where you can move faster diagonally.
        if (Mathf.Sqrt((rb.velocity.x * rb.velocity.x) + (rb.velocity.z * rb.velocity.z)) > maxSpeed)
        {
            float fallspeed = rb.velocity.y;
            Vector3 n = rb.velocity.normalized * maxSpeed;
            rb.velocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    private void SmoothCameraShake()
    {
        if (grounded && (Mathf.Abs(rb.velocity.x) > 0.1f || Mathf.Abs(rb.velocity.z) > 0.1f))
        {
            float shakeAmount = sprinting ? runShakeAmount : walkShakeAmount;
            Vector3 targetPosition = initialCamPosition + new Vector3(
                Mathf.Sin(Time.time * shakeFrequency) * shakeAmount,
                Mathf.Sin(Time.time * shakeFrequency * 0.5f) * shakeAmount,
                0);

            // Smoothly interpolate to the target position
            playerCam.localPosition = Vector3.Lerp(playerCam.localPosition, targetPosition, Time.deltaTime * 5f);
        }
        else
        {
            // Smoothly return to the initial position when not moving
            playerCam.localPosition = Vector3.Lerp(playerCam.localPosition, initialCamPosition, Time.deltaTime * 5f);
        }
    }

    private void PlayStepSound()
    {
        if (grounded && (Mathf.Abs(rb.velocity.x) > 0.1f || Mathf.Abs(rb.velocity.z) > 0.1f))
        {
            if (!isPlayingStepSound && Time.time >= nextStepTime)
            {
                audioSource.PlayOneShot(stepSound, 0.5f);
                isPlayingStepSound = true;
                nextStepTime = Time.time + stepInterval;
            }
        }
        else
        {
            isPlayingStepSound = false;
        }
    }

    public Vector2 FindVelRelativeToLook()
    {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);
        return new Vector2(xMag, yMag);
    }

    private bool IsFloor(Vector3 v)
    {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < maxSlopeAngle;
    }

    private bool cancellingGrounded;

    private void OnCollisionStay(Collision other)
    {
        // Make sure we are only checking for walkable layers
        int layer = other.gameObject.layer;
        if (whatIsGround != (whatIsGround | (1 << layer))) return;

        // Iterate through every collision in a physics update
        for (int i = 0; i < other.contactCount; i++)
        {
            Vector3 normal = other.contacts[i].normal;
            if (IsFloor(normal))
            {
                grounded = true;
                cancellingGrounded = false;
                normalVector = normal;
                CancelInvoke(nameof(StopGrounded));
            }
        }

        float delay = 3f;
        if (!cancellingGrounded)
        {
            cancellingGrounded = true;
            Invoke(nameof(StopGrounded), Time.deltaTime * delay);
        }
    }

    private void StopGrounded()
    {
        grounded = false;
    }
}
