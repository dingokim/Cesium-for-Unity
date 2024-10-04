using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

// 마우스가 위치한 곳의 Surface를 검출하는 기능을 담은 class.
// 클릭한 지점의 삼각형을 시작으로 주변 삼각형들을 찾고, normalVector간의 각도차이가 일정 이하이면 같은 cluster로 묶는 과정을 반복하여  surface를 검출한다.
// 3D 모델의 모서리가 작은 조각으로 뭉툭하게 이어져있는 경우에, Surface를 잘 분리하지 못한다. 그래서 현재는 사용하지 않음.
public class SurfaceFinder : MonoBehaviour
{
    public Material newMaterial;
    public GameObject redDotPrefab; // 미리 준비해둔 redDot 프리팹
    public float degree = 30.0f; // 각도 차이 허용값을 public 변수로 설정
    public float epsilon = 0.001f; // 좌표 비교 시 사용하는 오차 허용값

    private Dictionary<int, HashSet<int>> vertexMapping; // 동일한 위치를 가지는 버텍스 매핑
    private Mesh selectedMesh;
    private Transform selectedObjectTransform;

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            SelectObjectAndSurface();
        }
    }

    void SelectObjectAndSurface()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            MeshCollider meshCollider = hit.collider as MeshCollider;
            if (meshCollider != null && meshCollider.sharedMesh != null)
            {
                selectedMesh = meshCollider.sharedMesh;
                selectedObjectTransform = hit.collider.transform;

                // 동일한 위치를 가지는 버텍스 매핑 생성
                CreateVertexMapping();

                int triangleIndex = hit.triangleIndex * 3;

                if (triangleIndex < 0 || triangleIndex >= selectedMesh.triangles.Length) return;


                // Surface를 찾고, Edge를 찾음
                List<int> connectedTriangles = FindConnectedTriangles(triangleIndex);
                List<List<int>> edgeVertices = FindEdgeVertices(connectedTriangles);

                // Edge 정보 로그 출력
                int edgeNumber = 1;
                foreach (var edge in edgeVertices)
                {
                    Debug.Log($"Edge {edgeNumber}: " + string.Join(", ", edge.Select(v => "Vertex" + v)));
                    edgeNumber++;
                }

                // 새로운 Surface에 Material 적용
                ApplyMaterialToTriangles(connectedTriangles);
            }
        }
    }

    void CreateVertexMapping()
    {
        vertexMapping = new Dictionary<int, HashSet<int>>();
        Vector3[] vertices = selectedMesh.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!vertexMapping.ContainsKey(i))
            {
                vertexMapping[i] = new HashSet<int> { i };
            }

            for (int j = i + 1; j < vertices.Length; j++)
            {
                if (Vector3.Distance(vertices[i], vertices[j]) < epsilon)
                {
                    vertexMapping[i].Add(j);

                    if (!vertexMapping.ContainsKey(j))
                    {
                        vertexMapping[j] = new HashSet<int> { j };
                    }
                    vertexMapping[j].Add(i);
                }
            }
        }
    }


    List<int> FindConnectedTriangles(int startTriangleIndex)
    {
        List<int> connectedTriangles = new List<int>();
        HashSet<int> visitedTriangles = new HashSet<int>();
        Queue<int> trianglesToCheck = new Queue<int>();

        Vector3[] vertices = selectedMesh.vertices;
        int[] triangles = selectedMesh.triangles;

        trianglesToCheck.Enqueue(startTriangleIndex);
        visitedTriangles.Add(startTriangleIndex);

        while (trianglesToCheck.Count > 0)
        {
            int currentTriangleIndex = trianglesToCheck.Dequeue();
            connectedTriangles.Add(currentTriangleIndex);

            Vector3 currentNormal = GetTriangleNormal(currentTriangleIndex, vertices, triangles);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (visitedTriangles.Contains(i)) continue;

                if (AreTrianglesConnected(currentTriangleIndex, i, triangles) && IsAngleWithinThreshold(currentNormal, GetTriangleNormal(i, vertices, triangles)))
                {
                    trianglesToCheck.Enqueue(i);
                    visitedTriangles.Add(i);
                }
            }
        }

        return connectedTriangles;
    }

    Vector3 GetTriangleNormal(int triangleIndex, Vector3[] vertices, int[] triangles)
    {
        Vector3 v0 = vertices[triangles[triangleIndex]];
        Vector3 v1 = vertices[triangles[triangleIndex + 1]];
        Vector3 v2 = vertices[triangles[triangleIndex + 2]];

        return Vector3.Cross(v1 - v0, v2 - v0).normalized;
    }

    bool AreTrianglesConnected(int triangleIndex1, int triangleIndex2, int[] triangles)
    {
        HashSet<int> vertices1 = new HashSet<int>
        {
            triangles[triangleIndex1],
            triangles[triangleIndex1 + 1],
            triangles[triangleIndex1 + 2]
        };

        HashSet<int> vertices2 = new HashSet<int>
        {
            triangles[triangleIndex2],
            triangles[triangleIndex2 + 1],
            triangles[triangleIndex2 + 2]
        };

        foreach (int v1 in vertices1)
        {
            foreach (int v2 in vertices2)
            {
                if (vertexMapping[v1].Contains(v2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool IsAngleWithinThreshold(Vector3 normal1, Vector3 normal2)
    {
        return Vector3.Angle(normal1, normal2) < degree;
    }

    void ApplyMaterialToTriangles(List<int> triangleIndices)
    {
        Mesh newMesh = new Mesh();
        newMesh.vertices = selectedMesh.vertices;
        newMesh.normals = selectedMesh.normals;
        newMesh.uv = selectedMesh.uv; // UV 맵핑 보존

        // 원본 메쉬의 삼각형 리스트를 복사하여 수정
        List<int> mainMeshTriangles = new List<int>(selectedMesh.triangles);
        List<int> submeshTriangles = new List<int>();

        foreach (int triangleIndex in triangleIndices)
        {
            for (int i = 0; i < 3; i++)
            {
                int vertexIndex = triangleIndex + i;
                submeshTriangles.Add(mainMeshTriangles[vertexIndex]);
                mainMeshTriangles[vertexIndex] = -1; // 메인 메쉬에서 삼각형 제거 표시
            }
        }

        mainMeshTriangles.RemoveAll(index => index == -1);

        newMesh.subMeshCount = 2;
        newMesh.SetTriangles(mainMeshTriangles.ToArray(), 0); // 메인 서브메쉬
        newMesh.SetTriangles(submeshTriangles.ToArray(), 1);  // 새로운 서브메쉬

        newMesh.RecalculateNormals();

        MeshFilter meshFilter = selectedObjectTransform.GetComponent<MeshFilter>();
        meshFilter.mesh = newMesh;

        Material[] newMaterials = new Material[2];
        newMaterials[0] = selectedObjectTransform.GetComponent<MeshRenderer>().material;
        newMaterials[1] = newMaterial;

        selectedObjectTransform.GetComponent<MeshRenderer>().materials = newMaterials;
    }





    List<List<int>> FindEdgeVertices(List<int> triangleIndices)
    {
        Dictionary<(int, int), int> edgeDict = new Dictionary<(int, int), int>();
        List<int> triangles = triangleIndices.SelectMany(t => new int[] { t, t + 1, t + 2 }).ToList();
        Vector3[] vertices = selectedMesh.vertices;
        int[] meshTriangles = selectedMesh.triangles;

        // 삼각형의 모든 엣지를 추출하여 딕셔너리에 저장 (정렬하여 저장)
        for (int i = 0; i < triangles.Count; i += 3)
        {
            AddEdgeToDict(edgeDict, (meshTriangles[triangles[i]], meshTriangles[triangles[i + 1]]));
            AddEdgeToDict(edgeDict, (meshTriangles[triangles[i + 1]], meshTriangles[triangles[i + 2]]));
            AddEdgeToDict(edgeDict, (meshTriangles[triangles[i + 2]], meshTriangles[triangles[i]]));
        }

        // 딕셔너리에 저장된 엣지 중 하나만 등장하는 엣지들만 추출
        List<(int, int)> boundaryEdges = edgeDict.Where(edge => edge.Value == 1).Select(edge => edge.Key).ToList();

        // 엣지들을 연결하여 순서대로 정렬된 엣지 리스트를 생성
        List<List<int>> orderedEdges = OrderEdges(boundaryEdges);

        return orderedEdges;
    }

    void AddEdgeToDict(Dictionary<(int, int), int> edgeDict, (int, int) edge)
    {
        // 엣지를 항상 낮은 인덱스가 먼저 오도록 정렬하여 일관성 유지
        var sortedEdge = edge.Item1 < edge.Item2 ? edge : (edge.Item2, edge.Item1);
        if (edgeDict.ContainsKey(sortedEdge))
        {
            edgeDict[sortedEdge]++;
        }
        else
        {
            edgeDict[sortedEdge] = 1;
        }
    }

    List<List<int>> OrderEdges(List<(int, int)> boundaryEdges)
    {
        List<List<int>> edgeLoops = new List<List<int>>();

        while (boundaryEdges.Count > 0)
        {
            List<int> currentLoop = new List<int>();

            var edge = boundaryEdges[0];
            boundaryEdges.RemoveAt(0);

            currentLoop.Add(edge.Item1);
            currentLoop.Add(edge.Item2);

            bool loopClosed = false;

            while (!loopClosed)
            {
                loopClosed = true;

                for (int i = 0; i < boundaryEdges.Count; i++)
                {
                    var nextEdge = boundaryEdges[i];

                    if (currentLoop.Last() == nextEdge.Item1)
                    {
                        currentLoop.Add(nextEdge.Item2);
                        boundaryEdges.RemoveAt(i);
                        loopClosed = false;
                        break;
                    }
                    else if (currentLoop.Last() == nextEdge.Item2)
                    {
                        currentLoop.Add(nextEdge.Item1);
                        boundaryEdges.RemoveAt(i);
                        loopClosed = false;
                        break;
                    }
                }
            }

            edgeLoops.Add(currentLoop);
        }

        return edgeLoops;
    }
}