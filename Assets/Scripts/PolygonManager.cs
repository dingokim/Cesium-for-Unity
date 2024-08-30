using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using UnityEngine;
using Unity.Mathematics;
using System.Text;
using UnityEngine.Networking;
using System;
using System.Net.Sockets;
using System.Globalization;
using System.Threading;



public class PolygonManager : MonoBehaviour
{
    [SerializeField]
    private polygonState state;

    public GameObject spherePrefab;
    private Camera cam;

    private List<Vector3> polygonVertices;

    float topY;
    float bottomY;

    public Material areaMaterial;

    private List<GameObject> targets;

    private GameObject firstSphere;
    private GameObject lastSphere;
    private Vector3 firstVertex;
    private Vector3 lastVertex;
    
    public CesiumGeoreference georeference;

    public string serverIP = "129.254.193.41";
    public int serverPort = 65432;

    string responseData = null;

    public float paddingDistance = 5;

    public Transform ParentTransform;

    //CookieCutter_light cookieCutterInstance;
    CookieCutter cookieCutterInstance;
    private enum polygonState
    {
        empty,
        creating,
        complete
    }

    // Start is called before the first frame update
    void Start()
    {
        cam = Camera.main;
        polygonVertices = new List<Vector3>();

        ResetPolygon();

        //cookieCutterInstance = new CookieCutter_light();
        cookieCutterInstance = new CookieCutter();
        cookieCutterInstance.polygonManager = this.gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(2)) SphereCreation();

