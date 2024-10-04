using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR;

// 모든 면에 대한 clustering & grouping을 하기 위해 작성했던 코드
// 접근 방식이 바뀌어서 객체화에 쓰이진 않지만, 추후 최적화 과정에 사용될 수 있을 것 같아 남겨둠.
public class ClusterByTriangleDistance : MonoBehaviour
{
    public CesiumGeoreference georeference;
    public GameObject targetObject;
    public Vector3 upVector;

    private int startTriangleIndex;
    private GameObject startGroup;
    public int degree = 15;
    public float epsilon = 0.1f; // DBSCAN에서 사용할 거리 기준
    public int minPts = 0; // DBSCAN에서 사용할 최소 포인트 수
    public float yWeight = 1;

    private List<int> clusteredTriangles = new List<int>();
    private List<Vector3> clusteredTrianglesCentroid = new List<Vector3>();

    private HashSet<GameObject> detectedTiles = new HashSet<GameObject>();

    private List<GameObject> totalGameObject = new List<GameObject>();


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                targetObject = hit.collider.gameObject;
                Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
                startTriangleIndex = hit.triangleIndex;

                // upVector를 계산한다.
                //upVector = CalculateUpVectorByECEFConvert(hit.point);

                /*
                이유는 모르겠지만 CesiumGeoreference하 tile들 지면의 normalVector를 구해보면 항상 (0,-1,0)이다.
                일단 수동으로 upVector를 수정할 수 있게끔 public으로 선언해두었다.
                CesiumGeoreference 밖의 오브젝트들은 일반적인 유니티 오브젝트처럼 (0,1,0)을 up 방향으로 지정하면 된다.
                */



                // 노멀벡터가 upVector에 가까운 삼각형들을 clustering 한다. 
                // clusteredTriangles가 갱신된다.
                ExtractUp();


                // clusteredTriangles의 삼각형들을 삼각형간의 거리를 기준으로 group화한다.
                List<List<int>> groups = DBSCAN(clusteredTriangles);


                // 각 그룹을 오브젝트화 하여 시각화. (시작Tile의 경우 startGroup도 찾는다.)
                VisualizeCluster(groups);


