using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float h = -500;//ī�޶� ����.
    public float rotationSpeed = 5.0f; // ���콺 ȸ�� �ӵ�
    public float moveSpeed = 10.0f; // ī�޶� �̵� �ӵ�
    public float zoomSpeed = 10.0f; // �� �ӵ�
    public float minZoomDistance = 2.0f; // �ּ� �� �Ÿ�
    public float maxZoomDistance = 50.0f; // �ִ� �� �Ÿ�

    private Vector3 currentRotation;

    void Start()
    {
        currentRotation = transform.eulerAngles;
        h = gameObject.transform.position.y;
    }

    void Update()
    {
        // ���콺 ��Ŭ�� �巡�׷� ȸ��
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

            currentRotation.y += mouseX;
            currentRotation.x -= mouseY;

            // x�� ȸ���� -90������ 90�� ���̷� ����
            currentRotation.x = Mathf.Clamp(currentRotation.x, -89.9f, 89.9f);

            transform.eulerAngles = currentRotation;
        }

        // WSAD�� ī�޶� ��ǥ�� ���� �̵� (Y�� ����)
        Vector3 forward = transform.forward;
        forward.y = 0; // Y ���� ����
        if (forward == Vector3.zero) forward = Vector3.forward;
        forward.Normalize(); // ���� ���� ����ȭ

        Vector3 right = transform.right;
        right.y = 0; // Y ���� ����
        right.Normalize(); // ���� ���� ����ȭ

        float moveX = Input.GetAxis("Horizontal") * moveSpeed * Time.deltaTime; // A, D �Է�
        float moveZ = Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime; // W, S �Է�
        Vector3 move = right * moveX + forward * moveZ;

        transform.position += move;

        // ���콺 �ٷ� ī�޶� ��ǥ�� ���� �յ� �̵� (����/�ܾƿ�)
        float scroll = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        //float scroll = Input.mouseScrollDelta.y * zoomSpeed;
        Vector3 forwardMove = transform.forward * scroll;

        // �ּ�/�ִ� �Ÿ� ����
        float distanceFromOrigin = Vector3.Distance(transform.position + forwardMove, Vector3.zero);
        if (distanceFromOrigin >= minZoomDistance && distanceFromOrigin <= maxZoomDistance)
        {
            transform.position += forwardMove;
        }

        //ī�޶��� ���� h�� ����
        transform.position = new Vector3(transform.position.x, h, transform.position.z);
    }
}
