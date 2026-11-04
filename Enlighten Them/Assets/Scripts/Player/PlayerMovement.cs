using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;

    [Header("Animation")]
    [SerializeField] private Animator anim;

    private Rigidbody rb;
    private Vector2 movementInput;

    private bool movementEnabled = true;
    private bool back;
    private int faceDir = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (anim == null)
            anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        ReadMovementInput();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    private void ReadMovementInput()
    {
        if (!movementEnabled)
        {
            movementInput = Vector2.zero;
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        movementInput = new Vector2(horizontal, vertical);

        // Prevent diagonal movement from being faster.
        if (movementInput.sqrMagnitude > 1f)
            movementInput.Normalize();
    }

    private void ApplyMovement()
    {
        if (!movementEnabled)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        rb.velocity = new Vector3(
            movementInput.x,
            0f,
            movementInput.y
        ) * speed;
    }

    private void UpdateAnimation()
    {
        if (anim == null)
            return;

        bool isMoving =
            movementInput.sqrMagnitude > 0.001f;

        anim.SetBool("Move", isMoving);

        if (!isMoving)
            return;

        float horizontal = movementInput.x;
        float vertical = movementInput.y;

        if (Mathf.Abs(vertical) > 0.001f)
        {
            back = vertical > 0f;
            anim.SetBool("Back", back);
        }

        if (Mathf.Abs(horizontal) > 0.001f)
        {
            faceDir = horizontal > 0f ? 1 : -1;

            int visualDirection =
                back ? faceDir : -faceDir;

            Vector3 visualScale =
                anim.transform.localScale;

            visualScale.x =
                Mathf.Abs(visualScale.x) * visualDirection;

            anim.transform.localScale = visualScale;
        }
    }

    public void SetMovementEnabled(bool value)
    {
        movementEnabled = value;

        if (movementEnabled)
            return;

        movementInput = Vector2.zero;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (anim != null)
            anim.SetBool("Move", false);
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (anim != null)
            anim.SetBool("Move", false);
    }
}