        if (Input.GetKey(KeyCode.P) && Input.GetMouseButtonDown(0))
        {
            ResetPolygon();
            GetPolygonFromDB();
        }

        
    }

    public void CutCookies() 
    {
        if (state != polygonState.complete) 
        {
            Debug.Log("polygon�� �������� �ʾ� Cut�� ������ �� �����ϴ�.");
            return;
        }

        Vector3[] polygon = polygonVertices.ToArray();

        foreach (GameObject target in targets)
        {
            Debug.Log(target.name + " CookieCut ȣ��");
            GameObject targetPiece = cookieCutterInstance.CookieCut(target,polygon);

            if (ParentTransform != null)
            {
                targetPiece.transform.SetParent(ParentTransform);
            }
        }

        ResetPolygon();
    }


    private void SphereCreation()
    {

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            topY = hit.point.y + 500;
            bottomY = hit.point.y - 500;

            if (state == polygonState.empty)
            {
                state = polygonState.creating;

                polygonVertices.Add(hit.point);
                firstVertex = hit.point;
                lastVertex = hit.point;

                //�������� ��ġ�� sphere�� �����մϴ�
                firstSphere = lastSphere = Instantiate(spherePrefab, hit.point, Quaternion.identity);
                lastSphere.transform.parent = transform;
                lastSphere.name = "Polygon Vertex #" + polygonVertices.Count.ToString();
            }
            else if (state == polygonState.creating)
            {

                //���� ������� ������ ���� ���а� �����ȴٸ� �������� �̷� �� �����Ƿ� �ƹ��͵� ���������ʽ��ϴ�.
                for (int i = 0; i < polygonVertices.Count - 1; i++)
                {
                    Vector3 v1 = polygonVertices[i];
                    Vector3 v2 = polygonVertices[i + 1];
                    if (DoLinesIntersect(v1, v2, lastVertex, hit.point))
                    {
                        Debug.Log("�ش� ��ǥ�� �������� ���� �� �����ϴ�. �ٸ� ��ǥ�� �����ϼ���.");
                        return;
                    }
                }

                //polygonVertices�� lastVertex�� �����մϴ�.
                polygonVertices.Add(hit.point);
                lastVertex = hit.point;

                //Sphere�� �����մϴ�.
                lastSphere = Instantiate(spherePrefab, hit.point, Quaternion.identity);
                lastSphere.transform.parent = transform;
                lastSphere.name = "Polygon Vertex #" + polygonVertices.Count.ToString();

                //���� �����մϴ�.
            }
            else
            {
                return;
            }
        }

    }

    private bool DoLinesIntersect(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        float det(float a, float b, float c, float d) => a * d - b * c;

        float x1 = v1.x, z1 = v1.z;
        float x2 = v2.x, z2 = v2.z;
        float x3 = v3.x, z3 = v3.z;
        float x4 = v4.x, z4 = v4.z;

        float det1 = det(x2 - x1, x3 - x4, z2 - z1, z3 - z4);
        float det2 = det(x3 - x1, x3 - x4, z3 - z1, z3 - z4);
        float det3 = det(x2 - x1, x3 - x4, z2 - z1, z3 - z4);
        float det4 = det(x1 - x3, x2 - x3, z1 - z3, z2 - z3);

        if (det1 == 0)
            return false; // Lines are parallel

        float t1 = det2 / det1;
        float t2 = det4 / det3;

        return (0 < t1 && t1 < 1) && (0 < t2 && t2 < 1);
    }

    public void CompletePolygon()
    {
        //������ ������ 3�� �̻����� Ȯ���մϴ�.
        if (polygonVertices.Count < 3)
        {
            Debug.Log("�������� ������ �ּ� 3���Դϴ�.");
            return;
        }

        //���� ������� ������ ���� ���а� �����ȴٸ� �������� �̷� �� �����Ƿ� �ƹ��͵� ���������ʽ��ϴ�.
        for (int i = 0; i < polygonVertices.Count - 1; i++)
        {
            Vector3 v1 = polygonVertices[i];
            Vector3 v2 = polygonVertices[i + 1];
            if (DoLinesIntersect(v1, v2, lastVertex, firstVertex))
            {
                Debug.Log("�ش� ��ǥ�� �������� ���� �� �����ϴ�. �ٸ� ��ǥ�� �����ϼ���.");
                return;
            }
        }

        //state�� �����մϴ�.
        state = polygonState.complete;

        //Area�� �����մϴ�.
        GenerateArea();

        //Area�� �������� ������Ʈ���� ����Ʈ�� ����ϴ�.
        targets = GetContactedObjects();

        Debug.Log("Polygon Area�� ���������� �����Ǿ����ϴ�.");
        return;
    }

    public void ResetPolygon()
    {
        //������ �����մϴ�.
        polygonVertices.Clear();
        state = polygonState.empty;


        //vertex ������Ʈ�� ����ϴ�.
        foreach (Transform child in gameObject.transform)
        {
            Destroy(child.gameObject);
        }


        //Area�� ����ϴ�.
        MeshFilter myMeshFilter = gameObject.GetComponent<MeshFilter>();
        if (myMeshFilter != null) Destroy(myMeshFilter);
        MeshRenderer myMeshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (myMeshRenderer != null) Destroy(myMeshRenderer);
        MeshCollider myMeshCollider = gameObject.GetComponent<MeshCollider>();
        if (myMeshCollider != null) Destroy(myMeshCollider);
    }


    public void GetPolygonFromDB()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            topY = hit.point.y + 500;
            bottomY = hit.point.y - 500;

            Vector3 hitPoint = hit.point;
            Debug.Log("Unity Coordinates: " + hitPoint);

            if (georeference != null)
            {
                // Unity ��ǥ�� ECEF ��ǥ�� ��ȯ
                double3 doublePosition = new double3(hitPoint.x, hitPoint.y, hitPoint.z);
                double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(doublePosition);

                // ECEF ��ǥ�� ���浵 �� ���� ��ȯ
                double latitude, longitude, height;
                ECEFToLatLonHeight(ecef, out latitude, out longitude, out height);


                // ��ǥ�� ���浵 ���� ���
                Debug.Log("Cesium Coordinates: Lat: " + latitude + ", Lon: " + longitude + ", Height: " + height);


                // ���浵���� TCP �������� �۽��ϰ� polygon ������ responseData�� �����մϴ�.
                SendCoordinates(latitude, longitude);

                // polygon�� ���������� �����ߴٸ� 
                if (responseData != null)
                {
                    //responseData�� �ؼ��Ͽ� Vector3[] polygon�� �����մϴ�.
                    polygonVertices = ParsePolygon(responseData);


                    //������ ������ �е��� ����ϴ�.
                    polygonVertices = OffsetPolygon(polygonVertices, paddingDistance);


                    //state�� �����մϴ�.
                    state = polygonState.complete;

                    //Area�� �����մϴ�.
                    GenerateArea();

                    //Area�� �������� ������Ʈ���� ����Ʈ�� ����ϴ�.
                    targets = GetContactedObjects();

                    Debug.Log("Polygon Area�� ���������� �����Ǿ����ϴ�.");
                }
            }
            else 
            {
                Debug.LogError("CesiumGeoreference not found.");
            }
        }
    }

    void ECEFToLatLonHeight(double3 ecef, out double latitude, out double longitude, out double height)
    {
        double x = ecef.x;
        double y = ecef.y;
        double z = ecef.z;

        // WGS84 ���� ���� �ݰ�� �����
        double a = 6378137.0; // ������ ���� �ݰ�
        double e = 8.1819190842622e-2; // �����

        double b = Mathf.Sqrt((float)(a * a * (1 - e * e)));
        double ep = Mathf.Sqrt((float)((a * a - b * b) / (b * b)));
        double p = Mathf.Sqrt((float)(x * x + y * y));
        double th = Mathf.Atan2((float)(z * a), (float)(p * b));

        longitude = Mathf.Atan2((float)y, (float)x);
        latitude = Mathf.Atan2((float)(z + ep * ep * b * Mathf.Sin((float)th) * Mathf.Sin((float)th) * Mathf.Sin((float)th)),
                                (float)(p - e * e * a * Mathf.Cos((float)th) * Mathf.Cos((float)th) * Mathf.Cos((float)th)));
        double N = a / Mathf.Sqrt((float)(1 - e * e * Mathf.Sin((float)latitude) * Mathf.Sin((float)latitude)));
        height = p / Mathf.Cos((float)latitude) - N;

        // ���� ���� �� ������ ��ȯ
        latitude = latitude * (180.0 / Mathf.PI);
        longitude = longitude * (180.0 / Mathf.PI);
    }

    void LatLonHeightToECEF(double latitude, double longitude, double height, out double3 ecef)
    {
        // WGS84 ���� ���� �ݰ�� �����
        double a = 6378137.0; // ������ ���� �ݰ�
        double e = 8.1819190842622e-2; // �����

        // �� ������ ���� ������ ��ȯ
        latitude = latitude * (Mathf.PI / 180.0);
        longitude = longitude * (Mathf.PI / 180.0);

        double N = a / Mathf.Sqrt((float)(1 - e * e * Mathf.Sin((float)latitude) * Mathf.Sin((float)latitude)));

        double x = (N + height) * Mathf.Cos((float)latitude) * Mathf.Cos((float)longitude);
        double y = (N + height) * Mathf.Cos((float)latitude) * Mathf.Sin((float)longitude);
        double z = ((1 - e * e) * N + height) * Mathf.Sin((float)latitude);

        ecef = new double3(x, y, z);
    }

    // ���浵�� �Է¹޾� ������ �����ϰ� ������ �޴� �޼���
    public void SendCoordinates(double latitude, double longitude)
    {
        try
        {
            using (TcpClient client = new TcpClient(serverIP, serverPort))
            {
                NetworkStream stream = client.GetStream();

                // ���浵�� ������ ���� �����ͷ� ������
                string message = $"{latitude},{longitude}";
                byte[] data = Encoding.UTF8.GetBytes(message);

                // ������ ������ ����
                stream.Write(data, 0, data.Length);
                Debug.Log($"Sent: {message}");

                // �����κ��� ���� ����
                data = new byte[2048];
                int bytes = stream.Read(data, 0, data.Length);
                responseData = Encoding.UTF8.GetString(data, 0, bytes);
                Debug.Log($"Received: {responseData}");

                // ���� ���� ó��
                HandleServerResponse(responseData);
                //Thread.Sleep(500);
            }
        }
        catch (SocketException e)
        {
            Debug.LogError($"SocketException: {e}");
        }
    }

    // ������ ������ ó���ϴ� �޼���
    private void HandleServerResponse(string response)
    {
        if (response == "�������� �ش��ϴ� ��ǥ�� �����ϴ�.")
        {
            Debug.Log("�ش� ��ǥ�� �����ϴ� �������� ã�� �� �����ϴ�.");
            response = null;
        }
        else
        {
            Debug.Log($"������ ����: {response}");
            // ���⼭ ���� ������ ������ Ȱ���� �� �ֽ��ϴ�.
        }
    }


    List<Vector3> ParsePolygon(string response)
    {
        // "MULTIPOLYGON(((" �κ��� �����ϰ�, ������ ")))"�� ����
        string coords = response.Replace("MULTIPOLYGON(((", "").Replace(")))", "");

        // ��ǥ ���ڿ��� �и��Ͽ� �迭�� ��ȯ
        string[] pairs = coords.Split(',');

        List<Vector3> polygonPoints = new List<Vector3>();

        Debug.Log("polygon vertex �� : " + pairs.Length + "��");

        for (int i = 0; i < pairs.Length; i++)
        {
            // �� ��ǥ���� �������� �и�javascript:page(110622)
            string[] xy = pairs[i].Trim().Split(' ');

            double x = double.Parse(xy[0], CultureInfo.InvariantCulture);
            double z = double.Parse(xy[1], CultureInfo.InvariantCulture);

            // ��ǥ ��ȯ (EPSG:5186 -> WGS84)
            double latitude, longitude;
            ConvertEPSG5186ToWGS84(x,z,out latitude,out longitude);

            // WGS84 ��ǥ�� Unity ��ǥ�� ��ȯ
            Vector3 unityPoint = ConvertWGS84ToUnity(latitude,longitude);

            Debug.Log($"{i}��° polygon : {unityPoint}");

            // ������ vertex�� �������� ����. (ù vertex�� �ߺ�)
            if (i != pairs.Length - 1)
            {
                polygonPoints.Add(unityPoint);
            }
        }

        return polygonPoints;
    }


    void GenerateArea()
    {
        // ����� �Ʒ����� �������� ����
        List<Vector3> topVertices = new List<Vector3>();
        List<Vector3> bottomVertices = new List<Vector3>();

        foreach (Vector3 vertex in polygonVertices)
        {
            topVertices.Add(new Vector3(vertex.x, topY, vertex.z));
            bottomVertices.Add(new Vector3(vertex.x, bottomY, vertex.z));
        }

        // Mesh ����
        Mesh mesh = new Mesh();

        // �������� ����Ʈ
        List<Vector3> vertices = new List<Vector3>();
        vertices.AddRange(topVertices);      // ����
        vertices.AddRange(bottomVertices);   // �Ʒ���

        // �ﰢ���� ����Ʈ
        List<int> triangles = new List<int>();

        // ���� �ﰢ���� ����
        for (int i = 1; i < polygonVertices.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }

        // �Ʒ��� �ﰢ���� ���� (�ݴ� ��������)
        int offset = polygonVertices.Count;
        for (int i = 1; i < polygonVertices.Count - 1; i++)
        {
            triangles.Add(offset);
            triangles.Add(offset + i + 1);
            triangles.Add(offset + i);
        }

        // ���� �ﰢ���� ����
        for (int i = 0; i < polygonVertices.Count; i++)
        {
            int next = (i + 1) % polygonVertices.Count;

            // ù ��° �ﰢ��
            triangles.Add(i);
            triangles.Add(next);
            triangles.Add(offset + next);

            // �� ��° �ﰢ��
            triangles.Add(i);
            triangles.Add(offset + next);
            triangles.Add(offset + i);
        }

        // Mesh ����
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        // MeshFilter�� MeshRenderer ������Ʈ �߰�
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        // public ������ ����� ���͸����� ����
        if (areaMaterial != null)
        {
            meshRenderer.material = areaMaterial;
        }
        else
        {
            // ���� ���͸����� �Ҵ���� �ʾҴٸ� �⺻ ���͸��� ����
            meshRenderer.material = new Material(Shader.Find("Standard"));
        }

        // MeshCollider ������Ʈ �߰�
        MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = true;
    }

    public List<GameObject> GetContactedObjects()
    {
        // MeshCollider�� Bounds�� ������ (�ٰ������ ���)
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            Debug.LogWarning("MeshCollider�� �����ϴ�.");
            return new List<GameObject>();
        }

        // Bounds�� �������� Collider���� �˻�
        Bounds bounds = meshCollider.bounds;
        Collider[] colliders = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity);

        // ���� ���� ������Ʈ���� ����Ʈ�� ����
        List<GameObject> contactObjects = new List<GameObject>();
        foreach (Collider collider in colliders)
        {
            // �ڱ� �ڽ��� ����
            if (collider.gameObject != gameObject)
            {
                contactObjects.Add(collider.gameObject);
            }
        }

        return contactObjects;
    }


    public void ConvertEPSG5186ToWGS84(double x, double y, out double latitude, out double longitude)
    {
        // ��ȯ�� EPSG:5186 ��ǥ (x, y)
        double[] point = new double[] { x, y };

        string coordinateConvertString5186 = "+proj=tmerc +lat_0=38 +lon_0=127 +k=1 +x_0=200000 +y_0=600000 +ellps=GRS80 +units=m +no_defs";
        string coordinateConvertStringWGS = "+proj=longlat +ellps=WGS84 +datum=WGS84 +no_defs";
        try
        { 
            // ��ǥ ��ȯ ����
            DotSpatial.Projections.Reproject.ReprojectPoints(
                point,
                new double[] { 0 },
                DotSpatial.Projections.ProjectionInfo.FromProj4String(coordinateConvertString5186),
                DotSpatial.Projections.ProjectionInfo.FromProj4String(coordinateConvertStringWGS),
                0,
                1
            );

            // ��ȯ�� ��ǥ Ȯ��
            //Debug.Log($"Transformed Coordinates: Latitude={point[1]}, Longitude={point[0]}");

            latitude = point[1];
            longitude = point[0];
        }
        catch (InvalidOperationException ex)
        {
            Debug.LogError($"Transformation failed with InvalidOperationException: {ex.Message}");
            longitude = 0;
            latitude = 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Unexpected error during transformation: {ex.Message}");
            longitude = 0;
            latitude = 0;
        }
    }

    public Vector3 ConvertWGS84ToUnity(double latitude, double longitude, double height = 0)
    {
        // ���浵 �� ���� ECEF ��ǥ�� ��ȯ
        double3 d3;
        LatLonHeightToECEF(latitude, longitude, height, out d3);

        // ECEF ��ǥ�� Unity ��ǥ�� ��ȯ
        double3 doublePosition = georeference.TransformEarthCenteredEarthFixedPositionToUnity(d3);
        return new Vector3((float)doublePosition.x, (float)doublePosition.y, (float)doublePosition.z);
    }


    public List<Vector3> OffsetPolygon(List<Vector3> polygon, float d)
    {
        List<Vector3> paddedPolygon = new List<Vector3>();
        int n = polygon.Count;

        for (int i = 0; i < n; i++)
        {
            Vector3 pPrev = polygon[i == 0 ? n - 1 : i - 1];
            Vector3 pCurr = polygon[i];
            Vector3 pNext = polygon[(i + 1) % n];

            // ���� ���� ���� ���� ���͸� ���� (y = 0�̹Ƿ� xz ��鿡���� ���)
            Vector3 edge1 = pCurr - pPrev;
            Vector3 edge2 = pNext - pCurr;

            // �� ���Ϳ� ���� ��� ���͸� ��� (xz ��鿡 �����ϰ�)
            Vector3 normal1 = new Vector3(-edge1.z, 0, edge1.x).normalized;
            Vector3 normal2 = new Vector3(-edge2.z, 0, edge2.x).normalized;

            // �� ��� ������ ��� ���
            Vector3 normalAvg = (normal1 + normal2).normalized;

            // ��� ��� ���� �������� ���� �̵�
            Vector3 offsetPoint = pCurr + d * normalAvg;
            paddedPolygon.Add(offsetPoint);
        }

        return paddedPolygon;
    }
}
