using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    #region - Variables -
    [Header("Camera")]
    [SerializeField] private Camera playerCam;
    [SerializeField, Range(1, 15)] private float lookSensitivity = 5f;
    [SerializeField] private float lookXLimit = 45f;
//

    [Header("Health Component Parameters")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float timeBeforeRegen = 2.8f;
    [SerializeField] private float healthAmountIncrement = 4f;
    [SerializeField] private float healthTimeIncrement = 0.5f;

    [Header("Movement Parameters")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprint = 10f;
    [SerializeField] private float slopeFalloff = 8f;
//

    [Header("Jump Parameters")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float gravity = 15f;
//

    [Header("Crouch Parameters")]
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float crouchHeight = 0.7f;
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float timeToCrouch = 0.25f;
    [SerializeField] private Vector3 crouchCenter = new Vector3(0, 0.5f, 0);
//

    [Header("Headbob Parameters")]
    [SerializeField] private float walkBobbing = 10f;
    [SerializeField] private float walkBobAmount = 0.05f; // the amount of bobs the camera does
    [SerializeField] private float sprintBobbing = 15f;
    [SerializeField] private float sprintBobAmount = 0.12f;
    [SerializeField] private float crouchBobbing = 5f;
    [SerializeField] private float crouchBobAmount = 0.025f;
//

    [Header("Camera Zoom Parameters")]
    [SerializeField] private float zoomInTime = 0.31f;
    [SerializeField] private float zoomFOV = 30f;
//
    private Vector3 standCenter = Vector3.zero;
    private bool isCrouching;
    private bool duringCrouchAnim;
    private Vector3 movDir = Vector3.zero;
    private float xRot = 0;
    private float defaultYPos = 0;
    private float timer;
    private Vector3 hitPointNorm;
    private float defaultFOV;
    private Coroutine zoomRoutine;
    private float currHealth;
    private Coroutine regeneratingHealth;
//

    private bool isSliding
    {
        get
        {
            //Debug.DrawRay(transform.position, Vector3.down, Color.black);
            if (onGround && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 2f))
            {
                hitPointNorm = slopeHit.normal;
                return Vector3.Angle(hitPointNorm, Vector3.up) > charController.slopeLimit;
            }
            else
                return false;
        }
    }
//
    private bool canMove = true;
    private bool canCrouch = true;
    private bool canJump = true;
    private bool canSprint = true;
    private bool canHeadBob = true;
    private bool canSlideOnSlope = true;
    private bool canZoom = true;
//

    public static Action<float> OnTakeDMG;
    public static Action<float> OnDMG;
    public static Action<float> OnHealing;
//

    private bool zoomKeyPress => Input.GetMouseButtonDown(2);
    private bool zoomKeyRelease => Input.GetMouseButtonUp(2);
    private bool onGround => charController.isGrounded;
    private bool isSprinting => canSprint && Input.GetKey(KeyCode.LeftShift);
    private bool shouldJump => Input.GetButton("Jump") && onGround;
    private bool shouldCrouch => Input.GetKey(KeyCode.LeftControl) && !duringCrouchAnim && onGround; 
    CharacterController charController;
    #endregion

    #region - Awake / Update / OnEnable / OnDisable -
    void Awake()
    {
        playerCam = GetComponentInChildren<Camera>();
        charController = GetComponent<CharacterController>();
        defaultYPos = playerCam.transform.localPosition.y;  // cache the default head pos
        defaultFOV = playerCam.fieldOfView;  // cache the default FOV
        currHealth = maxHealth;  // set current health at start of game
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (canMove)
        {
            MovementHandler();
            CameraRotation();
        }

        if (canHeadBob)
        {
            HeadBobHandler();
        }

        if (canZoom)
        {
            ZoomHandler();
        }
    }

    private void OnEnable()
    {
        OnTakeDMG += ApplyDMG;
    }

    private void OnDisable()
    {
        OnTakeDMG -= ApplyDMG;
    }
    #endregion

    #region  - Movement Handling & Inputs -
    private void MovementHandler()
    {
        Vector3 fwd = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        float curSpeedX = canMove ? (isCrouching ? crouchSpeed : isSprinting ? sprint : walkSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isCrouching ? crouchSpeed : isSprinting ? sprint : walkSpeed) * Input.GetAxis("Horizontal") : 0;
        float movDirY = movDir.y;
        movDir = (fwd * curSpeedX) + (right * curSpeedY);

        // Jumping
        if (canJump)
        {
            if (shouldJump)
            {
                movDir.y = jumpForce;

                if (isCrouching)    //Transition to stand if jump while crouching
                {
                    StartCoroutine(CrouchStand());
                }
            }
            else
            {
                movDir.y = movDirY;
            }

            if (!onGround)
            {
                movDir.y -= gravity * Time.deltaTime;
            }
        }

        // Crouching
        if (canCrouch)
        {
            if (shouldCrouch)
            {
                StartCoroutine(CrouchStand());
            }
        }

        // Sprint Checks
        if(!onGround)
        {
            canSprint = false;
        }
        else
        {
            canSprint = true;
        }

        // Slope Check
        if (canSlideOnSlope && isSliding)
        {
            movDir += new Vector3(hitPointNorm.x, -hitPointNorm.y, hitPointNorm.z) * slopeFalloff;
        }
    }

    private void CameraRotation()
    {
        charController.Move(movDir * Time.deltaTime);

        if (canMove)
        {
            xRot += -Input.GetAxis("Mouse Y") * lookSensitivity;
            xRot = Mathf.Clamp(xRot, -lookXLimit, lookXLimit);
            playerCam.transform.localRotation = Quaternion.Euler(xRot, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSensitivity, 0);
        }
    }

    private IEnumerator CrouchStand()
    {
        if (isCrouching && Physics.Raycast(playerCam.transform.position, Vector3.up, 1f))
        {
            yield break;
        }

        duringCrouchAnim = true;

        float timeElapsed = 0;
        float targetHeight = isCrouching ? standHeight : crouchHeight;
        float curHeight = charController.height;
        Vector3 targetCenter = isCrouching ? standCenter : crouchCenter;
        Vector3 curCenter = charController.center;

        while (timeElapsed < timeToCrouch)
        {
            charController.height = Mathf.Lerp(curHeight, targetHeight, timeElapsed/timeToCrouch);
            charController.center = Vector3.Lerp(curCenter, targetCenter, timeElapsed/timeToCrouch);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        charController.height = targetHeight;
        charController.center = targetCenter;

        isCrouching = !isCrouching;

        duringCrouchAnim = false;
    }
    #endregion

    #region - Camera Zoom & HeadBob
    private void HeadBobHandler()
    {
        if (!onGround) return;

        if (Mathf.Abs(movDir.x) > 0.1f || Mathf.Abs(movDir.z) > 0.1f)
        {
            timer += Time.deltaTime * (isCrouching ? crouchBobbing : isSprinting ? sprintBobbing : walkBobbing);
            playerCam.transform.localPosition = new Vector3(playerCam.transform.localPosition.x,
            defaultYPos + Mathf.Sin(timer) * (isCrouching ? crouchBobAmount : isSprinting ? sprintBobAmount : walkBobAmount),
            playerCam.transform.localPosition.z);
        }
    }

    private void ZoomHandler()
    {
        if (zoomKeyPress)
        {
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }

            zoomRoutine = StartCoroutine(ToggleCamZoom(true));
        }

        if (zoomKeyRelease)
        {
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }

            zoomRoutine = StartCoroutine(ToggleCamZoom(false));
        }
    }

    private IEnumerator ToggleCamZoom(bool isEnter)
    {
        float targetFov = isEnter ? zoomFOV : defaultFOV;
        float startFOV = playerCam.fieldOfView;
        float timeElapsed = 0;

        while (timeElapsed < zoomInTime)
        {
            playerCam.fieldOfView = Mathf.Lerp(startFOV, targetFov, timeElapsed / zoomInTime);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        playerCam.fieldOfView = targetFov;
        zoomRoutine = null;
    }
    #endregion

    #region - Health System -
    private void ApplyDMG(float dmg)
    {
        currHealth -= dmg;
        OnDMG?.Invoke(currHealth);  //Invoke the currhealth to prevent any errors if nothing is listening to the on dmg action

        if (currHealth <= 0)
        {
            KillPlayer();
        }
        else if (regeneratingHealth != null)
        {
            StopCoroutine(regeneratingHealth);
        }

        regeneratingHealth = StartCoroutine(RegenHealth());
    }

    private void KillPlayer()
    {
        currHealth = 0;

        if (regeneratingHealth != null)
        {
            StopCoroutine(regeneratingHealth);
        }

        print("You Dead");
    }

    private IEnumerator RegenHealth()
    {
        yield return new WaitForSeconds(timeBeforeRegen);
        WaitForSeconds waitTime = new WaitForSeconds(healthTimeIncrement);

        while (currHealth < maxHealth)
        {
            currHealth += healthAmountIncrement;

            if (currHealth > maxHealth)
            {
                currHealth = maxHealth;
            }

            OnHealing?.Invoke(currHealth);
            yield return waitTime;
        }

        regeneratingHealth = null;
    }
    #endregion
}