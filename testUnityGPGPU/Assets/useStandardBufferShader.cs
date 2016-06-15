using UnityEngine;
using System.Collections;

public class useStandardBufferShader : MonoBehaviour
{

    public ComputeShader shaderSmooth;

    ComputeBuffer bufOrg, bufSmooth;

    const int count = 8;
    const float size = 5.0f;

    void Start()
    {
        float[] dataIn = new float[8] { 1, 2, 3, 4, 5, 6, 7, 8 };
        float[] dataOut = new float[8];

        bufOrg = new ComputeBuffer(count, sizeof(float), ComputeBufferType.Default);
        bufSmooth = new ComputeBuffer(count, sizeof(float), ComputeBufferType.Default);

        bufOrg.SetData(dataIn);

        shaderSmooth.SetBuffer(0, "bufferIn", bufOrg);
        shaderSmooth.SetBuffer(0, "bufferOut", bufSmooth);
        shaderSmooth.Dispatch(0, bufSmooth.count / 8, 1, 1);

        bufSmooth.GetData(dataOut);

        string line = "";
        for (int j = 0; j < 8; j++)
        {
            line += " " + dataOut[j];
        }
        Debug.Log(line);
    }

    void OnDestroy()
    {

        bufOrg.Release();
        bufSmooth.Release();

    }
}
