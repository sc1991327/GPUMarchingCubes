using UnityEngine;
using System.Collections;


[RequireComponent (typeof (MeshFilter))]
[RequireComponent (typeof (MeshRenderer))]
[RequireComponent (typeof (MeshCollider))]

public class VoxelChunk : MonoBehaviour {
	
	public int _SizeZ = 29;

    public RenderTexture DensityVolume;     // Render textures are textures that can be rendered to.
	public Texture3D DVolume;
	
	public Color _Col = Color.black;
	public Color cColor = Color.black;
	
	private float CollisionUpdateTime;
	private bool PlanToDestroy = false;
	public bool bActual = true;
	public bool bNeedCollisionUpdate = false;
	public bool ResetChunk = false;
	public bool bInitial = false;
	
	[HideInInspector]
	public bool bInitialized = false;
	
	public bool QPlanned = false,QDPlanned = false;
	
	
	
	public int Size
	{
		get { return _SizeZ+3; }
	}
	
	
	private Mesh DynamicCollider;
	
	public void Init()
	{
		if (bInitialized)
			return;
		
		DensityVolume = new RenderTexture(Size,Size,0,RenderTextureFormat.RFloat);
        DensityVolume.isVolume = true;      // If enabled, this Render Texture will be used as a Texture3D.
        DensityVolume.volumeDepth = Size;   // Volume extent of a 3D render texture.

        DensityVolume.enableRandomWrite = true;     // Enable random access write into this render texture on Shader Model 5.0 level shaders.
		DensityVolume.useMipMap = true;
		
		DensityVolume.wrapMode = TextureWrapMode.Clamp;
		DensityVolume.filterMode = FilterMode.Trilinear;
		
		//DensityVolume.mipMapBias=0.0f;
		//DensityVolume.anisoLevel = 8;
		//DensityVolume.antiAliasing = 8;

        DensityVolume.Create();             // Actually creates the RenderTexture.
	    //DensityVolume.DiscardContents(true,true);
	 	
        MeshFilter MF = GetComponent<MeshFilter>();
		MeshRenderer MR = GetComponent<MeshRenderer>();
		MeshCollider MC = GetComponent<MeshCollider>();
		
		if (MF.sharedMesh == null)
			MF.sharedMesh = new Mesh();
		
		VoxelCalculator.Instance.CreateEmptyVolume(DensityVolume, Size);
		//VoxelCalculator.Instance.CreateNoiseVolume(DensityVolume,transform.position,Size+0);
		//VoxelCalculator.Instance.CreatePlaneVolume(DensityVolume,transform.position);

		if (bInitial){
			VoxelCalculator.Instance.CreateNoiseVolume(DensityVolume,transform.position,Size);
			//VoxelCalculator.Instance.CreateSphereVolume(DensityVolume, transform.position);
			//VoxelCalculator.Instance.CreatePlaneVolume(DensityVolume,transform.position);
			//Invoke("Rebuild",0.2f);
			Rebuild();
		}

		bInitialized = true;
		//Init();
		
		MR.material = VoxelCalculator.Instance._DefaultMaterial;
		//Debug.Log("CHUNK CREATION TIME = " + (1000.0f*(Time.realtimeSinceStartup-startTime)).ToString()+"ms");
		
		MC.sharedMesh = MF.sharedMesh;
		CollisionUpdateTime = Time.time;
		
		if (transform.parent != null)
			name = transform.localPosition.ToString();

		//VoxelCalculator.Instance.FillBorders(DensityVolume, Neighbours, _SizeZ+0);
	}
	
	public void Rebuild()
	{
		MeshFilter MF = GetComponent<MeshFilter>();
		
		CHUNK_OPERATION d;
		
		d = delegate() 
		{
			if (VoxelCalculator.Instance._WithNeighbours != 0){
				RenderTexture[,,] Neighbours = VoxelCalculator.Instance.GetNeighbours(this);
				VoxelCalculator.Instance.BuildChunkMesh(DensityVolume, MF.sharedMesh, Neighbours);
			}
			else
				VoxelCalculator.Instance.BuildChunkMesh(DensityVolume, MF.sharedMesh, null);
			bNeedCollisionUpdate = true;
		};
		VoxelCalculator.Instance.QBuilder.Enqueue(d);
		
	}
	