                // startGroup에서부터 충돌체크를 통해 다음 목표 tile을 찾아 nextTiles에 넣는다.
                // 충돌체크하며 확장해갈 때 지면으로 판단되는 그룹은 포함하지 않는다.
                ;
                ;
                ;
                ;
                ;
            }


            // targetTiles에 nextTiles를 복사. nextTiles를 초기화.
            // targetTiles의 각 원소에 대해서 차례대로 '클러스터화->그룹화->시각화->다음타일찾기' 를 반복
            // targetTiles가 비었다면 startGroup에서 충돌체크를 통해 확장해가며 더이상 방문한적 없는 Tile을 찾을 수 없다면 종료

            ;
            ;
            ;
            ;
        }
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

            // 두 삼각형간의 거리가 epsilon보다 작다면 이웃하다고 판단
            float distance = TriangleDistance(indexTriangleVertices, currentTriangleVertices);
            if (distance <= epsilon)
            {
                neighbors.Add(i);
            }
        }

        return neighbors;
    }


    void VisualizeCluster_old(List<List<int>> groups)
    {
        GameObject[] groupObjects = new GameObject[groups.Count];
        float[] groupHeightAverages = new float[groups.Count];
        float minHeightAverage = float.MaxValue;
        float maxHeightAverage = float.MinValue;

        for (int k = 0; k < groups.Count; k++)
        {
            List<int> group = groups[k];

            List<Vector3> vertices = new List<Vector3>();
            List<int> newTriangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>(); // UV 좌표를 저장할 리스트
            Dictionary<int, int> vertexMap = new Dictionary<int, int>();

            Mesh mesh = targetObject.GetComponent<MeshFilter>().mesh;
            Vector3[] originalVertices = mesh.vertices;
            for (int i = 0; i < originalVertices.Length; i++)
            {
                // 각 정점을 월드 좌표계로 변환
                originalVertices[i] = targetObject.transform.TransformPoint(originalVertices[i]);
            }

            int[] originalTriangles = mesh.triangles;
            Vector2[] originalUVs = mesh.uv; // targetObject의 UV 좌표

            GameObject newObj = new GameObject("Cluster");

            foreach (int triangleIndex in group)
            {
                if (startGroup == null && triangleIndex == startTriangleIndex) //시작 surface를 찾는 과정
                {
                    startGroup = newObj;
                    startGroup.name = "Start";
                }

                for (int i = 0; i < 3; i++)
                {
                    int originalIndex = originalTriangles[triangleIndex * 3 + i];

                    if (!vertexMap.ContainsKey(originalIndex))
                    {
                        vertexMap[originalIndex] = vertices.Count;
                        vertices.Add(originalVertices[originalIndex]); // 로컬 좌표 사용
                        uvs.Add(originalUVs[originalIndex]); // 해당 정점의 UV 좌표를 추가
                    }
                    newTriangles.Add(vertexMap[originalIndex]);
                }
            }

            newObj.AddComponent<MeshFilter>();
            newObj.AddComponent<MeshRenderer>();

            Mesh newMesh = new Mesh();
            newMesh.vertices = vertices.ToArray();
            newMesh.triangles = newTriangles.ToArray();
            newMesh.uv = uvs.ToArray(); // 새 메쉬에 UV 매핑 적용
            newMesh.RecalculateNormals();

            newObj.GetComponent<MeshFilter>().mesh = newMesh;
            newObj.GetComponent<MeshRenderer>().material = targetObject.GetComponent<MeshRenderer>().material; // 기존 머터리얼을 사용
            newObj.transform.SetParent(this.transform);

            groupObjects[k] = newObj;

            // 정점의 높이값 평균 계산
            float heightAverage = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                heightAverage += CalculateHeight(vertices[i], upVector);
            }
            heightAverage = heightAverage / vertices.Count;
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

        for (int k = 0; k < groups.Count; k++)
        {
            //높이 평균이 기준값 미만인 그룹에 대해서
            if (groupHeightAverages[k] < heightThreshold)
            {
                //이름을 Ground로 갱신한다. 
                groupObjects[k].name = "Ground";
                groupObjects[k].SetActive(false);
            }

        }
    }
    // 각 그룹마다 오브젝트를 생성하여 가시화 하는 방식


    // 각 그룹에 대한 정보만 담고있고, 원본에서 ground 삼각형만 제거하는 방식
    void VisualizeCluster(List<List<int>> groups)
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












    public float TriangleDistance(Vector3[] tri1, Vector3[] tri2)
    {
        float minDistance = float.MaxValue;

        // Tri1의 변
        Vector3[] edges1 = { tri1[1] - tri1[0], tri1[2] - tri1[1], tri1[0] - tri1[2] };

        // Tri2의 변
        Vector3[] edges2 = { tri2[1] - tri2[0], tri2[2] - tri2[1], tri2[0] - tri2[2] };

        // 각 변의 끝점
        Vector3[] tri1Points = { tri1[0], tri1[1], tri1[2] };
        Vector3[] tri2Points = { tri2[0], tri2[1], tri2[2] };

        // 모든 변 조합 간 최단 거리 계산
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                float distance = SegmentDistance(tri1Points[i], edges1[i], tri2Points[j], edges2[j]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }
        }

        return minDistance;
    }

    private static float SegmentDistance(Vector3 P, Vector3 d, Vector3 Q, Vector3 e)
    {
        Vector3 r = P - Q;
        float a = Vector3.Dot(d, d);
        float b = Vector3.Dot(d, e);
        float c = Vector3.Dot(e, e);
        float d1 = Vector3.Dot(d, r);
        float d2 = Vector3.Dot(e, r);

        float denominator = a * c - b * b;

        float s = (b * d2 - c * d1) / denominator;
        float t = (a * d2 - b * d1) / denominator;

        s = Mathf.Clamp01(s);
        t = Mathf.Clamp01(t);

        Vector3 closestPoint1 = P + s * d;
        Vector3 closestPoint2 = Q + t * e;

        return Vector3.Distance(closestPoint1, closestPoint2);
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