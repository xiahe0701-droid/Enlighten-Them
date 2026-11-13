using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldPromptBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector3 rotationOffset;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        transform.rotation =
            targetCamera.transform.rotation *
            Quaternion.Euler(rotationOffset);
    }
}
