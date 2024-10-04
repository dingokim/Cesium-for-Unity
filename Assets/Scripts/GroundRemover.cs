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
    public float epsilon = 0.1f; // DBSCAN에서 사용할 거리 기준
    public int minPts = 0; // DBSCAN에서 사용할 최소 포인트 수
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


        // upVector를 계산한다.
        //upVector = CalculateUpVectorByECEFConvert(hit.point);
        /*
        이유는 모르겠지만 CesiumGeoreference하 tile들 지면의 normalVector를 구해보면 항상 (0,-1,0)이다.
        일단 수동으로 upVector를 수정할 수 있게끔 public으로 선언해두었다.
        CesiumGeoreference 밖의 오브젝트들은 일반적인 유니티 오브젝트처럼 (0,1,0)을 up 방향으로 지정하면 된다.
        */


        // 노멀벡터가 upVector에 가까운 삼각형들을 clustering 한다. ( clusteredTriangles가 갱신된다.)
        ExtractUp();


        // clusteredTriangles의 삼각형들을 삼각형간의 거리를 기준으로 group화한다.
        List<List<int>> groups = DBSCAN(clusteredTriangles);


        // 지면으로 예상되는 그룹을 찾고, 그 그룹의 삼각형들을 원본 mesh에서 제거한다.
        FindGroundAndRemove(groups);


        // collider를 갱신
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

        // 로컬 좌표를 월드 좌표로 변환하여 저장
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

        // 클러스터에서 실제 삼각형 인덱스로 변환
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



    //DBSCAN알고리즘에서 삼각형간의 거리를 기준으로 이웃함을 판단하는 함수
    List<int> GetNeighbors(List<int> clusteredTriangles, int index)
    {
        List<int> neighbors = new List<int>();
        Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        int indexInCluster = clusteredTriangles[index];

        // 현재 삼각형의 정점 좌표
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



    // 원본에서 ground 삼각형만 제거하여 mesh를 갱신
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

            // 정점의 높이값 평균 계산
            float heightAverage = 0;
            for (int i = 0; i < vertexIndices_Hash.Count; i++)
            {
                heightAverage += CalculateHeight(originalVertices[vertexIndices[i]], upVector);
            }
            heightAverage = heightAverage / vertexIndices.Count;
            groupHeightAverages[k] = heightAverage;

            // 정점의 높이값 평균의 최대 최소 갱신
            if (heightAverage < minHeightAverage)
            {
                minHeightAverage = heightAverage;
            }
            if (heightAverage > maxHeightAverage)
            {
                maxHeightAverage = heightAverage;
            }
        }


        // 지면과 천장을 구분할 높이값을 책정. (아래는 최대~최소를 9:1로 내분하는 값을 기준으로 함.)
        float heightThreshold = 0.1f * maxHeightAverage + 0.9f * minHeightAverage;


        // 높이 평균이 기준값 미만인 그룹에 대해서 그룹에 포함되는 삼각형들을 원본 오브젝트에서 제거
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
        // upVector를 단위 벡터로 정규화합니다.
        Vector3 normalizedUpVector = upVector.normalized;

        // 점의 좌표와 upVector를 내적하여 높이를 계산합니다.
        float height = Vector3.Dot(point, normalizedUpVector);

        return height;
    }


    public void RemoveTrianglesByIndex(GameObject obj, List<int> group)
    {
        Mesh mesh = obj.GetComponent<MeshFilter>().mesh;

        // 현재 삼각형 인덱스 배열을 가져옵니다.
        List<int> triangles = new List<int>(mesh.triangles);

        // 인덱스가 정렬되어 있지 않으면 오류가 발생할 수 있으므로, 역순으로 정렬합니다.
        group.Sort((a, b) => b.CompareTo(a));

        // 삼각형 인덱스에서 group에 포함된 인덱스를 제거합니다.
        foreach (int index in group)
        {
            // 삼각형 하나당 3개의 인덱스가 있으므로, 3개의 인덱스를 제거합니다.
            triangles.RemoveAt(index * 3);
            triangles.RemoveAt(index * 3);
            triangles.RemoveAt(index * 3);
        }

        // 제거된 삼각형 인덱스로 Mesh를 다시 설정합니다.
        mesh.triangles = triangles.ToArray();

        // Mesh를 업데이트하여 변경사항을 적용합니다.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
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

        // 월드 좌표를 ECEF 좌표로 변환
        double3 ecefPosition = georeference.TransformUnityPositionToEarthCenteredEarthFixed(worldPosition_d3);

        double3 upVector_d3 = new double3(-ecefPosition.x, ecefPosition.z, -ecefPosition.y);

        Vector3 upVector = new Vector3((float)upVector_d3.x, (float)upVector_d3.y, (float)upVector_d3.z);
        return upVector.normalized;
    }
}