using UnityEngine;

/// <summary>
/// Attach to Main Camera. Automatically sizes an orthographic camera
/// so the whole maze fits on screen. Optional convenience script.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFraming : MonoBehaviour
{
    public float mazeWidth = 15f;
    public float mazeHeight = 11f;
    public float padding = 1f;

    void Start()
    {
        Camera cam = GetComponent<Camera>();
        cam.orthographic = true;

        float screenRatio = (float)Screen.width / Screen.height;
        float targetRatio = mazeWidth / mazeHeight;

        if (screenRatio >= targetRatio)
        {
            cam.orthographicSize = (mazeHeight / 2f) + padding;
        }
        else
        {
            float differenceInSize = targetRatio / screenRatio;
            cam.orthographicSize = (mazeHeight / 2f) * differenceInSize + padding;
        }

        transform.position = new Vector3(0f, 0f, -10f);
    }
}
