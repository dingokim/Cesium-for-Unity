using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using NUnit.Framework.Internal;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR;

public class GroundRemover : MonoBehaviour
{
    public CesiumGeoreference georeference;
    public GameObject targetObject;
    public Vector3 upVector = Vector3.up;

    public int degree = 20;
    public float epsilon = 0.1f; // DBSCAN���� ����� �Ÿ� ����
    public int minPts = 0; // DBSCAN���� ����� �ּ� ����Ʈ ��
    public float yWeight = 1;

    private List<int> clusteredTriangles = new List<int>();
    private List<Vector3> clusteredTrianglesCentroid = new List<Vector3>();

    private HashSet<GameObject> detectedTiles = new HashSet<GameObject>();

    private List<GameObject> totalGameObject = new List<GameObject>();


    public void RemoveChildGround() 
    {
        Debug.Log($"RemoveChildGround Called... child : {transform.GetChild(0).gameObject.name}");
        RemoveGround(transform.GetChild(0).gameObject);
    }

    public void RemoveGround(GameObject _targetObject)
    {
        targetObject = _targetObject;

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;


        // upVector�� ����Ѵ�.
        //upVector = CalculateUpVectorByECEFConvert(hit.point);
        /*
        ������ �𸣰����� CesiumGeoreference�� tile�� ������ normalVector�� ���غ��� �׻� (0,-1,0)�̴�.
        �ϴ� �������� upVector�� ������ �� �ְԲ� public���� �����صξ���.
        CesiumGeoreference ���� ������Ʈ���� �Ϲ����� ����Ƽ ������Ʈó�� (0,1,0)�� up �������� �����ϸ� �ȴ�.
        */


        // ��ֺ��Ͱ� upVector�� ����� �ﰢ������ clustering �Ѵ�. ( clusteredTriangles�� ���ŵȴ�.)
        ExtractUp();


        // clusteredTriangles�� �ﰢ������ �ﰢ������ �Ÿ��� �������� groupȭ�Ѵ�.
        List<List<int>> groups = DBSCAN(clusteredTriangles);


        // �������� ����Ǵ� �׷��� ã��, �� �׷��� �ﰢ������ ���� mesh���� �����Ѵ�.
        FindGroundAndRemove(groups);


        // collider�� ����
        MeshCollider meshCollider = targetObject.GetComponent<MeshCollider>();
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = targetObject.GetComponent<MeshFilter>().mesh;
    }

