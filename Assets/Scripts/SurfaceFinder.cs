using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

// ���콺�� ��ġ�� ���� Surface�� �����ϴ� ����� ���� class.
// Ŭ���� ������ �ﰢ���� �������� �ֺ� �ﰢ������ ã��, normalVector���� �������̰� ���� �����̸� ���� cluster�� ���� ������ �ݺ��Ͽ�  surface�� �����Ѵ�.
// 3D ���� �𼭸��� ���� �������� �����ϰ� �̾����ִ� ��쿡, Surface�� �� �и����� ���Ѵ�. �׷��� ����� ������� ����.
public class SurfaceFinder : MonoBehaviour
{
    public Material newMaterial;
    public GameObject redDotPrefab; // �̸� �غ��ص� redDot ������
    public float degree = 30.0f; // ���� ���� ��밪�� public ������ ����
    public float epsilon = 0.001f; // ��ǥ �� �� ����ϴ� ���� ��밪

    private Dictionary<int, HashSet<int>> vertexMapping; // ������ ��ġ�� ������ ���ؽ� ����
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

                // ������ ��ġ�� ������ ���ؽ� ���� ����
                CreateVertexMapping();

                int triangleIndex = hit.triangleIndex * 3;

                if (triangleIndex < 0 || triangleIndex >= selectedMesh.triangles.Length) return;


                // Surface�� ã��, Edge�� ã��
                List<int> connectedTriangles = FindConnectedTriangles(triangleIndex);
                List<List<int>> edgeVertices = FindEdgeVertices(connectedTriangles);

                // Edge ���� �α� ���
                int edgeNumber = 1;
                foreach (var edge in edgeVertices)
                {
                    Debug.Log($"Edge {edgeNumber}: " + string.Join(", ", edge.Select(v => "Vertex" + v)));
                    edgeNumber++;
                }

                // ���ο� Surface�� Material ����
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
        newMesh.uv = selectedMesh.uv; // UV ���� ����

        // ���� �޽��� �ﰢ�� ����Ʈ�� �����Ͽ� ����
        List<int> mainMeshTriangles = new List<int>(selectedMesh.triangles);
        List<int> submeshTriangles = new List<int>();

        foreach (int triangleIndex in triangleIndices)
        {
            for (int i = 0; i < 3; i++)
            {
                int vertexIndex = triangleIndex + i;
                submeshTriangles.Add(mainMeshTriangles[vertexIndex]);
                mainMeshTriangles[vertexIndex] = -1; // ���� �޽����� �ﰢ�� ���� ǥ��
            }
        }

        mainMeshTriangles.RemoveAll(index => index == -1);

        newMesh.subMeshCount = 2;
        newMesh.SetTriangles(mainMeshTriangles.ToArray(), 0); // ���� ����޽�
        newMesh.SetTriangles(submeshTriangles.ToArray(), 1);  // ���ο� ����޽�

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

        // �ﰢ���� ��� ������ �����Ͽ� ��ųʸ��� ���� (�����Ͽ� ����)
        for (int i = 0; i < triangles.Count; i += 3)
        {
            AddEdgeToDict(edgeDict, (meshTriangles[triangles[i]], meshTriangles[triangles[i + 1]]));
            AddEdgeToDict(edgeDict, (meshTriangles[triangles[i + 1]], meshTriangles[triangles[i + 2]]));
            AddEdgeToDict(edgeDict, (meshTriangles[triangles[i + 2]], meshTriangles[triangles[i]]));
        }

        // ��ųʸ��� ����� ���� �� �ϳ��� �����ϴ� �����鸸 ����
        List<(int, int)> boundaryEdges = edgeDict.Where(edge => edge.Value == 1).Select(edge => edge.Key).ToList();

        // �������� �����Ͽ� ������� ���ĵ� ���� ����Ʈ�� ����
        List<List<int>> orderedEdges = OrderEdges(boundaryEdges);

        return orderedEdges;
    }

    void AddEdgeToDict(Dictionary<(int, int), int> edgeDict, (int, int) edge)
    {
        // ������ �׻� ���� �ε����� ���� ������ �����Ͽ� �ϰ��� ����
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
