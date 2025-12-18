using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class camerComtrol : MonoBehaviour
{
    public Transform target;       // The object to orbit around
    public float distance = 10f;   // Default distance
    public float zoomSpeed = 2f;   // Scroll wheel sensitivity
    public float rotationSpeed = 5f; // Mouse drag sensitivity
    public float minYAngle = 10f;  // Prevent camera from going below ground
    public float maxYAngle = 80f;  // Prevent flipping over the top

    private float currentX = 0f;
    private float currentY = 30f;

    void Update()
    {
        // Scroll wheel zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, 3f, 20f); // clamp zoom range

        // Mouse drag rotation
        if (Input.GetMouseButton(1)) // Right mouse button
        {
            currentX += Input.GetAxis("Mouse X") * rotationSpeed;
            currentY -= Input.GetAxis("Mouse Y") * rotationSpeed;
            currentY = Mathf.Clamp(currentY, minYAngle, maxYAngle); // clamp vertical angle
        }
    }

    void LateUpdate()
    {
        // Calculate rotation
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 dir = new Vector3(0, 0, -distance);
        transform.position = target.position + rotation * dir;
        transform.LookAt(target.position);
    }
}
