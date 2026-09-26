using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyChase3D : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Chase Settings")]
    public float detectRange = 6f;
    public float stopDistance = 0.6f;
    public float moveSpeed = 3f;

    [Header("Physics")]
    public LayerMask obstacleLayer;
    public float obstacleCheckDistance = 0.8f;
    public float avoidStrength = 1.5f;

    private Rigidbody rb;
    private Animator anim;
    SpriteRenderer sr;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();


        anim = GetComponentInChildren<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();

        // 2.5D 游戏常用：禁止旋转，避免被撞飞后乱转
        rb.freezeRotation = true;
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;

        // 只在 XZ 平面追踪，忽略 Y
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;

        if (distance > detectRange || distance <= stopDistance)
        {
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
            anim.SetBool("Move", false);
      

            return;
        }

        Vector3 moveDir = toPlayer.normalized;

        // 避障方向
        Vector3 avoidDir = GetAvoidDirection(moveDir);

        Vector3 finalDir = (moveDir + avoidDir * avoidStrength).normalized;
        // 是否移动
        anim.SetBool("Move", true);

        // 是否朝 +Z 方向移动
        anim.SetBool("Back", finalDir.z > 0.1f ? true : false);

        if (Mathf.Abs(finalDir.x) > 0.05f)
        {
            sr.flipX = finalDir.x > 0;
        }

        Vector3 velocity = finalDir * moveSpeed;

        // 保留 Y 轴速度，避免破坏重力/地面物理
        rb.velocity = new Vector3(
            velocity.x,
            rb.velocity.y,
            velocity.z
        );

        // 可选：让敌人面向移动方向
        //if (finalDir.sqrMagnitude > 0.01f)
        //{
        //    transform.forward = finalDir;
        //}
        float targetY = Mathf.Atan2(finalDir.x, finalDir.z) * Mathf.Rad2Deg;

        // 最多旋转15°
        targetY = Mathf.Clamp(targetY, -15f, 15f);

        Quaternion targetRot = Quaternion.Euler(0, targetY, 0);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            60f * Time.fixedDeltaTime
        );
    }

    private Vector3 GetAvoidDirection(Vector3 moveDir)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        Ray ray = new Ray(origin, moveDir);

        if (Physics.Raycast(ray, out RaycastHit hit, obstacleCheckDistance, obstacleLayer))
        {
            // 根据障碍物法线获得绕开的方向
            Vector3 avoidDir = Vector3.ProjectOnPlane(moveDir, hit.normal).normalized;

            if (avoidDir.sqrMagnitude < 0.01f)
            {
                avoidDir = Vector3.Cross(Vector3.up, moveDir).normalized;
            }

            return avoidDir;
        }

        return Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawRay(origin, transform.forward * obstacleCheckDistance);
    }
}