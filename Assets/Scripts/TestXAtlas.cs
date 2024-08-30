using UnityEngine;

public class TestXAtlas : MonoBehaviour
{
    [SerializeField] Final final;
   
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.H))
        {
            final.CombineMeshes();
        }
        
    }
}