    void ExtractUp()
    {
        clusteredTriangles.Clear();
        clusteredTrianglesCentroid.Clear();

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        Vector3[] localVertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = new Vector3[localVertices.Length];

        // ���� ��ǥ�� ���� ��ǥ�� ��ȯ�Ͽ� ����
        for (int i = 0; i < localVertices.Length; i++)
        {
            vertices[i] = targetObject.transform.TransformPoint(localVertices[i]);
        }


        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            float angle = Vector3.Angle(normal, upVector);

            if (angle <= degree)
            {
                clusteredTriangles.Add(i / 3);
                clusteredTrianglesCentroid.Add((v0 + v1 + v2) / 3.0f);
            }
        }
    }


    List<List<int>> DBSCAN(List<int> clusteredTriangles)
    {
        List<List<int>> groups = new List<List<int>>();
        int[] labels = new int[clusteredTriangles.Count];
        int groupId = 0;

        for (int i = 0; i < clusteredTriangles.Count; i++)
        {
            if (labels[i] != 0) continue;

            List<int> neighbors = GetNeighbors(clusteredTriangles, i);
            if (neighbors.Count < minPts)
            {
                labels[i] = -1; // noise
                continue;
            }

            groupId++;
            List<int> group = new List<int>();
            ExpandCluster(clusteredTriangles, i, neighbors, groupId, labels, group);
            groups.Add(group);
        }

        // Ŭ�����Ϳ��� ���� �ﰢ�� �ε����� ��ȯ
        for (int i = 0; i < groups.Count; i++)
        {
            for (int j = 0; j < groups[i].Count; j++)
            {
                groups[i][j] = clusteredTriangles[groups[i][j]];
            }
        }

        return groups;
    }

    void ExpandCluster(List<int> clusteredTriangles, int index, List<int> neighbors, int groupId, int[] labels, List<int> group)
    {
        labels[index] = groupId;
        group.Add(index);

        for (int i = 0; i < neighbors.Count; i++)
        {
            int neighborIndex = neighbors[i];
            if (labels[neighborIndex] == -1)
            {
                labels[neighborIndex] = groupId;
                group.Add(neighborIndex);
            }
            else if (labels[neighborIndex] == 0)
            {
                labels[neighborIndex] = groupId;
                group.Add(neighborIndex);
                List<int> nextNeighbors = GetNeighbors(clusteredTriangles, neighborIndex);
                if (nextNeighbors.Count >= minPts)
                {
                    neighbors.AddRange(nextNeighbors);
                }
            }
        }
    }



    //DBSCAN�˰��򿡼� �ﰢ������ �Ÿ��� �������� �̿����� �Ǵ��ϴ� �Լ�
    List<int> GetNeighbors(List<int> clusteredTriangles, int index)
    {
        List<int> neighbors = new List<int>();
        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        int indexInCluster = clusteredTriangles[index];

        // ���� �ﰢ���� ���� ��ǥ
        Vector3[] indexTriangleVertices = new Vector3[3];
        int triangleIndex = clusteredTriangles[index];
        indexTriangleVertices[0] = vertices[triangles[triangleIndex * 3]];
        indexTriangleVertices[1] = vertices[triangles[triangleIndex * 3 + 1]];
        indexTriangleVertices[2] = vertices[triangles[triangleIndex * 3 + 2]];

        for (int i = 0; i < clusteredTriangles.Count; i++)
        {
            if (i == index) continue;

            Vector3[] currentTriangleVertices = new Vector3[3];
            int currentTriangleIndex = clusteredTriangles[i];
            currentTriangleVertices[0] = vertices[triangles[currentTriangleIndex * 3]];
            currentTriangleVertices[1] = vertices[triangles[currentTriangleIndex * 3 + 1]];
            currentTriangleVertices[2] = vertices[triangles[currentTriangleIndex * 3 + 2]];

            if (IsTriangleConnected(vertices, triangles, currentTriangleIndex, indexInCluster)) 
            {
                neighbors.Add(i);
            }
        }
        
        return neighbors;
    }



    // �������� ground �ﰢ���� �����Ͽ� mesh�� ����
    void FindGroundAndRemove(List<List<int>> groups)
    {
        float[] groupHeightAverages = new float[groups.Count];
        float minHeightAverage = float.MaxValue;
        float maxHeightAverage = float.MinValue;

        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        int[] originalTriangles = mesh.triangles;
        Vector3[] originalVertices = mesh.vertices;
        for (int i = 0; i < originalVertices.Length; i++)
        {
            originalVertices[i] = targetObject.transform.TransformPoint(originalVertices[i]);
        }

        for (int k = 0; k < groups.Count; k++)
        {
            List<int> group = groups[k];

            HashSet<int> vertexIndices_Hash = new HashSet<int>();

            for (int i = 0; i < group.Count; i++) 
            {
                int triangleIndex = group[i];

                vertexIndices_Hash.Add(originalTriangles[triangleIndex * 3]);
                vertexIndices_Hash.Add(originalTriangles[triangleIndex * 3 + 1]);
                vertexIndices_Hash.Add(originalTriangles[triangleIndex * 3 + 2]);
            }

            List<int> vertexIndices = new List<int>(vertexIndices_Hash);

            // ������ ���̰� ��� ���
            float heightAverage = 0;
            for (int i = 0; i < vertexIndices_Hash.Count; i++)
            {
                heightAverage += CalculateHeight(originalVertices[vertexIndices[i]], upVector);
            }
            heightAverage = heightAverage / vertexIndices.Count;
            groupHeightAverages[k] = heightAverage;

            // ������ ���̰� ����� �ִ� �ּ� ����
            if (heightAverage < minHeightAverage)
            {
                minHeightAverage = heightAverage;
            }
            if (heightAverage > maxHeightAverage)
            {
                maxHeightAverage = heightAverage;
            }
        }


        // ����� õ���� ������ ���̰��� å��. (�Ʒ��� �ִ�~�ּҸ� 9:1�� �����ϴ� ���� �������� ��.)
        float heightThreshold = 0.1f * maxHeightAverage + 0.9f * minHeightAverage;


        // ���� ����� ���ذ� �̸��� �׷쿡 ���ؼ� �׷쿡 ���ԵǴ� �ﰢ������ ���� ������Ʈ���� ����
        List<int> trianglesToRemove = new List<int>();
        for (int k = 0; k < groups.Count; k++)
        {
            if (groupHeightAverages[k] < heightThreshold)
                trianglesToRemove.AddRange(groups[k]);
        }
        RemoveTrianglesByIndex(targetObject, trianglesToRemove);
    }


    public float CalculateHeight(Vector3 point, Vector3 upVector)
    {
        // upVector�� ���� ���ͷ� ����ȭ�մϴ�.
        Vector3 normalizedUpVector = upVector.normalized;

        // ���� ��ǥ�� upVector�� �����Ͽ� ���̸� ����մϴ�.
        float height = Vector3.Dot(point, normalizedUpVector);

        return height;
    }


    public void RemoveTrianglesByIndex(GameObject obj, List<int> group)
    {
        Mesh mesh = obj.GetComponent<MeshFilter>().mesh;

        // ���� �ﰢ�� �ε��� �迭�� �����ɴϴ�.
        List<int> triangles = new List<int>(mesh.triangles);

        // �ε����� ���ĵǾ� ���� ������ ������ �߻��� �� �����Ƿ�, �������� �����մϴ�.
        group.Sort((a, b) => b.CompareTo(a));

        // �ﰢ�� �ε������� group�� ���Ե� �ε����� �����մϴ�.
        foreach (int index in group)
        {
            // �ﰢ�� �ϳ��� 3���� �ε����� �����Ƿ�, 3���� �ε����� �����մϴ�.
            triangles.RemoveAt(index * 3);
            triangles.RemoveAt(index * 3);
            triangles.RemoveAt(index * 3);
        }

        // ���ŵ� �ﰢ�� �ε����� Mesh�� �ٽ� �����մϴ�.
        mesh.triangles = triangles.ToArray();

        // Mesh�� ������Ʈ�Ͽ� ��������� �����մϴ�.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
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
                if (DistanceBetweenEdges(v1_1, edges1[i], v2_1, edges2[j]) < epsilon)
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





    private Vector3 CalculateUpVectorByECEFConvert(Vector3 hitPoint) 
    {
        double3 worldPosition_d3 = new double3(hitPoint.x, hitPoint.y, hitPoint.z);

        // ���� ��ǥ�� ECEF ��ǥ�� ��ȯ
        double3 ecefPosition = georeference.TransformUnityPositionToEarthCenteredEarthFixed(worldPosition_d3);

        double3 upVector_d3 = new double3(-ecefPosition.x, ecefPosition.z, -ecefPosition.y);

        Vector3 upVector = new Vector3((float)upVector_d3.x, (float)upVector_d3.y, (float)upVector_d3.z);
        return upVector.normalized;
    }
}
