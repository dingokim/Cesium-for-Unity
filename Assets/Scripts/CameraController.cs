using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float h = -500;//카메라 높이.
    public float rotationSpeed = 5.0f; // 마우스 회전 속도
    public float moveSpeed = 10.0f; // 카메라 이동 속도
    public float zoomSpeed = 10.0f; // 줌 속도
    public float minZoomDistance = 2.0f; // 최소 줌 거리
    public float maxZoomDistance = 50.0f; // 최대 줌 거리

    private Vector3 currentRotation;

    void Start()
    {
        currentRotation = transform.eulerAngles;
        h = gameObject.transform.position.y;
    }

    void Update()
    {
        // 마우스 우클릭 드래그로 회전
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

            currentRotation.y += mouseX;
            currentRotation.x -= mouseY;

            // x축 회전을 -90도에서 90도 사이로 제한
            currentRotation.x = Mathf.Clamp(currentRotation.x, -89.9f, 89.9f);

            transform.eulerAngles = currentRotation;
        }

        // WSAD로 카메라 좌표계 기준 이동 (Y축 제거)
        Vector3 forward = transform.forward;
        forward.y = 0; // Y 성분 제거
        if (forward == Vector3.zero) forward = Vector3.forward;
        forward.Normalize(); // 방향 벡터 정규화

        Vector3 right = transform.right;
        right.y = 0; // Y 성분 제거
        right.Normalize(); // 방향 벡터 정규화

        float moveX = Input.GetAxis("Horizontal") * moveSpeed * Time.deltaTime; // A, D 입력
        float moveZ = Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime; // W, S 입력
        Vector3 move = right * moveX + forward * moveZ;

        transform.position += move;

        // 마우스 휠로 카메라 좌표계 기준 앞뒤 이동 (줌인/줌아웃)
        float scroll = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        //float scroll = Input.mouseScrollDelta.y * zoomSpeed;
        Vector3 forwardMove = transform.forward * scroll;

        // 최소/최대 거리 제한
        float distanceFromOrigin = Vector3.Distance(transform.position + forwardMove, Vector3.zero);
        if (distanceFromOrigin >= minZoomDistance && distanceFromOrigin <= maxZoomDistance)
        {
            transform.position += forwardMove;
        }

        //카메라의 높이 h로 고정
        transform.position = new Vector3(transform.position.x, h, transform.position.z);
    }
}