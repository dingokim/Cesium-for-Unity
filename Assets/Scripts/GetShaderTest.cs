using UnityEngine;

public class GetShaderTest : MonoBehaviour
{
    public Material cesiumMaterial; // 세슘 셰이더가 적용된 매터리얼
    public Texture2D baseColorTexture;

    public void SaveMaterialTexture(string filename)
    {
   
        // Cesium 셰이더에서 baseColorTexture를 가져오기
        cesiumMaterial = GetComponent<MeshRenderer>().material;
        baseColorTexture = cesiumMaterial.GetTexture("_baseColorTexture") as Texture2D;

        if (baseColorTexture != null)
        {
            // RenderTexture를 사용하여 텍스처를 읽기 가능한 Texture2D로 복사
            Texture2D readableTexture = GetReadableTexture(baseColorTexture);

            // 텍스처를 파일로 저장
            SaveTextureToFile(readableTexture, $"XAtlas/{filename}.jpg");
        }
        else
        {
            Debug.LogError("baseColorTexture를 찾을 수 없습니다.");
        }
    }
    

    Texture2D GetReadableTexture(Texture texture)
    {
        // RenderTexture 생성
        RenderTexture renderTexture = new RenderTexture(texture.width, texture.height, 0);
        Graphics.Blit(texture, renderTexture);

        // RenderTexture를 읽기 가능한 Texture2D로 복사
        Texture2D readableTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        RenderTexture.active = renderTexture;
        readableTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        readableTexture.Apply();

        // RenderTexture 활성화 해제 및 메모리 정리
        RenderTexture.active = null;
        renderTexture.Release();

        return readableTexture;
    }

    void SaveTextureToFile(Texture2D texture, string path)
    {
        // 텍스처 데이터를 PNG로 변환
        byte[] bytes = texture.EncodeToJPG();

        // 파일로 저장
        System.IO.File.WriteAllBytes(path, bytes);

        Debug.Log("Texture saved to: " + path);
    }
}
