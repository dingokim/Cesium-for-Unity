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

    float topY = -600;
    float bottomY = -1000;

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
        //쿠키커터 인스턴스 생성
        cookieCutterInstance = new CookieCutter();
        cookieCutterInstance.polygonManager = this.gameObject;

        if (state != polygonState.complete)
        {
            Debug.Log("polygon이 온전하지 않아 Cut을 수행할 수 없습니다.");
            return;
        }

        Vector3[] polygon = polygonVertices.ToArray();

        string verts = "";
        foreach (Vector3 vert in polygon) 
        {
            verts += $"{vert} ";
        }
        Debug.Log("verts :" + verts);

        foreach (GameObject target in targets)
        {
            //Debug.Log(target.name + " CookieCut 호출");
            GameObject targetPiece = cookieCutterInstance.CookieCut(target, polygon);

            if (ParentTransform != null)
            {
                targetPiece.transform.SetParent(ParentTransform);
            }
        }

        ResetPolygon();

        //쿠키커터 인스턴스 삭제 후 반환
        cookieCutterInstance = null;
        GC.Collect();
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

                //시작점의 위치에 sphere를 생성합니다
                firstSphere = lastSphere = Instantiate(spherePrefab, hit.point, Quaternion.identity);
                lastSphere.transform.parent = transform;
                lastSphere.name = "Polygon Vertex #" + polygonVertices.Count.ToString();
            }
            else if (state == polygonState.creating)
            {

                //새로 만들어진 선분이 이전 선분과 교차된다면 폴리곤을 이룰 수 없으므로 아무것도 수행하지않습니다.
                for (int i = 0; i < polygonVertices.Count - 1; i++)
                {
                    Vector3 v1 = polygonVertices[i];
                    Vector3 v2 = polygonVertices[i + 1];
                    if (DoLinesIntersect(v1, v2, lastVertex, hit.point))
                    {
                        Debug.Log("해당 좌표는 폴리곤을 만들 수 없습니다. 다른 좌표를 선택하세요.");
                        return;
                    }
                }

                //polygonVertices와 lastVertex를 갱신합니다.
                polygonVertices.Add(hit.point);
                lastVertex = hit.point;

                //Sphere를 생성합니다.
                lastSphere = Instantiate(spherePrefab, hit.point, Quaternion.identity);
                lastSphere.transform.parent = transform;
                lastSphere.name = "Polygon Vertex #" + polygonVertices.Count.ToString();

                //면을 생성합니다.
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
        //정점의 개수가 3개 이상인지 확인합니다.
        if (polygonVertices.Count < 3)
        {
            Debug.Log("폴리곤의 정점은 최소 3개입니다.");
            return;
        }

        //새로 만들어진 선분이 이전 선분과 교차된다면 폴리곤을 이룰 수 없으므로 아무것도 수행하지않습니다.
        for (int i = 0; i < polygonVertices.Count - 1; i++)
        {
            Vector3 v1 = polygonVertices[i];
            Vector3 v2 = polygonVertices[i + 1];
            if (DoLinesIntersect(v1, v2, lastVertex, firstVertex))
            {
                Debug.Log("해당 좌표는 폴리곤을 만들 수 없습니다. 다른 좌표를 선택하세요.");
                return;
            }
        }

        //state를 갱신합니다.
        state = polygonState.complete;

        //Area 오브젝트를 생성합니다.
        GenerateArea();

        //Area와 접촉중인 오브젝트들을 리스트에 담습니다.
        targets = GetContactedObjects();

        Debug.Log("Polygon Area가 정상적으로 생성되었습니다.");
        return;
    }

    public void ResetPolygon()
    {
        //변수를 갱신합니다.
        polygonVertices.Clear();
        state = polygonState.empty;


        //vertex 오브젝트를 지웁니다.
        foreach (Transform child in gameObject.transform)
        {
            Destroy(child.gameObject);
        }


        //Area를 지웁니다.
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
                // Unity 좌표를 ECEF 좌표로 변환
                double3 doublePosition = new double3(hitPoint.x, hitPoint.y, hitPoint.z);
                double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(doublePosition);

                // ECEF 좌표를 위경도 및 고도로 변환
                double latitude, longitude, height;
                ECEFToLatLonHeight(ecef, out latitude, out longitude, out height);


                // 좌표의 위경도 값을 출력
                Debug.Log("Cesium Coordinates: Lat: " + latitude + ", Lon: " + longitude + ", Height: " + height);


                // 위경도값은 TCP 소켓으로 송신하고 polygon 정보를 responseData에 저장합니다.
                SendCoordinates(latitude, longitude);

                // polygon을 정상적으로 수신했다면 
                if (responseData != null)
                {
                    //responseData를 해석하여 Vector3[] polygon을 갱신합니다.
                    polygonVertices = ParsePolygon(responseData);


                    //폴리곤 영역에 패딩을 씌웁니다.
                    polygonVertices = OffsetPolygon(polygonVertices, paddingDistance);


                    //state를 갱신합니다.
                    state = polygonState.complete;

                    //Area를 생성합니다.
                    GenerateArea();

                    //Area와 접촉중인 오브젝트들을 리스트에 담습니다.
                    targets = GetContactedObjects();

                    Debug.Log("Polygon Area가 정상적으로 생성되었습니다.");
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

        // WGS84 기준 지구 반경과 편평률
        double a = 6378137.0; // 지구의 적도 반경
        double e = 8.1819190842622e-2; // 편평률

        double b = Mathf.Sqrt((float)(a * a * (1 - e * e)));
        double ep = Mathf.Sqrt((float)((a * a - b * b) / (b * b)));
        double p = Mathf.Sqrt((float)(x * x + y * y));
        double th = Mathf.Atan2((float)(z * a), (float)(p * b));

        longitude = Mathf.Atan2((float)y, (float)x);
        latitude = Mathf.Atan2((float)(z + ep * ep * b * Mathf.Sin((float)th) * Mathf.Sin((float)th) * Mathf.Sin((float)th)),
                                (float)(p - e * e * a * Mathf.Cos((float)th) * Mathf.Cos((float)th) * Mathf.Cos((float)th)));
        double N = a / Mathf.Sqrt((float)(1 - e * e * Mathf.Sin((float)latitude) * Mathf.Sin((float)latitude)));
        height = p / Mathf.Cos((float)latitude) - N;

        // 라디안 값을 도 단위로 변환
        latitude = latitude * (180.0 / Mathf.PI);
        longitude = longitude * (180.0 / Mathf.PI);
    }

    void LatLonHeightToECEF(double latitude, double longitude, double height, out double3 ecef)
    {
        // WGS84 기준 지구 반경과 편평률
        double a = 6378137.0; // 지구의 적도 반경
        double e = 8.1819190842622e-2; // 편평률

        // 도 단위를 라디안 단위로 변환
        latitude = latitude * (Mathf.PI / 180.0);
        longitude = longitude * (Mathf.PI / 180.0);

        double N = a / Mathf.Sqrt((float)(1 - e * e * Mathf.Sin((float)latitude) * Mathf.Sin((float)latitude)));

        double x = (N + height) * Mathf.Cos((float)latitude) * Mathf.Cos((float)longitude);
        double y = (N + height) * Mathf.Cos((float)latitude) * Mathf.Sin((float)longitude);
        double z = ((1 - e * e) * N + height) * Mathf.Sin((float)latitude);

        ecef = new double3(x, y, z);
    }

    // 위경도를 입력받아 서버로 전송하고 응답을 받는 메서드
    public void SendCoordinates(double latitude, double longitude)
    {
        try
        {
            using (TcpClient client = new TcpClient(serverIP, serverPort))
            {
                NetworkStream stream = client.GetStream();

                // 위경도를 서버에 보낼 데이터로 포맷팅
                string message = $"{latitude},{longitude}";
                byte[] data = Encoding.UTF8.GetBytes(message);

                // 서버로 데이터 전송
                stream.Write(data, 0, data.Length);
                Debug.Log($"Sent: {message}");

                // 서버로부터 응답 수신
                data = new byte[2048];
                int bytes = stream.Read(data, 0, data.Length);
                responseData = Encoding.UTF8.GetString(data, 0, bytes);
                Debug.Log($"Received: {responseData}");

                // 서버 응답 처리
                HandleServerResponse(responseData);
                //Thread.Sleep(500);
            }
        }
        catch (SocketException e)
        {
            Debug.LogError($"SocketException: {e}");
        }
    }

    // 서버의 응답을 처리하는 메서드
    private void HandleServerResponse(string response)
    {
        if (response == "영역내에 해당하는 좌표가 없습니다.")
        {
            Debug.Log("해당 좌표를 포함하는 폴리곤을 찾을 수 없습니다.");
            response = null;
        }
        else
        {
            Debug.Log($"폴리곤 정보: {response}");
            // 여기서 받은 폴리곤 정보를 활용할 수 있습니다.
        }
    }


    List<Vector3> ParsePolygon(string response)
    {
        // "MULTIPOLYGON(((" 부분을 제거하고, 마지막 ")))"도 제거
        string coords = response.Replace("MULTIPOLYGON(((", "").Replace(")))", "");

        // 좌표 문자열을 분리하여 배열로 변환
        string[] pairs = coords.Split(',');

        List<Vector3> polygonPoints = new List<Vector3>();

        Debug.Log("polygon vertex 수 : " + pairs.Length + "개");

        for (int i = 0; i < pairs.Length; i++)
        {
            // 각 좌표쌍을 공백으로 분리javascript:page(110622)
            string[] xy = pairs[i].Trim().Split(' ');

            double x = double.Parse(xy[0], CultureInfo.InvariantCulture);
            double z = double.Parse(xy[1], CultureInfo.InvariantCulture);

            // 좌표 변환 (EPSG:5186 -> WGS84)
            double latitude, longitude;
            ConvertEPSG5186ToWGS84(x, z, out latitude, out longitude);

            // WGS84 좌표를 Unity 좌표로 변환
            Vector3 unityPoint = ConvertWGS84ToUnity(latitude, longitude);

            Debug.Log($"{i}번째 polygon : {unityPoint}");

            // 마지막 vertex는 저장하지 않음. (첫 vertex와 중복)
            if (i != pairs.Length - 1)
            {
                polygonPoints.Add(unityPoint);
            }
        }

        return polygonPoints;
    }


    // 잘라낼 범위에 해당하는 입체기둥을 생성
    void GenerateArea()
    {
        // 윗면과 아랫면의 꼭지점들 생성
        List<Vector3> topVertices = new List<Vector3>();
        List<Vector3> bottomVertices = new List<Vector3>();

        int vc = 0;
        foreach (Vector3 vertex in polygonVertices)
        {
            vc++;
            topVertices.Add(new Vector3(vertex.x, topY, vertex.z));
            bottomVertices.Add(new Vector3(vertex.x, bottomY, vertex.z));
        }

        // Mesh 생성
        Mesh mesh = new Mesh();

        // 꼭지점들 리스트
        List<Vector3> vertices = new List<Vector3>();
        vertices.AddRange(topVertices);      // 윗면
        vertices.AddRange(bottomVertices);   // 아랫면

        // 삼각형들 리스트
        List<int> triangles = new List<int>();

        // 윗면 삼각형들 생성
        for (int i = 1; i < polygonVertices.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }

        // 아랫면 삼각형들 생성 (반대 방향으로)
        int offset = polygonVertices.Count;
        for (int i = 1; i < polygonVertices.Count - 1; i++)
        {
            triangles.Add(offset);
            triangles.Add(offset + i + 1);
            triangles.Add(offset + i);
        }

        // 옆면 삼각형들 생성
        for (int i = 0; i < polygonVertices.Count; i++)
        {
            int next = (i + 1) % polygonVertices.Count;

            // 첫 번째 삼각형
            triangles.Add(i);
            triangles.Add(next);
            triangles.Add(offset + next);

            // 두 번째 삼각형
            triangles.Add(i);
            triangles.Add(offset + next);
            triangles.Add(offset + i);
        }

        // Mesh 설정
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        // MeshFilter와 MeshRenderer 컴포넌트 추가
        MeshFilter meshFilter;
        if (gameObject.GetComponent<MeshFilter>() == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        else meshFilter = gameObject.GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        MeshRenderer meshRenderer;
        if (gameObject.GetComponent<MeshRenderer>() == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        else meshRenderer = gameObject.GetComponent<MeshRenderer>();

        // public 변수로 선언된 머터리얼을 적용
        if (areaMaterial != null)
        {
            meshRenderer.material = areaMaterial;
        }
        else
        {
            // 만약 머터리얼이 할당되지 않았다면 기본 머터리얼 적용
            meshRenderer.material = new Material(Shader.Find("Standard"));
        }

        // MeshCollider 컴포넌트 추가
        MeshCollider meshCollider;
        if (gameObject.GetComponent<MeshCollider>() == null) meshCollider = gameObject.AddComponent<MeshCollider>();
        else meshCollider = gameObject.GetComponent<MeshCollider>();

        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = true;
    }

    public List<GameObject> GetContactedObjects()
    {
        // MeshCollider의 Bounds를 가져옴 (다각기둥의 경계)
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            Debug.LogWarning("MeshCollider가 없습니다.");
            return new List<GameObject>();
        }

        // Bounds를 기준으로 Collider들을 검색
        Bounds bounds = meshCollider.bounds;
        Collider[] colliders = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity);

        // 접촉 중인 오브젝트들을 리스트에 저장
        List<GameObject> contactObjects = new List<GameObject>();
        foreach (Collider collider in colliders)
        {
            // 자기 자신은 제외
            if (collider.gameObject != gameObject)
            {
                contactObjects.Add(collider.gameObject);
            }
        }

        return contactObjects;
    }


    public void ConvertEPSG5186ToWGS84(double x, double y, out double latitude, out double longitude)
    {
        // 변환할 EPSG:5186 좌표 (x, y)
        double[] point = new double[] { x, y };

        string coordinateConvertString5186 = "+proj=tmerc +lat_0=38 +lon_0=127 +k=1 +x_0=200000 +y_0=600000 +ellps=GRS80 +units=m +no_defs";
        string coordinateConvertStringWGS = "+proj=longlat +ellps=WGS84 +datum=WGS84 +no_defs";
        try
        {
            // 좌표 변환 수행
            DotSpatial.Projections.Reproject.ReprojectPoints(
                point,
                new double[] { 0 },
                DotSpatial.Projections.ProjectionInfo.FromProj4String(coordinateConvertString5186),
                DotSpatial.Projections.ProjectionInfo.FromProj4String(coordinateConvertStringWGS),
                0,
                1
            );

            // 변환된 좌표 확인
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
        // 위경도 및 고도를 ECEF 좌표로 변환
        double3 d3;
        LatLonHeightToECEF(latitude, longitude, height, out d3);

        // ECEF 좌표를 Unity 좌표로 변환
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

            // 이전 변과 다음 변의 벡터를 구함 (y = 0이므로 xz 평면에서만 고려)
            Vector3 edge1 = pCurr - pPrev;
            Vector3 edge2 = pNext - pCurr;

            // 각 벡터에 대해 노멀 벡터를 계산 (xz 평면에 수직하게)
            Vector3 normal1 = new Vector3(-edge1.z, 0, edge1.x).normalized;
            Vector3 normal2 = new Vector3(-edge2.z, 0, edge2.x).normalized;

            // 두 노멀 벡터의 평균 계산
            Vector3 normalAvg = (normal1 + normal2).normalized;

            // 평균 노멀 벡터 방향으로 점을 이동
            Vector3 offsetPoint = pCurr + d * normalAvg;
            paddedPolygon.Add(offsetPoint);
        }

        return paddedPolygon;
    }

    public void SetPolygon(List<Vector3> tilePolygonVertices) 
    {
        //폴리곤 데이터 초기화
        ResetPolygon();

        //매개변수로 받은 폴리곤 데이터를 polygonVertices에 할당
        polygonVertices = tilePolygonVertices;

        //폴리곤 상태를 Complete로 변경
        CompletePolygon();
    }
}