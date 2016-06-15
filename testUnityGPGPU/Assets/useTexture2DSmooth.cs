using UnityEngine;
using System.Collections;

public class useTexture2DSmooth : MonoBehaviour {

    public ComputeShader shaderCreate, shaderSmooth;

    RenderTexture texOrg, texSmooth;

    void Start()
    {
        texOrg = new RenderTexture(64, 64, 0);
        texOrg.enableRandomWrite = true;
        texOrg.Create();

        texSmooth = new RenderTexture(64, 64, 0);
        texSmooth.enableRandomWrite = true;
        texSmooth.Create();

        shaderCreate.SetTexture(0, "texO", texOrg);
        shaderCreate.Dispatch(0, texOrg.width / 8, texOrg.height / 8, 1);

        shaderSmooth.SetTexture(0, "texO", texOrg);
        shaderSmooth.SetTexture(0, "texS", texSmooth);
        shaderSmooth.Dispatch(0, texSmooth.width / 8, texSmooth.height / 8, 1);
    }

    void OnGUI()
    {
        int w = Screen.width / 2;
        int h = Screen.height / 2;
        int s = 512;

        GUI.DrawTexture(new Rect(w - s / 2, h - s / 2, s, s), texSmooth);
    }

    void OnDestroy()
    {
        texOrg.Release();
        texSmooth.Release();
    }
}