	// Use this for initialization
	void Start () {

		float startTime = Time.realtimeSinceStartup;

		Init();

		/*MC.enabled = false;
		
		DynamicCollider = new Mesh();
		DynamicCollider.MarkDynamic();
		
		DynamicCollider.vertices = MF.sharedMesh.vertices;
		DynamicCollider.triangles = MF.sharedMesh.triangles;
			
		MC.sharedMesh = DynamicCollider;
		
		MC.enabled = true;*/
	}
	
	
	// Update is called once per frame
	void Update () {
		
		if (ResetChunk)
		{
			RenderTexture[,,] Neighbours = VoxelCalculator.Instance.GetNeighbours(this);
			VoxelCalculator.Instance.FillBorders(DensityVolume, Neighbours, Size);
			MeshFilter MF = GetComponent<MeshFilter>();
			VoxelCalculator.Instance.BuildChunkMesh(DensityVolume, MF.sharedMesh,Neighbours);
			ResetChunk = false;
		}
		
		cColor = Color.Lerp(cColor,_Col,2.0f*Time.deltaTime);
		DrawVoxel(cColor,transform.position);

		if (bNeedCollisionUpdate && Time.time > CollisionUpdateTime+0.3f)
		{
			ResetCollider();
			CollisionUpdateTime = Time.time;
			bNeedCollisionUpdate=false;
		}

	}
	
	void OnDestroy()
	{
		if (DensityVolume!=null){
			DensityVolume.Release();
		}
	}
	
	public void CheckNeedToDestroy()
	{
		MeshFilter MF = GetComponent<MeshFilter>();

		if (!QPlanned && (MF.sharedMesh == null || MF.sharedMesh.vertexCount < 3 ) ){
			GameObject.Destroy(gameObject);
		}
        else
        {
            PlanToDestroy = false;
        }
	}
	
	public void ResetCollider()
	{
		MeshFilter MF = GetComponent<MeshFilter>();
		MeshCollider MC = GetComponent<MeshCollider>();

		float startTime = Time.realtimeSinceStartup;
		if (MF.sharedMesh != null && MF.sharedMesh.triangles.Length > 3 ){
			
            MC.enabled = false;
			//MC.sharedMesh = MF.sharedMesh;
			
			/*MC.sharedMesh = null;
			DynamicCollider.Clear();
			DynamicCollider.vertices = MF.sharedMesh.vertices;
			DynamicCollider.triangles = MF.sharedMesh.triangles;
			MC.sharedMesh = DynamicCollider;*/

			MC.enabled = true;
			//Debug.Log("Collider rebuild time: " + (1000.0f*(Time.realtimeSinceStartup-startTime)).ToString()+"ms");
			_Col = Color.green;
			
			bActual = true;
		}
		/*else
		{
			bActual = false;
			
			if (!PlanToDestroy){
				//VoxelCalculator.Instance.CreateEmptyVolume(DensityVolume,_SizeZ+4);
				CHUNK_OPERATION d;
				d = delegate() 
				{
					CheckNeedToDestroy();//GameObject.Destroy(gameObject);
				};
				VoxelCalculator.Instance.QBuilder.Enqueue(d);
				PlanToDestroy = true;
			}
		}*/
		/*else{
			
			if (!PlanToDestroy){
				//VoxelCalculator.Instance.CreateEmptyVolume(DensityVolume,_SizeZ+4);
				CHUNK_OPERATION d;
				d = delegate() 
				{
					CheckNeedToDestroy();//GameObject.Destroy(gameObject);
				};
				VoxelCalculator.Instance.QRender.Enqueue(d);
				PlanToDestroy = true;
			}
		}*/
	}
	
    // draw voxel box
	public void DrawVoxel(Color Col, Vector3 Pos, float Dur = 0.0f)
	{
		Vector3 A,B,C,D,E,F,G,H;

        A = Pos + new Vector3(0, 0, 0);

        B = A + transform.right * (_SizeZ);
        C = A + transform.up * (_SizeZ);
        D = A + transform.forward * (_SizeZ);

        E = A + transform.right * (_SizeZ) + transform.forward * (_SizeZ);
        F = A + transform.right * (_SizeZ) + transform.up * (_SizeZ);

        G = A + transform.right * (_SizeZ) + transform.up * (_SizeZ) + transform.forward * (_SizeZ);
        H = A + transform.up * (_SizeZ) + transform.forward * (_SizeZ);
				
		Debug.DrawLine(A, B, Col,Dur);
		Debug.DrawLine(B, E, Col,Dur);
		Debug.DrawLine(E, D, Col,Dur);
		Debug.DrawLine(D, A, Col,Dur);
	
		Debug.DrawLine(C, F, Col,Dur);
		Debug.DrawLine(F, G, Col,Dur);
		Debug.DrawLine(G, H, Col,Dur);
		Debug.DrawLine(H, C, Col,Dur);
	
		Debug.DrawLine(A, C, Col,Dur);
		Debug.DrawLine(D, H, Col,Dur);
		Debug.DrawLine(E, G, Col,Dur);
		Debug.DrawLine(B, F, Col,Dur);
	}
}
