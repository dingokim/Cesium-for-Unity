using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class MeshChunkManager : MonoBehaviour
{
    public GameObject targetObject; // 타겟 오브젝트 지정

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                targetObject = hit.collider.gameObject;
                RemoveDisconnectedTriangles(targetObject);

            }
        }
    }

    public void RemoveDisconnectedTrianglesInChild() 
    {
        RemoveDisconnectedTriangles(gameObject.transform.GetChild(0).gameObject);
    }

    void RemoveDisconnectedTriangles(GameObject target)
    {
        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.mesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector2[] uvs = mesh.uv;

        List<HashSet<int>> triangleGroups = new List<HashSet<int>>();
        HashSet<int> visitedTriangles = new HashSet<int>();

        // 모든 삼각형에 대해 BFS를 수행하여 그룹화
        for (int i = 0; i < triangles.Length / 3; i++)
        {
            if (!visitedTriangles.Contains(i))
            {
                HashSet<int> currentGroup = new HashSet<int>();
                Queue<int> triangleQueue = new Queue<int>();
                triangleQueue.Enqueue(i);
                visitedTriangles.Add(i);

                while (triangleQueue.Count > 0)
                {
                    int currentTriangleIndex = triangleQueue.Dequeue();
                    currentGroup.Add(currentTriangleIndex);

                    // 현재 삼각형과 연결된 삼각형을 탐색
                    for (int j = 0; j < triangles.Length / 3; j++)
                    {
                        if (!visitedTriangles.Contains(j) && IsTriangleConnected(vertices, triangles, currentTriangleIndex, j))
                        {
                            triangleQueue.Enqueue(j);
                            visitedTriangles.Add(j);
                        }
                    }
                }

                triangleGroups.Add(currentGroup);
            }
        }

        // 가장 큰 삼각형 그룹을 찾기
        HashSet<int> largestChunk = null;
        int maxGroupSize = 0;

        foreach (var group in triangleGroups)
        {
            if (group.Count > maxGroupSize)
            {
                largestChunk = group;
                maxGroupSize = group.Count;
            }
        }

        // 새로운 메쉬 생성
        List<Vector3> newVertices = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>(); // 새로 정렬된 UV 좌표
        List<int> newTriangles = new List<int>();
        Dictionary<int, int> vertexMap = new Dictionary<int, int>(); // 원래 인덱스를 새 인덱스로 매핑

        foreach (int triangleIndex in largestChunk)
        {
            for (int i = 0; i < 3; i++)
            {
                int oldIndex = triangles[triangleIndex * 3 + i];

                if (!vertexMap.ContainsKey(oldIndex))
                {
                    vertexMap[oldIndex] = newVertices.Count;
                    newVertices.Add(vertices[oldIndex]);
                    newUVs.Add(uvs[oldIndex]); // UV 좌표도 같이 추가
                }

                newTriangles.Add(vertexMap[oldIndex]);
            }
        }

        Mesh newMesh = new Mesh();
        newMesh.vertices = newVertices.ToArray();
        newMesh.uv = newUVs.ToArray();
        newMesh.triangles = newTriangles.ToArray();

        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        meshFilter.mesh = newMesh;

        // collider를 갱신
        MeshCollider meshCollider = target.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = newMesh;
        }
    }

    bool IsTriangleConnected(Vector3[] vertices, int[] triangles, int triangleIndex1, int triangleIndex2)
    {
        // 삼각형 1의 AABB 계산
        Bounds bounds1 = new Bounds(vertices[triangles[triangleIndex1 * 3]], Vector3.zero);
        bounds1.Encapsulate(vertices[triangles[triangleIndex1 * 3 + 1]]);
        bounds1.Encapsulate(vertices[triangles[triangleIndex1 * 3 + 2]]);

        // 삼각형 2의 AABB 계산
        Bounds bounds2 = new Bounds(vertices[triangles[triangleIndex2 * 3]], Vector3.zero);
        bounds2.Encapsulate(vertices[triangles[triangleIndex2 * 3 + 1]]);
        bounds2.Encapsulate(vertices[triangles[triangleIndex2 * 3 + 2]]);

        // AABB가 겹치는지 확인 (빠른 필터링)
        if (!bounds1.Intersects(bounds2))
            return false;

        // AABB가 겹치는 경우에만 정확한 거리 계산
        return CheckExactTriangleDistance(vertices, triangles, triangleIndex1, triangleIndex2);
    }

    bool CheckExactTriangleDistance(Vector3[] vertices, int[] triangles, int triangleIndex1, int triangleIndex2)
    {
        // 삼각형 1의 정점들
        Vector3 v1_1 = vertices[triangles[triangleIndex1 * 3]];
        Vector3 v1_2 = vertices[triangles[triangleIndex1 * 3 + 1]];
        Vector3 v1_3 = vertices[triangles[triangleIndex1 * 3 + 2]];

        // 삼각형 2의 정점들
        Vector3 v2_1 = vertices[triangles[triangleIndex2 * 3]];
        Vector3 v2_2 = vertices[triangles[triangleIndex2 * 3 + 1]];
        Vector3 v2_3 = vertices[triangles[triangleIndex2 * 3 + 2]];

        float tolerance = 0.01f; // 거리 비교를 위한 허용 오차

        // 삼각형 1의 세 변
        Vector3[] edges1 = {
        v1_2 - v1_1,
        v1_3 - v1_2,
        v1_1 - v1_3
    };

        // 삼각형 2의 세 변
        Vector3[] edges2 = {
        v2_2 - v2_1,
        v2_3 - v2_2,
        v2_1 - v2_3
    };

        // 모든 변 간의 최소 거리 계산
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (DistanceBetweenEdges(v1_1, edges1[i], v2_1, edges2[j]) < tolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    float DistanceBetweenEdges(Vector3 p1, Vector3 d1, Vector3 p2, Vector3 d2)
    {
        Vector3 r = p1 - p2;
        float a = Vector3.Dot(d1, d1);
        float b = Vector3.Dot(d1, d2);
        float c = Vector3.Dot(d2, d2);
        float d = Vector3.Dot(d1, r);
        float e = Vector3.Dot(d2, r);

        float denominator = a * c - b * b;

        float s = (b * e - c * d) / denominator;
        float t = (a * e - b * d) / denominator;

        s = Mathf.Clamp01(s);
        t = Mathf.Clamp01(t);

        Vector3 closestPointOnEdge1 = p1 + s * d1;
        Vector3 closestPointOnEdge2 = p2 + t * d2;

        return Vector3.Distance(closestPointOnEdge1, closestPointOnEdge2);
    }
}
