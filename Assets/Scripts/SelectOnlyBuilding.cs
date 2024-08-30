using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class MeshChunkManager : MonoBehaviour
{
    public GameObject targetObject; // Ÿ�� ������Ʈ ����

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

        // ��� �ﰢ���� ���� BFS�� �����Ͽ� �׷�ȭ
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

                    // ���� �ﰢ���� ����� �ﰢ���� Ž��
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

        // ���� ū �ﰢ�� �׷��� ã��
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

        // ���ο� �޽� ����
        List<Vector3> newVertices = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>(); // ���� ���ĵ� UV ��ǥ
        List<int> newTriangles = new List<int>();
        Dictionary<int, int> vertexMap = new Dictionary<int, int>(); // ���� �ε����� �� �ε����� ����

        foreach (int triangleIndex in largestChunk)
        {
            for (int i = 0; i < 3; i++)
            {
                int oldIndex = triangles[triangleIndex * 3 + i];

                if (!vertexMap.ContainsKey(oldIndex))
                {
                    vertexMap[oldIndex] = newVertices.Count;
                    newVertices.Add(vertices[oldIndex]);
                    newUVs.Add(uvs[oldIndex]); // UV ��ǥ�� ���� �߰�
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

        // collider�� ����
        MeshCollider meshCollider = target.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = newMesh;
        }
    }

    bool IsTriangleConnected(Vector3[] vertices, int[] triangles, int triangleIndex1, int triangleIndex2)
    {
        // �ﰢ�� 1�� AABB ���
        Bounds bounds1 = new Bounds(vertices[triangles[triangleIndex1 * 3]], Vector3.zero);
        bounds1.Encapsulate(vertices[triangles[triangleIndex1 * 3 + 1]]);
        bounds1.Encapsulate(vertices[triangles[triangleIndex1 * 3 + 2]]);

        // �ﰢ�� 2�� AABB ���
        Bounds bounds2 = new Bounds(vertices[triangles[triangleIndex2 * 3]], Vector3.zero);
        bounds2.Encapsulate(vertices[triangles[triangleIndex2 * 3 + 1]]);
        bounds2.Encapsulate(vertices[triangles[triangleIndex2 * 3 + 2]]);

        // AABB�� ��ġ���� Ȯ�� (���� ���͸�)
        if (!bounds1.Intersects(bounds2))
            return false;

        // AABB�� ��ġ�� ��쿡�� ��Ȯ�� �Ÿ� ���
        return CheckExactTriangleDistance(vertices, triangles, triangleIndex1, triangleIndex2);
    }

    bool CheckExactTriangleDistance(Vector3[] vertices, int[] triangles, int triangleIndex1, int triangleIndex2)
    {
        // �ﰢ�� 1�� ������
        Vector3 v1_1 = vertices[triangles[triangleIndex1 * 3]];
        Vector3 v1_2 = vertices[triangles[triangleIndex1 * 3 + 1]];
        Vector3 v1_3 = vertices[triangles[triangleIndex1 * 3 + 2]];

        // �ﰢ�� 2�� ������
        Vector3 v2_1 = vertices[triangles[triangleIndex2 * 3]];
        Vector3 v2_2 = vertices[triangles[triangleIndex2 * 3 + 1]];
        Vector3 v2_3 = vertices[triangles[triangleIndex2 * 3 + 2]];

        float tolerance = 0.01f; // �Ÿ� �񱳸� ���� ��� ����

        // �ﰢ�� 1�� �� ��
        Vector3[] edges1 = {
        v1_2 - v1_1,
        v1_3 - v1_2,
        v1_1 - v1_3
    };

        // �ﰢ�� 2�� �� ��
        Vector3[] edges2 = {
        v2_2 - v2_1,
        v2_3 - v2_2,
        v2_1 - v2_3
    };

        // ��� �� ���� �ּ� �Ÿ� ���
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
